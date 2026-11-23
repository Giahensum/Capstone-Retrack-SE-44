using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Xunit;

namespace Retrack.Tests;

public sealed partial class StockReceiptTests
{
    private async Task<Guid> BulkSource(AppDbContext db, bool paid = true, bool handed = true)
    {
        var now = DateTime.UtcNow;
        var lot = new LiquidationLot { Id = Guid.NewGuid(), SellerId = bulkSeller, Title = "Lô kiểm thử nhập một bước",
            StartingPrice = 1000000, BidIncrement = 10000, Status = "AWARDED", SurveyStart = now.AddDays(-2),
            SurveyEnd = now.AddDays(-1), BiddingStart = now.AddHours(-2), OriginalEnd = now.AddHours(-1),
            BiddingEnd = now.AddHours(-1), CollectionDeadline = now.AddDays(2) };
        var bid = new LiquidationBid { Id = Guid.NewGuid(), Lot = lot, OwnerId = owner, DepotId = depot, Amount = 1000000, CreatedAt = now.AddHours(-2) };
        var agreement = new LiquidationAgreement { Lot = lot, WinningBid = bid, DepotId = depot, BuyerId = owner, Amount = 1000000,
            PaymentReportedAt = paid ? now : null, PaymentConfirmedAt = paid ? now : null,
            SellerHandedOverAt = paid && handed ? now : null };
        db.LiquidationAgreements.Add(agreement); await db.SaveChangesAsync(); return agreement.Id;
    }
    private static CreateStockReceiptDto BulkInput(Guid agreementId) => new() {
        OperationId = Guid.NewGuid(), BulkAgreementId = agreementId, SourceType = "BULK_PURCHASE", Reference = "Nhận cả lô một lần",
        ReceivedAt = DateTime.UtcNow, PurchaseCost = 1000000, PostImmediately = true, ConfirmBulkReceived = true,
        Lines = [new() { Kind = "MATERIAL", MaterialType = "IRON", Name = "Sắt", Unit = "kg", Quantity = 100 },
                 new() { Kind = "REUSABLE", Name = "Ghế", Unit = "cái", Quantity = 2 }] };

    [Fact]
    public async Task Bulk_intake_retry_and_race_records_receipt_collection_and_notice_once()
    {
        Guid agreement; await using (var db = Open()) agreement = await BulkSource(db);
        var input = BulkInput(agreement);
        async Task<Guid> Receive() { await using var db = Open(); return (await Stock(db).CreateAsync(owner, depot, input)).Id; }
        var results = await Task.WhenAll(Receive(), Receive()); Assert.Equal(results[0], results[1]);
        await using var check = Open();
        Assert.Equal(1, await check.DepotStockReceipts.CountAsync(x => x.BulkAgreementId == agreement && x.Status == "POSTED"));
        Assert.NotNull((await check.LiquidationAgreements.SingleAsync(x => x.Id == agreement)).OwnerCollectedAt);
        Assert.Equal(1, await check.Notifications.CountAsync(x => x.UserId == bulkSeller));
        Assert.Equal(100, Assert.Single(await Inventory(check).GetAsync(owner, depot)).AvailableKg);
        Assert.Equal(2, Assert.Single((await Stock(check).ReusableAsync(owner, depot, new())).Items).AvailableQuantity);
        Assert.False((await Stock(check).GetAsync(owner, depot, input.OperationId)).CostsConfirmed);
    }
    [Theory]
    [InlineData(true, false)] [InlineData(false, false)]
    public async Task Bulk_intake_cannot_skip_payment_or_seller_handover(bool paid, bool handed)
    {
        await using var db = Open(); var id = await BulkSource(db, paid, handed);
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(db).CreateAsync(owner, depot, BulkInput(id)));
        await using var check = Open(); Assert.Null((await check.LiquidationAgreements.SingleAsync(x => x.Id == id)).OwnerCollectedAt);
        Assert.False(await check.DepotStockReceipts.AnyAsync(x => x.BulkAgreementId == id));
    }
    [Fact]
    public async Task Bulk_intake_rejects_wrong_owner_changed_price_and_second_operation()
    {
        await using var db = Open(); var id = await BulkSource(db); var input = BulkInput(id);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Stock(db).CreateAsync(Guid.NewGuid(), depot, input));
        input.PurchaseCost = 900000;
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).CreateAsync(owner, depot, input));
        input.PurchaseCost = 1000000;
        await Stock(db).CreateAsync(owner, depot, input);
        await using var other = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Stock(other).CreateAsync(owner, depot, BulkInput(id)));
    }
    [Fact]
    public async Task Bulk_intake_rollback_preserves_collection_and_stock_when_save_fails()
    {
        await using var db = Open(); var id = await BulkSource(db);
        var service = new DepotStockService(new DepotStockRepository(db), new DepotBatchRepository(db), new FailAfterSave(new DepotUnitOfWork(db)), Scope(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(owner, depot, BulkInput(id)));
        await using var check = Open(); Assert.Null((await check.LiquidationAgreements.SingleAsync(x => x.Id == id)).OwnerCollectedAt);
        Assert.False(await check.DepotStockReceipts.AnyAsync(x => x.BulkAgreementId == id));
        Assert.False(await check.Notifications.AnyAsync(x => x.UserId == bulkSeller));
    }
    [Fact]
    public async Task Bulk_intake_flag_requires_immediate_post_and_existing_two_step_client_still_works()
    {
        await using var db = Open(); var id = await BulkSource(db); var input = BulkInput(id); input.PostImmediately = false;
        await Assert.ThrowsAsync<ArgumentException>(() => Stock(db).CreateAsync(owner, depot, input));
        await db.LiquidationAgreements.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.OwnerCollectedAt, DateTime.UtcNow));
        input.ConfirmBulkReceived = false;
        var draft = await Stock(db).CreateAsync(owner, depot, input); Assert.Equal("DRAFT", draft.Status);
        Assert.Equal("POSTED", (await Stock(db).ReviewAsync(owner, depot, draft.Id, "POSTED")).Status);
    }
}
