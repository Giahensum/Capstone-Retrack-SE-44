using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

public sealed partial class MoneyLedgerPostgresTests
{
    private static DepotStockService Intake(AppDbContext db, bool fail = false)
    {
        var config = new MoneyConfigReader(db);
        IDepotUnitOfWork work = new DepotUnitOfWork(db);
        if (fail) work = new IntakeFailure(work);
        return new(new DepotStockRepository(db), new DepotBatchRepository(db), work,
            new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)),
            new PlatformMoneyHooks(db, config, new PayableLedgerService(db, config, TimeProvider.System)));
    }
    private sealed class IntakeFailure(IDepotUnitOfWork inner) : IDepotUnitOfWork
    {
        public Task<IDepotTransaction> BeginAsync() => inner.BeginAsync();
        public async Task SaveAsync() { await inner.SaveAsync(); throw new InvalidOperationException("Lỗi thử trước commit"); }
    }
    [PostgresMoneyFact]
    public async Task Bulk_money_intake_preserves_sale_hook_once_and_rolls_back_with_inventory()
    {
        await SetConfig(MoneyConfigKeys.Mode, MoneyConfigKeys.ModePlatform);
        await using var seed = Open(); var now = DateTime.UtcNow;
        var depot = new Depot { Id = Guid.NewGuid(), OwnerId = payer, Name = "Kho kiểm thử riêng", VerificationStatus = "APPROVED" };
        var lot = new LiquidationLot { Id = Guid.NewGuid(), SellerId = payee, Title = "Lô thử money", Status = "AWARDED",
            StartingPrice = 1000, BidIncrement = 1, SurveyStart = now.AddDays(-2), SurveyEnd = now.AddDays(-1),
            BiddingStart = now.AddHours(-2), OriginalEnd = now.AddHours(-1), BiddingEnd = now.AddHours(-1), CollectionDeadline = now.AddDays(1) };
        var bid = new LiquidationBid { Id = Guid.NewGuid(), Lot = lot, OwnerId = payer, DepotId = depot.Id, Amount = 1000, CreatedAt = now.AddHours(-2) };
        var agreement = new LiquidationAgreement { Lot = lot, WinningBid = bid, DepotId = depot.Id, BuyerId = payer, Amount = 1000,
            PaymentReportedAt = now, PaymentConfirmedAt = now, SellerHandedOverAt = now };
        seed.Depots.Add(depot); seed.LiquidationAgreements.Add(agreement); await seed.SaveChangesAsync();
        seed.MoneyCollections.Add(new MoneyCollection { Flow = MoneyFlows.LiquidationBalance, SourceType = MoneySources.LiquidationAgreement,
            SourceId = agreement.Id, PayerId = payer, PayeeId = payee, Amount = 1000, Status = "PAID", PaidAt = now });
        await seed.SaveChangesAsync();
        var input = new CreateStockReceiptDto { OperationId = Guid.NewGuid(), BulkAgreementId = agreement.Id,
            SourceType = "BULK_PURCHASE", Reference = "Nhập lô money", ReceivedAt = now, PurchaseCost = 1000,
            PostImmediately = true, ConfirmBulkReceived = true,
            Lines = [new() { Kind = "MATERIAL", MaterialType = "IRON", Name = "Sắt", Unit = "kg", Quantity = 10 }] };
        await using (var fail = Open())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Intake(fail, true).CreateAsync(payer, depot.Id, input));
        await using (var afterFailure = Open())
        {
            Assert.Empty(await afterFailure.PayableLines.ToListAsync());
            Assert.Null((await afterFailure.LiquidationAgreements.SingleAsync(x => x.Id == agreement.Id)).OwnerCollectedAt);
            Assert.False(await afterFailure.DepotStockReceipts.AnyAsync());
        }
        for (var retry = 0; retry < 2; retry++)
        { await using var db = Open(); await Intake(db).CreateAsync(payer, depot.Id, input); }
        await using var check = Open(); var sale = Assert.Single(await check.PayableLines.ToListAsync());
        Assert.Equal(1000, sale.Gross); Assert.Equal(payee, sale.PayeeId); Assert.Equal(MoneySources.LiquidationAgreement, sale.SourceType);
        Assert.Equal(1, await check.DepotStockReceipts.CountAsync(x => x.Status == "POSTED"));
    }
}
