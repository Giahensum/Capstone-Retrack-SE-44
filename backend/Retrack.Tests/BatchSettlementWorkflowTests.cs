using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Factory;
using Xunit;

namespace Retrack.Tests;

public sealed class BatchSettlementWorkflowTests : IAsyncLifetime
{
    private readonly string schema = "settlement_" + Guid.NewGuid().ToString("N");
    private string connection = null!;
    private NpgsqlConnection admin = null!;
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        var other = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (source is null || other is null || new NpgsqlConnectionStringBuilder(source).Database != "Retrack_TV2_test"
            || new NpgsqlConnectionStringBuilder(other).Database != "Retrack_TV2_test") throw new InvalidOperationException("Chỉ chạy trên DB test.");
        admin = new NpgsqlConnection(source); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = false }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        await using var conn = new NpgsqlConnection(connection); await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;", conn);
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task DisposeAsync()
    {
        await using var cmd = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await cmd.ExecuteNonQueryAsync(); await admin.DisposeAsync();
    }
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private static User U(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.invalid", FullName = "Fixture quyết toán" };
    private static BatchService Batches(AppDbContext db)
    {
        var scope = new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
        var repo = new DepotBatchRepository(db); var work = new DepotUnitOfWork(db);
        return new(repo, work, scope, new InventoryService(new DepotInventoryRepository(db), scope), null!,
            new DepotStockService(new DepotStockRepository(db), repo, work, scope));
    }
    private static BatchSettlementService Decisions(AppDbContext db) => new(new DepotBatchRepository(db), new DepotUnitOfWork(db),
        new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)));
    private static SettlementRequest Price(decimal value = 1000) => new() { AgreedPricePerKg = value, VatLegalBasis = "Dữ liệu kiểm thử không áp dụng VAT" };
    private static BatchPaymentProofRequest Proof(Guid owner) => new() { PaymentReference = "CK-FIXTURE", PaymentProofUrl = $"/api/factory/attachments/{owner:N}/{Guid.Empty:N}" };
    private async Task<(Guid Owner, Guid FactoryOwner, Guid Depot, Guid Batch)> Seed()
    {
        await using var db = Open();
        var owner = U("DEPOT_OWNER"); var factoryOwner = U("FACTORY");
        var depot = new Depot { Owner = owner, Name = "Kho xác nhận", VerificationStatus = "APPROVED" };
        var factory = new Factory { Owner = factoryOwner, Name = "Nhà máy", AcceptedMaterialsCsv = "PET" };
        var pickup = new PickupRequest { Seller = U("SELLER"), TargetDepot = depot, Address = "Ngũ Hành Sơn", Status = "DONE" };
        pickup.Items.Add(new() { MaterialType = "PET", WeightKg = 100, PricePerKg = 1000, SubTotal = 100000 });
        db.AddRange(factory, pickup); await db.SaveChangesAsync();
        var created = await Batches(db).CreateAsync(owner.Id, depot.Id, new() { OperationId = Guid.NewGuid(), MaterialType = "PET", WeightKg = 10, TargetFactoryId = factory.Id });
        var batch = await db.InventoryBatches.SingleAsync(b => b.Id == created.Id);
        batch.Status = "VERIFIED"; batch.FactoryReceivedAt = DateTime.UtcNow;
        batch.QualityCheck = new() { Factory = factory, Batch = batch, ActualWeightKg = 10, IsAccepted = true, Grade = "A", PurityPercent = 95 };
        db.BatchQualityChecks.Add(batch.QualityCheck);
        await db.SaveChangesAsync(); return (owner.Id, factoryOwner.Id, depot.Id, batch.Id);
    }
    [Fact]
    public async Task New_batch_price_is_only_a_proposal_until_depot_agrees_and_receives_money()
    {
        var f = await Seed(); await using var db = Open();
        var result = await new FactoryOrderService(db).SettleAsync(f.FactoryOwner, f.Batch,
            new() { AgreedPricePerKg = 1000, VatLegalBasis = "Dữ liệu kiểm thử không áp dụng VAT" }, default);
        Assert.Equal("Success", result.Outcome.ToString());
        Assert.Equal("SETTLEMENT_PENDING", result.Data!.Status);
        var saved = await db.InventoryBatches.SingleAsync(x => x.Id == f.Batch);
        Assert.Null(saved.SettledAt);
        Assert.False(await db.PlatformTransactions.AnyAsync(x => x.SourceId == f.Batch));
    }
    [Fact]
    public async Task Both_parties_complete_in_order_retry_and_race_create_one_fee_and_keep_audit()
    {
        var f = await Seed();
        await using (var db = Open())
        {
            var factory = new FactoryOrderService(db);
            await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default);
            Assert.Equal("Conflict", (await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, Proof(f.FactoryOwner), default)).Outcome.ToString());
            await Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 1, Accept = true, Amount = 10000 });
            Assert.Empty(await db.PlatformTransactions.ToListAsync());
            Assert.Equal("Success", (await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, Proof(f.FactoryOwner), default)).Outcome.ToString());
            Assert.Equal("Success", (await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, Proof(f.FactoryOwner), default)).Outcome.ToString());
            Assert.Empty(await db.PlatformTransactions.ToListAsync());
            Assert.Null((await db.InventoryBatches.SingleAsync()).SettledAt);
            var dashboard = (await new FactoryDashboardService(db).GetAsync(f.FactoryOwner, default)).Data!;
            Assert.Equal(1, dashboard.PendingSettlementCount);
            Assert.Contains(dashboard.PriorityOrders, x => x.Id == f.Batch);
            var reports = new DepotReportRepository(db);
            var start = DateTime.UtcNow.AddDays(-1); var end = DateTime.UtcNow.AddDays(1);
            Assert.Empty((await reports.DailyAmountsAsync(f.Depot, start, end)).Revenue);
            Assert.Equal(0, (await reports.CostChannelsAsync(f.Depot, start, end)).Single(x => x.Channel == "FACTORY").TransactionCount);
            var row = await db.InventoryBatches.SingleAsync();
            Assert.True(await new DepotPartnershipRepository(db).HasOpenOrdersAsync(f.Depot, row.TargetFactoryId!.Value, default));
        }
        async Task Confirm() { await using var db = Open(); await Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 10000, "CK-FIXTURE"); }
        await Task.WhenAll(Confirm(), Confirm()); await Confirm();
        await using var check = Open();
        var saved = await check.InventoryBatches.SingleAsync();
        Assert.Equal("PAID", saved.Status); Assert.NotNull(saved.SettledAt);
        Assert.Equal(saved.DepotPaymentConfirmedAt, saved.SettledAt);
        var finalReports = new DepotReportRepository(check);
        var finalStart = DateTime.UtcNow.AddDays(-1); var finalEnd = DateTime.UtcNow.AddDays(1);
        Assert.Equal(10000m, (await finalReports.DailyAmountsAsync(f.Depot, finalStart, finalEnd)).Revenue.Sum(x => x.Amount));
        Assert.Equal(1, (await finalReports.CostChannelsAsync(f.Depot, finalStart, finalEnd)).Single(x => x.Channel == "FACTORY").TransactionCount);
        Assert.Equal(0, (await new FactoryDashboardService(check).GetAsync(f.FactoryOwner, default)).Data!.PendingSettlementCount);
        var fee = Assert.Single(await check.PlatformTransactions.ToListAsync());
        Assert.Equal(0m, fee.FeeAmount); Assert.Equal(f.FactoryOwner, fee.PayerId);
        Assert.Equal(4, await check.BatchWorkflowEvents.CountAsync());
    }
    [Fact]
    public async Task Dispute_requires_reason_new_proposal_invalidates_old_confirmation_and_keeps_snapshot()
    {
        var f = await Seed(); await using var db = Open(); var factory = new FactoryOrderService(db);
        await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default);
        await Assert.ThrowsAsync<ArgumentException>(() => Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 1, Amount = 10000 }));
        await Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 1, Amount = 10000, Reason = "Đề nghị giá 1.200đ/kg" });
        await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(1200), default);
        await Assert.ThrowsAsync<DepotConflictException>(() => Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 1, Amount = 10000, Accept = true }));
        await Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 2, Amount = 12000, Accept = true });
        Assert.Equal(4, await db.BatchWorkflowEvents.CountAsync());
        Assert.Contains("1000", (await db.BatchWorkflowEvents.OrderBy(x => x.CreatedAt).FirstAsync()).SnapshotJson);
    }

    [Fact]
    public async Task Factory_cash_payment_requires_depot_confirmation_and_cannot_replace_report_on_retry()
    {
        var f = await Seed(); await using var db = Open(); var factory = new FactoryOrderService(db);
        await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default);
        await Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 1, Amount = 10000, Accept = true });
        var payment = new BatchPaymentProofRequest { PaymentMethod = "CASH", PaymentReference = "BIEN-NHAN-001" };
        Assert.Equal("Success", (await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, payment, default)).Outcome.ToString());
        Assert.Equal("Success", (await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, payment, default)).Outcome.ToString());
        Assert.Equal("Conflict", (await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, new() { PaymentMethod = "CASH", PaymentReference = "KHAC" }, default)).Outcome.ToString());
        await Assert.ThrowsAsync<DepotConflictException>(() => Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 9999, "BIEN-NHAN-001"));
        await Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 10000, "BIEN-NHAN-001");
        await Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 10000, "BIEN-NHAN-001");
        await using var check = Open(); var batch = await check.InventoryBatches.SingleAsync(x => x.Id == f.Batch);
        Assert.Equal("PAID", batch.Status); Assert.Equal("CASH", batch.PaymentMethod); Assert.NotNull(batch.DepotPaymentConfirmedAt);
        Assert.Single(await check.BatchWorkflowEvents.Where(x => x.Action == "PAYMENT_CONFIRMED").ToListAsync());
    }
    [Fact]
    public async Task Retry_does_not_change_price_or_invoice_metadata_and_other_owner_cannot_approve()
    {
        var f = await Seed(); await using var db = Open(); var factory = new FactoryOrderService(db);
        await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default);
        Assert.Equal("Success", (await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default)).Outcome.ToString());
        Assert.Equal("Conflict", (await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(1200), default)).Outcome.ToString());
        var changed = Price(); changed.InvoiceNumber = "HOA-DON-KHAC";
        Assert.Equal("Conflict", (await factory.SettleAsync(f.FactoryOwner, f.Batch, changed, default)).Outcome.ToString());
        var other = U("DEPOT_OWNER"); db.Users.Add(other); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Decisions(db).DecideAsync(other.Id, f.Depot, f.Batch, new() { Revision = 1, Amount = 10000, Accept = true }));
        Assert.Equal(1, await db.BatchWorkflowEvents.CountAsync());
    }
    [Fact]
    public async Task Payment_confirmation_checks_amount_reference_and_cannot_run_before_proof()
    {
        var f = await Seed(); await using var db = Open(); var factory = new FactoryOrderService(db);
        await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default);
        await Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Revision = 1, Amount = 10000, Accept = true });
        await Assert.ThrowsAsync<DepotConflictException>(() => Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 10000, "CK-FIXTURE"));
        await factory.ReportPaymentAsync(f.FactoryOwner, f.Batch, Proof(f.FactoryOwner), default);
        await Assert.ThrowsAsync<DepotConflictException>(() => Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 9999, "CK-FIXTURE"));
        await Assert.ThrowsAsync<DepotConflictException>(() => Decisions(db).ConfirmPaymentAsync(f.Owner, f.Depot, f.Batch, 10000, "SAI"));
        Assert.False(await db.PlatformTransactions.AnyAsync());
    }
    [Fact]
    public async Task Invoice_already_checked_by_depot_cannot_be_replaced_and_oversized_price_is_rejected()
    {
        var f = await Seed(); await using var db = Open(); var factory = new FactoryOrderService(db);
        await factory.InvoiceAsync(f.FactoryOwner, f.Batch, new() { InvoiceFileUrl = "https://example.invalid/original.pdf" }, default);
        await factory.SettleAsync(f.FactoryOwner, f.Batch, Price(), default);
        await Decisions(db).DecideAsync(f.Owner, f.Depot, f.Batch, new() { Accept = true, Revision = 1, Amount = 10000 });
        Assert.Equal("Conflict", (await factory.InvoiceAsync(f.FactoryOwner, f.Batch, new() { InvoiceFileUrl = "https://example.invalid/replaced.pdf" }, default)).Outcome.ToString());
        var second = await Seed();
        var batch = await db.InventoryBatches.Include(x => x.QualityCheck).SingleAsync(x => x.Id == second.Batch);
        batch.QualityCheck!.ActualWeightKg = 1_000_000_000m; await db.SaveChangesAsync();
        Assert.Equal("Invalid", (await factory.SettleAsync(second.FactoryOwner, second.Batch, Price(1_000_000_000m), default)).Outcome.ToString());
        Assert.False(await db.BatchWorkflowEvents.AnyAsync(x => x.BatchId == second.Batch));
    }
}
