using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    private static DepotBuyingPriceService Prices(Retrack.API.Data.AppDbContext db) =>
        new(new DepotBuyingPriceRepository(db), new DepotBatchRepository(db), new DepotUnitOfWork(db), Scope(db));

    [Fact]
    public async Task Depot_publishes_own_prices_and_seller_sees_each_depot_condition_and_unpublication()
    {
        await using var db = Open(); var second = Guid.NewGuid();
        db.Depots.Add(new Depot { Id = second, OwnerId = owner, Name = "Kho so sánh giá", Address = "Đà Nẵng", VerificationStatus = "APPROVED" });
        await db.SaveChangesAsync();
        try
        {
            var first = await Prices(db).SaveAsync(owner, depot, "Sắt", new(6000, "Sắt sạch, không lẫn tạp", true, null));
            await Prices(db).SaveAsync(owner, second, "IRON", new(6500, "Chỉ nhận từ 50 kg", true, null));
            var seller = new SellerRepository(db);
            var rows = await seller.GetNearbyDepotsAsync(null, null);
            Assert.Equal(6000, rows.Single(x => x.Id == depot).Prices["IRON"]);
            Assert.Equal(6500, rows.Single(x => x.Id == second).Prices["IRON"]);
            Assert.Equal("Sắt sạch, không lẫn tạp", rows.Single(x => x.Id == depot).PriceConditions["IRON"]);
            Assert.NotNull(rows.Single(x => x.Id == depot).PricesUpdatedAt);
            var same = await Prices(db).SaveAsync(owner, depot, "IRON", new(6000, first.ConditionNote, true, null));
            Assert.Equal(first.UpdatedAt, same.UpdatedAt);
            await Prices(db).SaveAsync(owner, depot, "IRON", new(6000, first.ConditionNote, false, first.UpdatedAt));
            Assert.Empty((await seller.GetNearbyDepotsAsync(null, null)).Single(x => x.Id == depot).Prices);
            await db.Depots.Where(x => x.Id == second).ExecuteUpdateAsync(s => s.SetProperty(x => x.VerificationStatus, "UNVERIFIED"));
            Assert.DoesNotContain(await seller.GetNearbyDepotsAsync(null, null), x => x.Id == second);
        }
        finally { await db.Depots.Where(x => x.Id == second).ExecuteDeleteAsync(); }
    }

    [Fact]
    public async Task Price_updates_are_owner_scoped_and_do_not_reprice_existing_stock()
    {
        await using var db = Open(); var receipt = await Posted(db, Input(100, 5000));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Prices(db).SaveAsync(employee, depot, "IRON", new(6000, "", true, null)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Prices(db).SaveAsync(Guid.NewGuid(), depot, "IRON", new(6000, "", true, null)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Prices(db).ListAsync(owner, Guid.NewGuid(), new()));
        var price = await Prices(db).SaveAsync(owner, depot, "IRON", new(6000, "", true, null));
        await Prices(db).SaveAsync(owner, depot, "IRON", new(9000, "", true, price.UpdatedAt));
        await using var check = Open();
        Assert.Equal(500000, (await Stock(check).GetAsync(owner, depot, receipt.Id)).TotalCost);
        Assert.Single((await Prices(check).ListAsync(owner, depot, new())).Items);
    }

    [Theory]
    [InlineData(0)][InlineData(-100)][InlineData(1.5)][InlineData(1000000001)]
    public async Task Buying_price_rejects_invalid_amounts(decimal amount)
    {
        await using var db = Open();
        await Assert.ThrowsAsync<ArgumentException>(() => Prices(db).SaveAsync(owner, depot, "IRON", new(amount, "", true, null)));
        Assert.False(await db.DepotBuyingPrices.AnyAsync(x => x.DepotId == depot));
    }

    [Fact]
    public async Task Price_revision_prevents_lost_updates_and_duplicate_rows_under_race()
    {
        await using var db = Open(); var original = await Prices(db).SaveAsync(owner, depot, "IRON", new(6000, "", true, null));
        async Task<bool> Attempt(decimal amount)
        {
            await using var context = Open();
            try { await Prices(context).SaveAsync(owner, depot, "IRON", new(amount, "", true, original.UpdatedAt)); return true; }
            catch (DepotConflictException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Attempt(6500), Attempt(7000)), x => x);
        await using var check = Open(); Assert.Single((await Prices(check).ListAsync(owner, depot, new())).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => Prices(check).SaveAsync(owner, depot, "UNKNOWN", new(10, "", true, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => Prices(check).ListAsync(owner, depot, new() { PageSize = 101 }));
    }
}
