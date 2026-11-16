using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Factory;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.Tests;

public sealed class BuyerFeeWorkflowTests : IAsyncLifetime
{
    private readonly string schema = "buyer_fee_" + Guid.NewGuid().ToString("N");
    private string connection = null!;
    private NpgsqlConnection admin = null!;
    private string bootstrap = null!;

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
        var other = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (source is null || other is null || new NpgsqlConnectionStringBuilder(source).Database != "Retrack_TV2_test"
            || new NpgsqlConnectionStringBuilder(other).Database != "Retrack_TV2_test")
            throw new InvalidOperationException("Chỉ chạy fixture trên Retrack_TV2_test.");
        admin = new NpgsqlConnection(source);
        await admin.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await cmd.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = false }.ConnectionString;
        bootstrap = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        await Execute(bootstrap[..bootstrap.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;");
    }

    public async Task DisposeAsync()
    {
        // Chỉ xóa schema fixture do chính bài kiểm thử tạo trong DB test đã xác minh.
        await using var cmd = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
        await cmd.ExecuteNonQueryAsync();
        await admin.DisposeAsync();
    }

    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private async Task Execute(string sql)
    {
        await using var conn = new NpgsqlConnection(connection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync();
    }
    private static PickupService Pickup(AppDbContext db) => new(new PickupRequestRepository(db), db,
        new DepotPaymentService(new DepotOwnerRepository(db), new DepotPaymentRepository(db), new DepotUnitOfWork(db)));
    private static User User(string role) => new() { Role = role, Email = Guid.NewGuid() + "@test.invalid", FullName = "Fixture phí" };

    [Theory]
    [InlineData(PlatformFees.BuyerSeparate, 10000, 10500)]
    [InlineData(PlatformFees.Legacy, 9500, 10000)]
    public async Task Collection_handover_payment_race_and_fifo_keep_one_financial_snapshot(string policy, decimal receives, decimal sourceCost)
    {
        await using var db = Open();
        var owner = User("DEPOT_OWNER"); var seller = User("SELLER"); var employee = User("DEPOT_EMPLOYEE");
        var depot = new Depot { Owner = owner, Name = "Kho phí", VerificationStatus = "APPROVED" };
        db.AddRange(seller, new DepotStaff { Depot = depot, User = employee, StaffType = employee.Role });
        await db.SystemConfigs.Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE").ExecuteUpdateAsync(x => x.SetProperty(p => p.ConfigValue, "5.00"));
        await db.SaveChangesAsync();
        var created = await Pickup(db).CreateAsync(seller.Id, new CreatePickupRequestDto { TargetDepotId = depot.Id, Address = "Đà Nẵng" });
        Assert.Equal(PlatformFees.BuyerSeparate, created.FeePolicy);
        var pickup = await db.PickupRequests.SingleAsync(x => x.Id == created.Id);
        // Dòng legacy mô phỏng đơn đã tồn tại trước ngày chuyển chính sách.
        pickup.FeePolicy = policy; pickup.AcceptedCollectorId = employee.Id; pickup.Status = "IN_PROGRESS";
        pickup.CheckinImageUrl = "https://example.com/checkin.png";
        db.PickupCheckIns.Add(new PickupCheckIn { PickupRequest = pickup, Employee = employee, ImageUrl = pickup.CheckinImageUrl });
        await db.SystemConfigs.Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE").ExecuteUpdateAsync(x => x.SetProperty(p => p.ConfigValue, "7.00"));
        await db.SaveChangesAsync();
        var collection = new EmployeeCollectionService(db, null!);
        var draft = await collection.SaveAsync(employee.Id, pickup.Id, new SaveClassificationRequest(0,
            [new ClassificationItemInput("PET", 10, 1000)]), default);
        Assert.Equal(receives, draft.NetAmount); Assert.Equal(500, draft.PlatformFeeAmount);
        var sent = await collection.TransitionAsync(employee.Id, pickup.Id, "SUBMITTED", draft.Revision, default);
        Assert.Equal(receives, sent.NetAmount);
        Assert.Equal(receives, (await Pickup(db).ConfirmBySellerAsync(pickup.Id, seller.Id)).NetAmount);
        Assert.Equal("AWAITING_PAYMENT", (await collection.TransitionAsync(employee.Id, pickup.Id, "HANDED_OVER", draft.Revision, default)).Status);
        async Task Pay()
        {
            await using var payDb = Open();
            await Pickup(payDb).MarkPaymentSentAsync(pickup.Id, owner.Id, "https://example.com/proof.png");
        }
        await Task.WhenAll(Pay(), Pay());
        await using var saved = Open();
        var fee = await saved.PlatformTransactions.SingleAsync(x => x.SourceId == pickup.Id);
        Assert.Equal(500, fee.FeeAmount); Assert.Equal(5, fee.FeePercentage);
        Assert.Equal(owner.Id, fee.PayerId); Assert.Equal(policy, fee.FeePolicy);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Pickup(saved).MarkDoneAsync(pickup.Id, owner.Id));
        await Pickup(saved).MarkDoneAsync(pickup.Id, seller.Id);
        var layers = await new DepotStockRepository(saved).AvailableLayersAsync(depot.Id, "PET");
        Assert.Equal(sourceCost, Assert.Single(layers).UnitCost * 10);
        var report = await new DepotReportRepository(saved).DailyAmountsAsync(depot.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Equal(sourceCost == 10500 ? 10500 : 9500, Assert.Single(report.Costs).Amount);
        var sellerView = await Pickup(saved).GetByIdAsync(pickup.Id, seller.Id);
        Assert.Equal(receives, sellerView!.NetAmount); Assert.Equal(5, sellerView.PlatformFeePercentage);
    }

    [Theory]
    [InlineData(PlatformFees.BuyerSeparate, 11000, 11500)]
    [InlineData(PlatformFees.Legacy, 10500, 11000)]
    public async Task Factory_settlement_race_preserves_vat_and_separate_fee_after_reload(string policy, decimal receives, decimal buyerTotal)
    {
        await using var db = Open();
        var factory = new Factory { Owner = User("FACTORY"), Name = "Nhà máy test" };
        var depot = new Depot { Owner = User("DEPOT_OWNER"), Name = "Kho test" };
        var batch = new InventoryBatch { Depot = depot, TargetFactory = factory, Status = "VERIFIED", MaterialType = "PET",
            DeclaredWeightKg = 10, ActualWeightKg = 10, FeePolicy = policy, FeePercentageSnapshot = policy == PlatformFees.BuyerSeparate ? 5 : null };
        batch.QualityCheck = new BatchQualityCheck { Batch = batch, Factory = factory, ActualWeightKg = 10, IsAccepted = true };
        db.Add(batch);
        var configured = policy == PlatformFees.BuyerSeparate ? "7.00" : "5.00";
        await db.SystemConfigs.Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE").ExecuteUpdateAsync(x => x.SetProperty(p => p.ConfigValue, configured));
        await db.SaveChangesAsync();
        var request = new Retrack.API.DTOs.Factory.SettlementRequest { AgreedPricePerKg = 1000, PaymentReference = "FIXTURE-CK",
            VatTreatment = "TAXABLE", VatPercentage = 10, InvoiceNumber = "FIXTURE-HD", SellerTaxCode = "TEST",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        async Task<Retrack.API.DTOs.Factory.OrderSettlementResponse> Settle()
        {
            await using var other = Open();
            var result = await new FactoryOrderService(other).SettleAsync(factory.OwnerId, batch.Id, request, default);
            Assert.Equal(ServiceOutcome.Success, result.Outcome); return result.Data!;
        }
        var results = await Task.WhenAll(Settle(), Settle());
        Assert.All(results, x => { Assert.Equal(receives, x.NetPayableAmount); Assert.Equal(buyerTotal, x.BuyerTotalAmount); Assert.Equal(500, x.FeeAmount); Assert.Equal(policy, x.FeePolicy); });
        await using var saved = Open();
        Assert.Single(await saved.PlatformTransactions.Where(t => t.SourceId == batch.Id).ToListAsync());
        var view = (await new FactoryOrderService(saved).GetAsync(factory.OwnerId, batch.Id, default)).Data!;
        Assert.Equal(5, view.PlatformFeePercentage); Assert.Equal(buyerTotal, view.BuyerTotalAmount);
        var report = Assert.Single(await new DepotReportRepository(saved).CostChannelsAsync(depot.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)), x => x.Channel == "FACTORY");
        Assert.Equal(receives - 1000, report.NetRevenue);
        Assert.Equal(policy == PlatformFees.BuyerSeparate ? 0 : 500, report.PlatformFees);
        var otherFactory = new Factory { Owner = User("FACTORY"), Name = "Nhà máy khác" };
        saved.Factories.Add(otherFactory); await saved.SaveChangesAsync();
        var wrong = await new FactoryOrderService(saved).SettleAsync(otherFactory.OwnerId, batch.Id, request, default);
        Assert.Equal(ServiceOutcome.NotFound, wrong.Outcome);
        // Đổi owner sau quyết toán không được chuyển nghĩa vụ phí đã chốt sang tài khoản khác.
        var chargedOwnerId = factory.OwnerId;
        var successor = User("FACTORY"); saved.Users.Add(successor); await saved.SaveChangesAsync();
        await saved.Factories.Where(x => x.Id == factory.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.OwnerId, successor.Id));
        var adminService = new AdminService(new UserRepository(saved), new MarketPriceRepository(saved),
            new AuditLogRepository(saved), new PlatformInvoiceRepository(saved), new NotificationService(saved), saved);
        var now = DateTime.UtcNow.AddHours(7);
        var invoices = await adminService.GenerateMonthlyInvoicesAsync(now.Year, now.Month, depot.OwnerId);
        if (policy == PlatformFees.BuyerSeparate)
        {
            var invoice = Assert.Single(invoices);
            Assert.Equal(chargedOwnerId, invoice.PayerId); Assert.Equal(500, invoice.TotalFeeAmount);
        }
        else Assert.Empty(invoices); // Lô cũ đã khấu trừ phí, không phát hành hóa đơn thu lần hai.
        Assert.Empty(await adminService.GenerateMonthlyInvoicesAsync(now.Year, now.Month, depot.OwnerId));
    }

    [Fact]
    public async Task Upgrade_marks_existing_rows_legacy_without_changing_money_and_does_not_reset_new_policy()
    {
        await using var db = Open();
        var depot = new Depot { Owner = User("DEPOT_OWNER"), Name = "Kho cũ" };
        var pickup = new PickupRequest { Seller = User("SELLER"), TargetDepot = depot, Address = "Fixture cũ", Status = "DONE",
            GrossAmount = 10000, PlatformFeePercentage = 5, PlatformFeeAmount = 500, NetAmount = 9500 };
        var batch = new InventoryBatch { Depot = depot, MaterialType = "PET", DeclaredWeightKg = 10, Status = "COMPLETED",
            GrossAmount = 10000, VatAmount = 1000, InvoiceTotalAmount = 11000, PlatformFeeAmount = 500, NetAmount = 10500 };
        db.AddRange(pickup, batch, new PlatformTransaction { SourceType = "PICKUP_REQUEST", SourceId = pickup.Id,
            PayerId = depot.Owner.Id, FeeAmount = 500, TransactionAmount = 10000, FeePercentage = 5 });
        await db.SaveChangesAsync();
        await Execute("ALTER TABLE pickup_requests DROP COLUMN fee_policy CASCADE; ALTER TABLE inventory_batches DROP COLUMN fee_policy CASCADE; ALTER TABLE inventory_batches DROP COLUMN fee_percentage_snapshot CASCADE; ALTER TABLE platform_transactions DROP COLUMN fee_policy CASCADE;");
        var start = bootstrap.IndexOf("-- RETRACK_BUYER_FEES_V1_BEGIN", StringComparison.Ordinal);
        var end = bootstrap.IndexOf("-- RETRACK_BUYER_FEES_V1_END", StringComparison.Ordinal);
        var block = bootstrap[start..end];
        await Execute(block); await Execute(block);
        db.ChangeTracker.Clear();
        var oldPickup = await db.PickupRequests.SingleAsync(x => x.Id == pickup.Id);
        var oldBatch = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
        var oldTransaction = await db.PlatformTransactions.SingleAsync();
        Assert.Equal(PlatformFees.Legacy, oldPickup.FeePolicy); Assert.Equal(9500, oldPickup.NetAmount);
        Assert.Equal(PlatformFees.Legacy, oldBatch.FeePolicy); Assert.Equal(10500, oldBatch.NetAmount);
        Assert.Equal(PlatformFees.Legacy, oldTransaction.FeePolicy); Assert.Equal(500, oldTransaction.FeeAmount);
        oldBatch.FeePolicy = PlatformFees.BuyerSeparate; oldBatch.FeePercentageSnapshot = 5;
        await db.SaveChangesAsync(); await Execute(block); db.ChangeTracker.Clear();
        var repeated = await db.InventoryBatches.SingleAsync(x => x.Id == batch.Id);
        Assert.Equal(PlatformFees.BuyerSeparate, repeated.FeePolicy); Assert.Equal(5, repeated.FeePercentageSnapshot);
        Assert.Equal(10500, repeated.NetAmount);
    }
}
