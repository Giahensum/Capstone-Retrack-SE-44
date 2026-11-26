using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

public sealed class DepotSubscriptionPostgresTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Value { get; set; } = new(2026, 1, 30, 20, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Value;
    }
    private sealed class PayOs : IPayOsClient
    {
        public bool IsConfigured => true;
        public bool ValidSignature { get; set; } = true;
        public bool Timeout { get; set; }
        public int CreateCalls { get; private set; }
        public PayOsPayment Payment { get; set; } = new("PENDING", 0, null, null, "0123456789abcdef0123456789abcdef");
        public Task<PayOsLink> CreatePaymentLinkAsync(long code, long amount, string description, CancellationToken ct = default)
        {
            CreateCalls++;
            if (Timeout) throw new PayOsException("Mô phỏng timeout sau khi provider tạo link.");
            return Task.FromResult(new PayOsLink("https://pay.payos.vn/web/0123456789abcdef0123456789abcdef", "0123456789abcdef0123456789abcdef"));
        }
        public Task<PayOsPayment> GetPaymentAsync(long code, CancellationToken ct = default) => Task.FromResult(Payment);
        public bool VerifyWebhookData(JsonElement data, string signature) => ValidSignature;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string schema = "subscription_" + Guid.NewGuid().ToString("N");
        private string connection = "";
        private string baseConnection = "";
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Admin { get; } = Guid.NewGuid();
        public Guid Depot { get; } = Guid.NewGuid();
        public Clock Clock { get; } = new();
        public PayOs PayOs { get; } = new();
        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        public DepotSubscriptionService Service(AppDbContext db) => new(new DepotSubscriptionRepository(db), PayOs, Clock);
        public async Task InitializeAsync()
        {
            baseConnection = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")!;
            var other = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION");
            if (new NpgsqlConnectionStringBuilder(baseConnection).Database != "Retrack_TV2_test" || string.IsNullOrWhiteSpace(other) || new NpgsqlConnectionStringBuilder(other).Database != "Retrack_TV2_test")
                throw new InvalidOperationException("Chỉ chạy trên Retrack_TV2_test với đủ hai biến kiểm thử.");
            await using var admin = new NpgsqlConnection(baseConnection); await admin.OpenAsync();
            await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
            connection = new NpgsqlConnectionStringBuilder(baseConnection) { SearchPath = schema, Pooling = false }.ConnectionString;
            await using var db = Db();
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            await db.Database.ExecuteSqlRawAsync("CREATE SEQUENCE depot_subscription_order_code_seq START 8000000000000000");
            db.Users.AddRange(new User { Id = Owner, Role = "DEPOT_OWNER", Email = "owner@test.local", IsActive = true }, new User { Id = Admin, Role = "ADMIN", Email = "admin@test.local", IsActive = true });
            db.Depots.Add(new Depot { Id = Depot, OwnerId = Owner, Name = "Kho kiểm thử", VerificationStatus = "APPROVED" });
            db.SystemConfigs.AddRange(new SystemConfig { ConfigKey = "BUSINESS_MODEL", ConfigValue = "DEPOT_SUBSCRIPTION" }, new SystemConfig { ConfigKey = "SUBSCRIPTION_MONTHLY_PRICE", ConfigValue = "199000" });
            await db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync()
        {
            if (string.IsNullOrEmpty(baseConnection)) return;
            await using var admin = new NpgsqlConnection(baseConnection); await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE", admin); await cmd.ExecuteNonQueryAsync();
        }
    }
    private static async Task<Fixture> Setup() { var f = new Fixture(); await f.InitializeAsync(); return f; }

    [PostgresPickupFact]
    public async Task TrialIsOncePerDepotAndConcurrentStartsDoNotExtendIt()
    {
        await using var f = await Setup();
        async Task Start() { await using var db = f.Db(); await f.Service(db).StartTrialAsync(f.Owner, f.Depot); }
        await Task.WhenAll(Start(), Start());
        f.Clock.Value = f.Clock.Value.AddDays(20);
        await Start();
        await using var check = f.Db();
        var s = await check.DepotSubscriptions.SingleAsync();
        Assert.Equal(new DateTime(2026, 2, 13, 20, 0, 0, DateTimeKind.Utc), s.TrialEndsAt);
        await Assert.ThrowsAsync<DepotSubscriptionRequiredException>(() => f.Service(check).RequireAccessAsync(f.Depot));
        Assert.Single(await check.AuditLogs.Where(x => x.Action == "SUBSCRIPTION_TRIAL_STARTED").ToListAsync());
    }
    [PostgresPickupFact]
    public async Task ConcurrentOrdersHaveOnePendingSnapshotAndPriceChangesOnlyAffectNextOrder()
    {
        await using var f = await Setup();
        async Task<SubscriptionOrderDto> Buy() { await using var db = f.Db(); return await f.Service(db).CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid()); }
        var orders = await Task.WhenAll(Buy(), Buy());
        Assert.Equal(orders[0].Id, orders[1].Id);
        await using var check = f.Db(); var service = f.Service(check);
        await service.SetPriceAsync(f.Admin, 249000);
        var same = await Buy(); Assert.Equal(199000, same.Amount);
        await service.SubmitProofAsync(f.Owner, f.Depot, same.Id, new("https://example.test/proof.png", "CHUYEN-GOI"));
        await service.ApproveAsync(f.Admin, same.Id, "NHAN-DU");
        var next = await Buy(); Assert.Equal(249000, next.Amount);
    }
    [PostgresPickupFact]
    public async Task ManualApprovalRetriesAddOneVietnamCalendarMonthAfterTrial()
    {
        await using var f = await Setup(); await using var db = f.Db(); var service = f.Service(db);
        await service.StartTrialAsync(f.Owner, f.Depot);
        var o = await service.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid());
        await service.SubmitProofAsync(f.Owner, f.Depot, o.Id, new("https://example.test/proof.png", "CK001"));
        async Task Approve() { await using var other = f.Db(); await f.Service(other).ApproveAsync(f.Admin, o.Id, "BANK001"); }
        await Task.WhenAll(Approve(), Approve());
        await using var check = f.Db(); var order = await check.DepotSubscriptionOrders.SingleAsync(); var sub = await check.DepotSubscriptions.SingleAsync();
        Assert.Equal("PAID", order.Status); Assert.Equal(new DateTime(2026, 3, 13, 20, 0, 0, DateTimeKind.Utc), sub.PaidUntil);
        Assert.Equal(sub.PaidUntil, order.PeriodEnd);
        Assert.Single(await check.AuditLogs.Where(x => x.Action == "SUBSCRIPTION_ACTIVATED").ToListAsync());
    }
    [PostgresPickupFact]
    public async Task SignedWebhookAndSyncRaceActivateOnlyOnceAndWrongAmountsNeverActivate()
    {
        await using var f = await Setup(); await using var db = f.Db(); var s = f.Service(db);
        var o = await s.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid()); var link = await s.CreateLinkAsync(f.Owner, f.Depot, o.Id);
        JsonElement Body(long amount) => JsonSerializer.SerializeToElement(new { success = true, signature = "test-signature", data = new { code = "00", orderCode = link.OrderCode, amount } });
        await s.WebhookAsync(Body(100)); Assert.Null((await s.GetAsync(f.Owner, f.Depot)).PaidUntil);
        f.PayOs.ValidSignature = false; Assert.False(await s.WebhookAsync(Body(199000)));
        f.PayOs.ValidSignature = true; f.PayOs.Payment = new("PAID", 199000, "BANK001", null);
        async Task Webhook() { await using var other = f.Db(); Assert.True(await f.Service(other).WebhookAsync(Body(199000))); }
        async Task Sync() { await using var other = f.Db(); await f.Service(other).SyncAsync(f.Owner, f.Depot, o.Id); }
        await Task.WhenAll(Webhook(), Sync(), Webhook());
        await using var check = f.Db(); Assert.Single(await check.AuditLogs.Where(x => x.Action == "SUBSCRIPTION_ACTIVATED").ToListAsync());
        Assert.Equal(new DateTime(2026, 2, 27, 20, 0, 0, DateTimeKind.Utc), (await check.DepotSubscriptions.SingleAsync()).PaidUntil);
    }
    [PostgresPickupFact]
    public async Task TimeoutRecoveryKeepsSameOrderCodeAndDoesNotCreateSecondLink()
    {
        await using var f = await Setup(); await using var db = f.Db(); var s = f.Service(db);
        var o = await s.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid());
        f.PayOs.Timeout = true;
        await Assert.ThrowsAsync<PayOsException>(() => s.CreateLinkAsync(f.Owner, f.Depot, o.Id));
        f.Clock.Value = f.Clock.Value.AddMinutes(1);
        var recovered = await s.CreateLinkAsync(f.Owner, f.Depot, o.Id);
        Assert.Contains("pay.payos.vn", recovered.CheckoutUrl); Assert.Equal(1, f.PayOs.CreateCalls);
        Assert.Null((await s.GetAsync(f.Owner, f.Depot)).PaidUntil);
    }
    [PostgresPickupFact]
    public async Task WrongOwnerInactiveAccountAndMissingProofCannotActivate()
    {
        await using var f = await Setup(); await using var db = f.Db(); var s = f.Service(db);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => s.GetAsync(f.Admin, f.Depot));
        var o = await s.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid());
        await Assert.ThrowsAsync<DepotConflictException>(() => s.ApproveAsync(f.Admin, o.Id, "NO-PROOF"));
        await Assert.ThrowsAsync<ArgumentException>(() => s.SetPriceAsync(f.Admin, 199000.5m));
        var owner = await db.Users.FindAsync(f.Owner); owner!.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => s.StartTrialAsync(f.Owner, f.Depot));
    }
    [PostgresPickupFact]
    public async Task FailedAuditRollsBackBothPaidOrderAndSubscriptionExtension()
    {
        await using var f = await Setup(); await using var db = f.Db(); var s = f.Service(db);
        var o = await s.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid()); await s.SubmitProofAsync(f.Owner, f.Depot, o.Id, new("https://example.test/p.png", "BANK"));
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_activation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.action = 'SUBSCRIPTION_ACTIVATED' THEN RAISE EXCEPTION 'fixture rollback'; END IF; RETURN NEW; END; $$; CREATE TRIGGER fixture_fail BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION fail_activation();");
        await Assert.ThrowsAsync<DbUpdateException>(() => s.ApproveAsync(f.Admin, o.Id, "BANK"));
        await using var check = f.Db(); Assert.Equal("PENDING", (await check.DepotSubscriptionOrders.SingleAsync()).Status); Assert.Empty(await check.DepotSubscriptions.ToListAsync());
    }
    [PostgresPickupFact]
    public async Task SubscriptionStopsNewCommissionsAndKeepsExistingMoneyCollectionsRecognized()
    {
        await using var f = await Setup(); await using var db = f.Db();
        db.SystemConfigs.Add(new() { ConfigKey = "money.mode", ConfigValue = "PLATFORM" });
        db.PayeeCommissions.Add(new() { UserId = f.Owner, Flow = "BATCH", Rate = 9 }); await db.SaveChangesAsync();
        var config = new MoneyConfigReader(db);
        Assert.False(await config.UsesPlatformAsync("BATCH", Guid.NewGuid()));
        Assert.Equal("0", await new DepotBatchRepository(db).FeeConfigurationAsync());
        Assert.Equal(0, await new PayableLedgerService(db, config, f.Clock).ResolveRateAsync(f.Owner, "BATCH"));
        var source = Guid.NewGuid();
        db.MoneyCollections.Add(new() { Id = Guid.NewGuid(), SourceId = source, SourceType = "INVENTORY_BATCH", Flow = "BATCH", PayerId = f.Admin, PayeeId = f.Owner, Amount = 100000, Status = "PENDING" }); await db.SaveChangesAsync();
        Assert.True(await config.UsesPlatformAsync("BATCH", source));
        await db.SystemConfigs.Where(x => x.ConfigKey == "money.mode").ExecuteUpdateAsync(x => x.SetProperty(c => c.ConfigValue, "LEGACY"));
        Assert.True(await config.UsesPlatformAsync("BATCH", source));
        Assert.False(await config.UsesPlatformAsync("BATCH", Guid.NewGuid()));
    }

    [PostgresPickupFact]
    public async Task AdminCanRequestProofCorrectionWithoutLosingAuditOrGrantingAccess()
    {
        await using var f = await Setup(); await using var db = f.Db(); var s = f.Service(db);
        var o = await s.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid());
        await s.SubmitProofAsync(f.Owner, f.Depot, o.Id, new("https://example.test/wrong.png", "CHUA-KHOP"));
        await s.RejectProofAsync(f.Admin, o.Id, "Ảnh mờ, cần mã giao dịch đúng");
        await s.RejectProofAsync(f.Admin, o.Id, "Ảnh mờ, cần mã giao dịch đúng");
        var page = await s.OrdersAsync(f.Owner, f.Depot, 1, 20);
        Assert.Null(page.Items[0].ProofUrl); Assert.Contains("Ảnh mờ", page.Items[0].ReviewNote);
        Assert.False((await s.GetAsync(f.Owner, f.Depot)).HasAccess);
        await s.SubmitProofAsync(f.Owner, f.Depot, o.Id, new("https://example.test/correct.png", "DUNG-MA"));
        await s.ApproveAsync(f.Admin, o.Id, "NGAN-HANG-DA-NHAN");
        Assert.True((await s.GetAsync(f.Owner, f.Depot)).HasAccess);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "SUBSCRIPTION_PROOF_REJECTED").ToListAsync());
    }
    [PostgresPickupFact]
    public async Task ExpiryIsScopedPerDepotAndDoesNotPreventCompletingAlreadyPurchasedBulkIntake()
    {
        await using var f = await Setup(); await using var db = f.Db(); var subscriptions = f.Service(db);
        var second = new Depot { OwnerId = f.Owner, Name = "Kho thứ hai", VerificationStatus = "APPROVED" }; db.Depots.Add(second); await db.SaveChangesAsync();
        await subscriptions.StartTrialAsync(f.Owner, f.Depot);
        await Assert.ThrowsAsync<DepotSubscriptionRequiredException>(() => subscriptions.RequireAccessAsync(second.Id));
        f.Clock.Value = f.Clock.Value.AddDays(15);
        var scope = new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db), subscriptions);
        await Assert.ThrowsAsync<DepotSubscriptionRequiredException>(() => scope.RequireTradingAsync(f.Owner, f.Depot));
        Assert.Equal(f.Depot, (await scope.GetProfileAsync(f.Owner, f.Depot)).Id);
        Assert.NotNull(await subscriptions.CreateOrderAsync(f.Owner, f.Depot, Guid.NewGuid()));
        var stock = new DepotStockService(new DepotStockRepository(db), new DepotBatchRepository(db), new DepotUnitOfWork(db), scope, subscriptions: subscriptions);
        var receipt = new CreateStockReceiptDto { OperationId = Guid.NewGuid(), Reference = "Nguồn mới", ReceivedAt = DateTime.UtcNow, PostImmediately = true,
            Lines = [new() { Name = "Sắt", MaterialType = "IRON", Quantity = 20 }] };
        await Assert.ThrowsAsync<DepotSubscriptionRequiredException>(() => stock.CreateAsync(f.Owner, f.Depot, receipt));
        var seller = new User { Role = "SELLER", Email = "seller-expiry@test.local" };
        var now = DateTime.UtcNow;
        var lot = new LiquidationLot { Seller = seller, Title = "Giao dịch đã mua trước", Status = "AWARDED", SurveyStart = now.AddDays(-3), SurveyEnd = now.AddDays(-2), BiddingStart = now.AddDays(-1), OriginalEnd = now.AddHours(-2), BiddingEnd = now.AddHours(-2), CollectionDeadline = now.AddDays(1) };
        var bid = new LiquidationBid { Lot = lot, OwnerId = f.Owner, DepotId = f.Depot, Amount = 1000000 };
        var agreement = new LiquidationAgreement { Lot = lot, WinningBid = bid, DepotId = f.Depot, BuyerId = f.Owner, Amount = 1000000, PaymentConfirmedAt = now, SellerHandedOverAt = now };
        db.LiquidationAgreements.Add(agreement); await db.SaveChangesAsync();
        receipt.OperationId = Guid.NewGuid(); receipt.BulkAgreementId = agreement.Id; receipt.SourceType = "BULK_PURCHASE"; receipt.ConfirmBulkReceived = true; receipt.PurchaseCost = 1000000;
        Assert.Equal("POSTED", (await stock.CreateAsync(f.Owner, f.Depot, receipt)).Status);
        Assert.NotNull((await db.LiquidationAgreements.SingleAsync()).OwnerCollectedAt);
    }
}
