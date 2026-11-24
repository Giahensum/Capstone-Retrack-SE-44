using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    [Fact]
    public async Task Inventory_history_unifies_sources_paginates_in_database_and_preserves_units()
    {
        await using var db = Open();
        var pickup = new PickupRequest { Id = Guid.NewGuid(), SellerId = bulkSeller, TargetDepotId = depot,
            Status = "DONE", StockReceivedAt = DateTime.UtcNow, Items = [new() { MaterialType = "PET", WeightKg = 5 }] };
        db.PickupRequests.Add(pickup); await db.SaveChangesAsync();
        try
        {
            var input = Input(2, 100, true); input.Reference = "Lịch sử ghế thử";
            await Stock(db).CreateAsync(owner, depot, input);
            var history = await Inventory(db).HistoryAsync(owner, depot, new() { PageSize = 1 });
            Assert.Equal(2, history.TotalCount); Assert.Single(history.Items);
            var second = await Inventory(db).HistoryAsync(owner, depot, new() { PageSize = 1, Page = 2 });
            Assert.NotEqual(history.Items[0].Id, Assert.Single(second.Items).Id);
            var receipts = await Inventory(db).HistoryAsync(owner, depot, new() { Source = "RECEIPT" });
            var receipt = Assert.Single(receipts.Items); Assert.Equal("DRAFT", receipt.Status);
            Assert.Equal("cái", Assert.Single(receipt.Lines).Unit);
            var pickups = await Inventory(db).HistoryAsync(owner, depot, new() { Source = "PICKUP", Status = "DONE" });
            Assert.Equal(pickup.Id, Assert.Single(pickups.Items).Id);
            Assert.Equal(5, Assert.Single(pickups.Items[0].Lines).Quantity);
            Assert.Single((await Inventory(db).HistoryAsync(owner, depot, new() { Search = "ghế thử" })).Items);
            Assert.Empty((await Inventory(db).HistoryAsync(owner, depot, new() { Source = "BULK_PURCHASE" })).Items);
        }
        finally { await db.PickupRequests.Where(x => x.Id == pickup.Id).ExecuteDeleteAsync(); }
    }
    [Fact]
    public async Task Inventory_history_rejects_wrong_owner_and_invalid_filters()
    {
        await using var db = Open();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Inventory(db).HistoryAsync(employee, depot, new()));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Inventory(db).HistoryAsync(owner, Guid.NewGuid(), new()));
        foreach (var query in new InventoryHistoryQuery[] { new() { Page = 0 }, new() { PageSize = 101 }, new() { Source = "FAKE" }, new() { Status = "PAID" }, new() { Page = int.MaxValue } })
            await Assert.ThrowsAsync<ArgumentException>(() => Inventory(db).HistoryAsync(owner, depot, query));
    }
}
