using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class ClosedPeriodBillingTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AdminService Service(AppDbContext db) => new(new UserRepository(db), new MarketPriceRepository(db),
        new AuditLogRepository(db), new PlatformInvoiceRepository(db), new NotificationService(db), db);
    private static void Fee(AppDbContext db, User payer, decimal amount) => db.PlatformTransactions.Add(new()
    {
        SourceType = "PICKUP_REQUEST", SourceId = Guid.NewGuid(), PayerId = payer.Id,
        FeeAmount = amount, CreatedAt = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    });
    private static User Payer(AppDbContext db)
    {
        var payer = new User { Role = "DEPOT_OWNER", FullName = "Kho đối soát", Email = Guid.NewGuid()+"@test.invalid" };
        db.Users.Add(payer);return payer;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CannotIssueInvoiceForOpenOrFutureVietnamMonth(int offset)
    {
        await using var db = Db();
        var period = DateTime.UtcNow.AddHours(7).AddMonths(offset);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).GenerateMonthlyInvoicesAsync(period.Year, period.Month, Guid.NewGuid()));
        Assert.Empty(db.PlatformInvoices);
    }

    [Fact]
    public async Task RegenerationReconcilesUnpaidInvoiceAndKeepsAudit()
    {
        await using var db = Db();var payer = Payer(db);Fee(db,payer,50);await db.SaveChangesAsync();
        var service = Service(db);var original = Assert.Single(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        Fee(db,payer,100);await db.SaveChangesAsync();
        var changed = Assert.Single(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        Assert.Equal(original.Id,changed.Id);Assert.Equal(150,changed.TotalFeeAmount);
        Assert.Single(db.PlatformInvoices);
        Assert.Contains(db.AuditLogs,x=>x.EntityId==original.Id && x.Action=="RECONCILE");
        Assert.Empty(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
    }

    [Theory]
    [InlineData("PAID",null)]
    [InlineData("SUBMITTED","https://example.invalid/proof.png")]
    [InlineData("PENDING","https://pay.example.invalid/checkout#orderCode=123")]
    public async Task LateFeeCreatesPayableSupplementWithoutChangingLockedInvoice(string state,string? proof)
    {
        await using var db=Db();var payer=Payer(db);Fee(db,payer,50);await db.SaveChangesAsync();
        var service=Service(db);var initial=Assert.Single(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        var original=await db.PlatformInvoices.SingleAsync();original.Status=state;original.PaymentProofUrl=proof;
        original.PaidAt=state=="PAID"?DateTime.UtcNow:null;await db.SaveChangesAsync();
        Fee(db,payer,100);await db.SaveChangesAsync();
        var supplement=Assert.Single(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        Assert.NotEqual(initial.Id,supplement.Id);Assert.Equal(100,supplement.TotalFeeAmount);Assert.Equal("PENDING",supplement.Status);
        Assert.Equal(1,supplement.Revision);Assert.Equal(initial.Id,supplement.AdjustmentOfId);
        Assert.Equal(50,original.TotalFeeAmount);Assert.Equal(state,original.Status);Assert.Equal(proof,original.PaymentProofUrl);
        Assert.Equal(2,await db.PlatformInvoices.CountAsync());
        Assert.Empty(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        Fee(db,payer,25);await db.SaveChangesAsync();
        var updated=Assert.Single(await service.GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        Assert.Equal(supplement.Id,updated.Id);Assert.Equal(125,updated.TotalFeeAmount);
        Assert.Equal(2,await db.PlatformInvoices.CountAsync());
    }

    [Fact]
    public void MonthClosesAtExactVietnamMidnight()
    {
        var end = new DateTimeOffset(2026,11,30,17,0,0,TimeSpan.Zero);
        Assert.Throws<ArgumentException>(()=>BillingPeriod.ClosedMonth(2026,11,end.AddTicks(-1)));
        var bounds = BillingPeriod.ClosedMonth(2026,11,end);
        Assert.Equal(new DateTime(2026,10,31,17,0,0,DateTimeKind.Utc),bounds.Start);
        Assert.Equal(end.UtcDateTime,bounds.End);
        Assert.Throws<ArgumentException>(()=>BillingPeriod.ClosedMonth(2026,12,end));
    }

    [Fact]
    public async Task NegativeDifferenceRequiresReconciliationInsteadOfSilentlyCreditingPaidInvoice()
    {
        await using var db=Db();var payer=Payer(db);Fee(db,payer,50);
        var invoice=new PlatformInvoice{Payer=payer,PeriodYear=2026,PeriodMonth=1,TotalFeeAmount=100,Status="PAID"};
        db.PlatformInvoices.Add(invoice);await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(()=>Service(db).GenerateMonthlyInvoicesAsync(2026,1,Guid.NewGuid()));
        Assert.Equal(100,invoice.TotalFeeAmount);Assert.Single(db.PlatformInvoices);
    }
}
