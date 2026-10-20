using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Liquidation;
using Xunit;
namespace Retrack.Tests;

public sealed class DepotLiquidationTests : IAsyncLifetime
{
    private readonly Guid owner = Guid.NewGuid(), competitor = Guid.NewGuid(), seller = Guid.NewGuid(), depot = Guid.NewGuid(), second = Guid.NewGuid(), rivalDepot = Guid.NewGuid(), lotId = Guid.NewGuid();
    private readonly TestClock clock = new(DateTime.UtcNow);
    private sealed class TestClock(DateTime now) : TimeProvider { public DateTime Now = now; public override DateTimeOffset GetUtcNow() => new(Now); }
    private static AppDbContext Open()
    {
        var cs = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        if (new NpgsqlConnectionStringBuilder(cs).Database != "Retrack_TV2_test") throw new InvalidOperationException("Sai DB test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
    }
    private static DepotService Scope(AppDbContext db) => new(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
    private LiquidationService Service(AppDbContext db) => new(new LiquidationRepository(db), Scope(db), new DepotUnitOfWork(db), new DepotBatchRepository(db), clock);
    private static DepotStockService Stock(AppDbContext db) => new(new DepotStockRepository(db), new DepotBatchRepository(db), new DepotUnitOfWork(db), Scope(db));
    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        db.Users.AddRange(new User { Id = owner, Email = $"bulk-{owner}@test.invalid", Role = "DEPOT_OWNER" }, new User { Id = competitor, Email = $"bulk-{competitor}@test.invalid", Role = "DEPOT_OWNER" }, new User { Id = seller, Email = $"bulk-{seller}@test.invalid", Role = "SELLER" });
        db.Depots.AddRange(new Depot { Id = depot, OwnerId = owner, Name = "Kho lô lớn", VerificationStatus = "APPROVED" }, new Depot { Id = second, OwnerId = owner, Name = "Kho thứ hai", VerificationStatus = "APPROVED" }, new Depot { Id = rivalDepot, OwnerId = competitor, Name = "Kho cạnh tranh", VerificationStatus = "APPROVED" });
        db.LiquidationLots.Add(new LiquidationLot { Id = lotId, SellerId = seller, Title = "Thanh lý khách sạn Đà Nẵng", PublicArea = "Ngũ Hành Sơn", Address = "Địa chỉ khảo sát riêng", ContactPhone = "0900000000", StartingPrice = 10000000, BidIncrement = 100000,
            SurveyStart = clock.Now.AddHours(-1), SurveyEnd = clock.Now.AddHours(1), BiddingStart = clock.Now.AddHours(2), OriginalEnd = clock.Now.AddHours(3), BiddingEnd = clock.Now.AddHours(3), CollectionDeadline = clock.Now.AddDays(2) });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.DepotStockReceipts.Where(x => x.DepotId == depot || x.DepotId == second || x.DepotId == rivalDepot).ExecuteDeleteAsync();
        await db.LiquidationAgreements.Where(x => x.LotId == lotId).ExecuteDeleteAsync();
        await db.LiquidationLots.Where(x => x.Id == lotId).ExecuteDeleteAsync();
        await db.Depots.Where(x => x.Id == depot || x.Id == second || x.Id == rivalDepot).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Id == owner || x.Id == competitor || x.Id == seller).ExecuteDeleteAsync();
    }
    private async Task Register(AppDbContext db, Guid who, Guid warehouse)
    { await Service(db).ParticipateAsync(who, warehouse, lotId, true, false); await Service(db).ParticipateAsync(who, warehouse, lotId, true, true); }
    [Fact]
    public async Task Survey_is_private_per_warehouse_and_requires_approved_active_owner()
    {
        await using var db = Open();
        Assert.Null((await Service(db).DetailAsync(owner, depot, lotId)).Address);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).ParticipateAsync(competitor, depot, lotId, true, false));
        var warehouse = await db.Depots.SingleAsync(x => x.Id == depot); warehouse.VerificationStatus = "UNVERIFIED"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).ParticipateAsync(owner, depot, lotId, true, false));
        warehouse.VerificationStatus = "APPROVED"; await db.SaveChangesAsync(); await Register(db, owner, depot);
        Assert.NotNull((await Service(db).DetailAsync(owner, depot, lotId)).Address);
        Assert.Null((await Service(db).DetailAsync(owner, second, lotId)).Address);
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).ParticipateAsync(owner, second, lotId, true, false));
        Assert.Equal("PUBLISHED", (await db.LiquidationLots.SingleAsync(x => x.Id == lotId)).Status);
    }
    [Fact]
    public async Task Bid_enforces_schedule_survey_minimum_idempotency_and_preserves_terminal_results()
    {
        await using var db = Open(); await Register(db, owner, depot);
        var input = new LiquidationBidInput(Guid.NewGuid(), 10000000);
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).BidAsync(owner, depot, lotId, input));
        clock.Now = clock.Now.AddHours(2.5);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).BidAsync(owner, depot, lotId, input with { Amount = 9999999 }));
        var first = await Service(db).BidAsync(owner, depot, lotId, input);
        Assert.Equal(first, await Service(db).BidAsync(owner, depot, lotId, input));
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).BidAsync(owner, depot, lotId, input with { Amount = 11000000 }));
        Assert.Single((await Service(db).MyBidsAsync(owner, depot, lotId, 1)).Items);
        Assert.Empty((await Service(db).MyBidsAsync(owner, second, lotId, 1)).Items);
        clock.Now = clock.Now.AddHours(1); await Service(db).FinalizeAsync(lotId); await Service(db).FinalizeAsync(lotId);
        Assert.Single(await db.LiquidationAgreements.Where(x => x.LotId == lotId).ToListAsync());
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).BidAsync(owner, depot, lotId, new(Guid.NewGuid(), 12000000)));
        Assert.Equal(first, await Service(db).BidAsync(owner, depot, lotId, input));
    }
    [Fact]
    public async Task Simultaneous_equal_bids_have_one_winner_and_concurrent_closing_has_one_agreement()
    {
        await using (var db = Open()) { await Register(db, owner, depot); await Register(db, competitor, rivalDepot); }
        clock.Now = clock.Now.AddHours(2.5);
        async Task<bool> Bid(Guid who, Guid warehouse)
        {
            await using var db = Open(); try { await Service(db).BidAsync(who, warehouse, lotId, new(Guid.NewGuid(), 10000000)); return true; }
            catch (ArgumentException) { return false; }
        }
        Assert.Equal(1, (await Task.WhenAll(Bid(owner, depot), Bid(competitor, rivalDepot))).Count(x => x));
        clock.Now = clock.Now.AddHours(1);
        async Task Close() { await using var db = Open(); await Service(db).FinalizeAsync(lotId); }
        await Task.WhenAll(Close(), Close());
        await using var check = Open(); Assert.Equal(1, await check.LiquidationBids.CountAsync(x => x.LotId == lotId)); Assert.Equal(1, await check.LiquidationAgreements.CountAsync(x => x.LotId == lotId));
    }
    [Fact]
    public async Task Last_minute_bid_extends_within_cap()
    {
        await using var db = Open(); await Register(db, owner, depot); clock.Now = clock.Now.AddMinutes(179);
        await Service(db).BidAsync(owner, depot, lotId, new(Guid.NewGuid(), 10000000)); db.ChangeTracker.Clear();
        var lot = await db.LiquidationLots.SingleAsync(x => x.Id == lotId); Assert.Equal(lot.OriginalEnd.AddMinutes(1), lot.BiddingEnd);
        Assert.Equal(lot.OriginalEnd.AddMinutes(10), LiquidationRules.Extend(lot.OriginalEnd.AddMinutes(9), lot.OriginalEnd.AddMinutes(10), lot.OriginalEnd.AddMinutes(10)));
    }
    [Fact]
    public async Task Empty_ended_lot_has_no_agreement_and_cannot_receive_bids()
    {
        await using var db = Open(); await Register(db, owner, depot); clock.Now = clock.Now.AddHours(4);
        await Service(db).FinalizeAsync(lotId);
        Assert.Equal("NO_SALE", (await db.LiquidationLots.SingleAsync(x => x.Id == lotId)).Status);
        Assert.Empty(await db.LiquidationAgreements.Where(x => x.LotId == lotId).ToListAsync());
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).BidAsync(owner, depot, lotId, new(Guid.NewGuid(), 10000000)));
    }
    [Fact]
    public async Task Outbid_and_award_notices_are_transactional_and_retries_do_not_duplicate_them()
    {
        await using var db = Open(); await Register(db, owner, depot); await Register(db, competitor, rivalDepot);
        clock.Now = clock.Now.AddHours(2.5);
        await Service(db).BidAsync(owner, depot, lotId, new(Guid.NewGuid(), 10000000));
        var input = new LiquidationBidInput(Guid.NewGuid(), 10100000);
        await Service(db).BidAsync(competitor, rivalDepot, lotId, input); await Service(db).BidAsync(competitor, rivalDepot, lotId, input);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).BidAsync(owner, depot, lotId, new(Guid.NewGuid(), 10100000)));
        var outbid = Assert.Single(await db.Notifications.Where(x => x.UserId == owner).ToListAsync());
        Assert.Equal(depot, outbid.DepotId); Assert.Equal("LIQUIDATION", outbid.Category); Assert.Equal(lotId, outbid.TargetId);
        clock.Now = clock.Now.AddHours(1); await Service(db).FinalizeAsync(lotId); await Service(db).FinalizeAsync(lotId);
        Assert.Single(await db.Notifications.Where(x => x.UserId == competitor).ToListAsync());
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.UserId == owner || x.UserId == competitor));
    }
    [Fact]
    public async Task Agreement_receipt_requires_both_sides_and_keeps_lump_sum_with_no_duplicate_stock()
    {
        await using var db = Open(); await Register(db, owner, depot); clock.Now = clock.Now.AddHours(2.5);
        await Service(db).BidAsync(owner, depot, lotId, new(Guid.NewGuid(), 10000000)); clock.Now = clock.Now.AddHours(1); await Service(db).FinalizeAsync(lotId);
        var agreement = await db.LiquidationAgreements.SingleAsync(x => x.LotId == lotId);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).AgreementAsync(competitor, rivalDepot, agreement.Id));
        await Assert.ThrowsAsync<DepotConflictException>(() => Service(db).AgreementActionAsync(owner, depot, agreement.Id, "CONFIRM_COLLECTED"));
        await Service(db).AgreementActionAsync(owner, depot, agreement.Id, "REPORT_PAYMENT", "CK-test");
        await Service(db).AgreementActionAsync(owner, depot, agreement.Id, "REPORT_PAYMENT", "CK-test");
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Service(db).AgreementActionAsync(owner, depot, agreement.Id, "CONFIRM_PAYMENT"));
        var input = new CreateStockReceiptDto { OperationId = Guid.NewGuid(), BulkAgreementId = agreement.Id, SourceType = "BULK_PURCHASE", Reference = "Lô khách sạn", ReceivedAt = DateTime.UtcNow, PurchaseCost = 10000000, CostsConfirmed = true,
            Lines = [new() { Kind = "MATERIAL", MaterialType = "IRON", Name = "Sắt", Unit = "kg", Quantity = 1000, ReferencePrice = 10000 }, new() { Kind = "REUSABLE", Name = "Ghế", Unit = "cái", Quantity = 10, ReferencePrice = 200000 }], Expenses = [new("FUEL", "Xăng xe", 200000)] };
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).CreateAsync(owner, depot, input));
        // Fixture mô phỏng contract đầu ra TV1; không gọi hoặc cài API xác nhận Seller thay TV1.
        agreement.PaymentConfirmedAt = DateTime.UtcNow; agreement.SellerHandedOverAt = DateTime.UtcNow; await db.SaveChangesAsync();
        await Service(db).AgreementActionAsync(owner, depot, agreement.Id, "CONFIRM_COLLECTED");
        var receipt = await Stock(db).CreateAsync(owner, depot, input);
        Assert.Equal(10200000, receipt.TotalCost); Assert.Equal(receipt.TotalCost, receipt.Lines.Sum(x => x.AllocatedCost));
        Assert.Equal(receipt.Id, (await Stock(db).CreateAsync(owner, depot, input)).Id);
        input.OperationId = Guid.NewGuid(); await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).CreateAsync(owner, depot, input));
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).UpdateCostsAsync(owner, depot, receipt.Id, new(1, true, receipt.Lines.Select(x => new StockLineCostInput(x.Id, x.ReferencePrice, 0)).ToList(), [])));
        await Stock(db).ReviewAsync(owner, depot, receipt.Id, "POSTED");
        Assert.Equal(receipt.Id, (await Service(db).AgreementAsync(owner, depot, agreement.Id)).ReceiptId);
    }
}
