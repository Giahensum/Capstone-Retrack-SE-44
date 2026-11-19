using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Factory;
using Retrack.API.Services.Liquidation;
using Retrack.API.Services.Money;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class MoneyCollectionUnitTests
{
    [Fact]
    public void Money_redirect_is_derived_from_return_url_and_carries_flow_and_source()
    {
        var opts = new PayOsOptions { ReturnUrl = "https://app.example/depot/reports?payos=return", CancelUrl = "https://app.example/depot/reports?payos=cancel" };
        var (ret, cancel) = opts.Redirects(PayOsRedirect.Money, "flow=PICKUP&source=abc");
        Assert.Equal("https://app.example/pay/return?flow=PICKUP&source=abc", ret);
        Assert.Equal(ret, cancel);
        // Giá trị cũ không đổi.
        Assert.Equal((opts.ReturnUrl, opts.CancelUrl), opts.Redirects(PayOsRedirect.Depot));
        Assert.Equal("https://app.example/factory?payos=return", opts.Redirects(PayOsRedirect.Factory).Return);
        Assert.Equal("https://x.test/p?flow=A", new PayOsOptions { MoneyReturnUrl = "https://x.test/p/" }.Redirects(PayOsRedirect.Money, "flow=A").Return);
    }
}

/// <summary>Một schema PostgreSQL dùng chung cho cả lớp test (mỗi test dùng nguồn/người dùng mới nên không đụng nhau).</summary>
public sealed class MoneyCollectionDb : IAsyncLifetime
{
    private readonly string schema = "moneycol_" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection admin = null!;
    public string Connection { get; private set; } = "";
    public bool Enabled { get; private set; }

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(source)) return;
        var parsed = new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test", parsed.Database);
        Assert.Equal("Retrack_TV2_test", new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")).Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        admin = new(source); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await create.ExecuteNonQueryAsync();
        Connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = true, MaxPoolSize = 40 }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        await using var setup = new NpgsqlConnection(Connection); await setup.OpenAsync();
        await using var command = new NpgsqlCommand(sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;", setup) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
        Enabled = true;
    }

    public async Task DisposeAsync()
    {
        if (admin == null) return;
        NpgsqlConnection.ClearAllPools();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await drop.ExecuteNonQueryAsync(); await admin.DisposeAsync();
    }
}

/// <summary>Bản giả của PayOS thu: ghi lại link đã tạo và trả trạng thái do test đặt.</summary>
public sealed class FakeCollectionPayOs : IPayOsClient
{
    public bool Configured = true;
    public readonly List<(long Code, long Amount, string Description, PayOsRedirect Redirect, string? Query)> Created = [];
    public readonly Dictionary<long, PayOsPayment> Remote = [];
    public bool IsConfigured => Configured;
    public Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, CancellationToken ct = default) =>
        CreatePaymentLinkAsync(orderCode, amount, description, PayOsRedirect.Depot, null, ct);
    public Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, PayOsRedirect redirect, string? redirectQuery, CancellationToken ct = default)
    {
        lock (Created) { Created.Add((orderCode, amount, description, redirect, redirectQuery)); Remote[orderCode] = new("PENDING", 0, null, null); }
        return Task.FromResult(new PayOsLink($"https://pay.test/{orderCode}", $"link-{orderCode}"));
    }
    public Task<PayOsPayment> GetPaymentAsync(long orderCode, CancellationToken ct = default) =>
        Task.FromResult(Remote.TryGetValue(orderCode, out var p) ? p : new("PENDING", 0, null, null));
    public bool VerifyWebhookData(JsonElement data, string signature) => signature == "good";
    public void Pay(long code, long amount, string reference = "FT1") => Remote[code] = new("PAID", amount, reference, DateTime.UtcNow);
}

public sealed class MoneyCollectionPostgresTests(MoneyCollectionDb fixture) : IClassFixture<MoneyCollectionDb>
{
    private const int Hold = 60;
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.Connection).Options);

    // ---------- dựng dữ liệu ----------
    private static User NewUser(string role, bool active = true) => new() { Id = Guid.NewGuid(), Role = role, IsActive = active, Email = Guid.NewGuid() + "@test.invalid", FullName = "Người dùng thử", Phone = "0900000000" };

    private async Task SetConfig(string key, string value)
    {
        await using var db = Open();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO system_configs (config_key, config_value) VALUES ({key}, {value}) ON CONFLICT (config_key) DO UPDATE SET config_value = EXCLUDED.config_value");
    }
    private async Task Mode(string mode) { await SetConfig("money.mode", mode); await SetConfig("payout.hold_minutes", Hold.ToString()); }

    private sealed record PickupWorld(Guid Id, User Seller, User Owner, Guid DepotId);
    private async Task<PickupWorld> NewPickup(string status = "AWAITING_PAYMENT", decimal gross = 500000)
    {
        await using var db = Open();
        var seller = NewUser("SELLER"); var owner = NewUser("DEPOT_OWNER");
        var depot = new Depot { Id = Guid.NewGuid(), Owner = owner, Name = "Kho thử", Address = "1 Nguyễn Văn Linh", VerificationStatus = "APPROVED" };
        var pickup = new PickupRequest { Seller = seller, TargetDepotId = depot.Id, Address = "A", Status = status, GrossAmount = gross, NetAmount = gross, PlatformFeePercentage = 0, FeePolicy = PlatformFees.BuyerSeparate };
        db.AddRange(seller, owner, depot); db.Add(pickup); await db.SaveChangesAsync();
        return new(pickup.Id, seller, owner, depot.Id);
    }

    private sealed record BatchWorld(Guid Id, User Factory, User DepotOwner, Guid DepotId, Guid FactoryId);
    private async Task<BatchWorld> NewBatch(string status = "SETTLEMENT_ACCEPTED", decimal goods = 1_000_000, decimal vat = 100_000)
    {
        await using var db = Open();
        var factoryOwner = NewUser("FACTORY"); var depotOwner = NewUser("DEPOT_OWNER");
        var depot = new Depot { Id = Guid.NewGuid(), Owner = depotOwner, Name = "Kho lô", Address = "2 Bạch Đằng", VerificationStatus = "APPROVED" };
        var factory = new Factory { Id = Guid.NewGuid(), Owner = factoryOwner, Name = "Nhà máy thử" };
        var batch = new InventoryBatch
        {
            Id = Guid.NewGuid(), Code = "LO-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), Depot = depot, TargetFactory = factory, MaterialType = "PET", DeclaredWeightKg = 100,
            Status = status, SettlementWorkflowVersion = 1, SettlementRevision = 1, SettlementProposedAt = DateTime.UtcNow, SettlementApprovedAt = DateTime.UtcNow,
            GrossAmount = goods, VatAmount = vat, InvoiceTotalAmount = goods + vat, NetAmount = goods + vat, VatTreatment = "TAXABLE", VatPercentage = 10,
            FeePolicy = PlatformFees.BuyerSeparate, FeePercentageSnapshot = 5, PlatformFeeAmount = 0
        };
        db.AddRange(factoryOwner, depotOwner, depot, factory); db.Add(batch); await db.SaveChangesAsync();
        return new(batch.Id, factoryOwner, depotOwner, depot.Id, factory.Id);
    }

    private sealed record LiqWorld(Guid AgreementId, Guid LotId, User Seller, User Buyer, Guid DepotId);
    private async Task<LiqWorld> NewAgreement(decimal amount = 10_000_000, decimal deposit = 1_000_000, bool signed = true)
    {
        await using var db = Open();
        var seller = NewUser("SELLER"); var buyer = NewUser("DEPOT_OWNER");
        var depot = new Depot { Id = Guid.NewGuid(), Owner = buyer, Name = "Kho thanh lý", Address = "3 Lê Lợi", VerificationStatus = "APPROVED" };
        var now = DateTime.UtcNow;
        var lot = new LiquidationLot
        {
            Id = Guid.NewGuid(), SellerId = seller.Id, Title = "Thanh lý thử", ScopeDetails = "Toàn bộ", CollectionTerms = "7 ngày", Address = "4 Hùng Vương", ContactPhone = "0900000001",
            SurveyStart = now.AddDays(-9), SurveyEnd = now.AddDays(-8), BiddingStart = now.AddDays(-5), OriginalEnd = now.AddDays(-1), BiddingEnd = now.AddDays(-1),
            CollectionDeadline = now.AddDays(7), StartingPrice = 1_000_000, BidIncrement = 100_000, DepositPercent = deposit > 0 ? 10 : 0, HighestPrice = amount, BidCount = 1,
            Status = "AWARDED", RequestHash = new string('a', 64)
        };
        db.AddRange(seller, buyer, depot, lot); await db.SaveChangesAsync();
        var bid = new LiquidationBid { Id = Guid.NewGuid(), LotId = lot.Id, DepotId = depot.Id, OwnerId = buyer.Id, Amount = amount };
        db.Add(bid); await db.SaveChangesAsync();
        var ag = new LiquidationAgreement { Id = Guid.NewGuid(), LotId = lot.Id, WinningBidId = bid.Id, DepotId = depot.Id, BuyerId = buyer.Id, Amount = amount, DepositAmount = deposit,
            SellerSignedAt = signed ? now : null, BuyerSignedAt = signed ? now : null };
        db.Add(ag); await db.SaveChangesAsync();
        return new(ag.Id, lot.Id, seller, buyer, depot.Id);
    }

    private static PayableLedgerService Ledger(AppDbContext db) => new(db, new MoneyConfigReader(db), TimeProvider.System);
    private static MoneyCollectionService Svc(AppDbContext db, FakeCollectionPayOs payos) =>
        new(db, payos, new MoneyConfigReader(db), Ledger(db), TimeProvider.System, NullLogger<MoneyCollectionService>.Instance);
    private static PlatformMoneyHooks Hooks(AppDbContext db) => new(db, new MoneyConfigReader(db), Ledger(db));

    private async Task<T> With<T>(Func<AppDbContext, Task<T>> act) { await using var db = Open(); return await act(db); }
    private Task<MoneyCollection> Collection(string flow, Guid source) => With(db => db.MoneyCollections.AsNoTracking().SingleAsync(c => c.Flow == flow && c.SourceId == source));
    private Task<T> Svc<T>(FakeCollectionPayOs p, Func<MoneyCollectionService, Task<T>> act) => With(db => act(Svc(db, p)));

    private static JsonElement Webhook(long code, long amount, string reference = "FT1", bool success = true, string signature = "good")
    {
        var json = $$"""{"code":"{{(success ? "00" : "01")}}","desc":"x","success":{{(success ? "true" : "false")}},"data":{"orderCode":{{code}},"amount":{{amount}},"reference":"{{reference}}","code":"{{(success ? "00" : "01")}}"},"signature":"{{signature}}"}""";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private async Task<long> LinkAsync(FakeCollectionPayOs p, string flow, Guid source, Guid payer)
    {
        var link = await Svc(p, s => s.CreateLinkAsync(payer, flow, source));
        return link.OrderCode;
    }

    private Task<List<string>> Audit(Guid collectionId) => With(db => db.AuditLogs.AsNoTracking().Where(a => a.EntityId == collectionId).Select(a => a.Action).ToListAsync());
    private Task<List<MoneyCollectionAnomaly>> Anomalies(Guid collectionId) => With(db => db.MoneyCollectionAnomalies.AsNoTracking().Where(a => a.CollectionId == collectionId).ToListAsync());
    private Task<List<PayableLine>> Lines(Guid source) => With(db => db.PayableLines.AsNoTracking().Where(l => l.SourceId == source).ToListAsync());

    // ---------- chế độ, cấu hình, quyền ----------

    [PostgresMoneyFact]
    public async Task Link_is_conflict_in_legacy_mode_and_creates_nothing()
    {
        await Mode("LEGACY"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(w.Owner.Id, "PICKUP", w.Id)));
        Assert.Contains("chưa được bật", ex.Message);
        Assert.Empty(p.Created);
        Assert.False(await With(db => db.MoneyCollections.AnyAsync(c => c.SourceId == w.Id)));
        var dto = await Svc(p, s => s.GetAsync(w.Owner.Id, "PICKUP", w.Id));
        Assert.Equal(("NONE", "LEGACY", false, "PAYER"), (dto.Status, dto.MoneyMode, dto.CanPay, dto.Role));
    }

    [PostgresMoneyFact]
    public async Task Link_is_conflict_when_payos_not_configured()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs { Configured = false };
        var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(w.Owner.Id, "PICKUP", w.Id)));
        Assert.Contains("PayOS chưa sẵn sàng", ex.Message);
    }

    [PostgresMoneyFact]
    public async Task Only_the_payer_can_link_or_sync_and_inactive_accounts_are_rejected()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var stranger = NewUser("DEPOT_OWNER"); var inactive = NewUser("SELLER", active: false);
        await With(async db => { db.AddRange(stranger, inactive); await db.SaveChangesAsync(); return 0; });
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.CreateLinkAsync(stranger.Id, "PICKUP", w.Id)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.CreateLinkAsync(w.Seller.Id, "PICKUP", w.Id))); // người nhận không được tạo link
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.SyncAsync(stranger.Id, "PICKUP", w.Id)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.GetAsync(stranger.Id, "PICKUP", w.Id)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.GetAsync(inactive.Id, "PICKUP", w.Id)));
        // Chủ kho bị khóa tài khoản không tạo được link.
        await With(async db => { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET is_active = false WHERE id = {w.Owner.Id}"); return 0; });
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.CreateLinkAsync(w.Owner.Id, "PICKUP", w.Id)));
        Assert.Empty(p.Created);
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(p, s => s.GetAsync(w.Seller.Id, "NOPE", w.Id)));
    }

    [PostgresMoneyFact]
    public async Task Payee_can_read_status_but_never_sees_the_checkout_url()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        var asPayer = await Svc(p, s => s.GetAsync(w.Owner.Id, "PICKUP", w.Id));
        var asPayee = await Svc(p, s => s.GetAsync(w.Seller.Id, "PICKUP", w.Id));
        Assert.NotNull(asPayer.CheckoutUrl); Assert.True(asPayer.CanPay); Assert.Equal("PAYER", asPayer.Role);
        Assert.Null(asPayee.CheckoutUrl); Assert.Null(asPayee.OrderCode); Assert.False(asPayee.CanPay); Assert.Equal("PAYEE", asPayee.Role);
        Assert.Equal(500000, asPayee.Amount);
    }

    // ---------- link ----------

    [PostgresMoneyFact]
    public async Task Link_creates_collection_reuses_pending_link_and_keeps_description_short()
    {
        await Mode("PLATFORM"); var w = await NewPickup(gross: 500000); var p = new FakeCollectionPayOs();
        var first = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        var second = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        Assert.Equal(first, second); Assert.Single(p.Created);
        var created = p.Created[0];
        Assert.Equal(500000, created.Amount); Assert.True(created.Description.Length <= 25);
        Assert.Equal(PayOsRedirect.Money, created.Redirect); Assert.Equal($"flow=PICKUP&source={w.Id}", created.Query);
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal(("PENDING", w.Owner.Id, w.Seller.Id, first), (c.Status, c.PayerId, c.PayeeId, c.PayosOrderCode ?? 0));
        Assert.Equal($"https://pay.test/{first}", c.PayosCheckoutUrl);
        Assert.Equal(1, await With(db => db.MoneyCollectionOrders.CountAsync(o => o.CollectionId == c.Id)));
    }

    [PostgresMoneyFact]
    public async Task Link_regenerates_when_old_one_expired_and_money_on_the_old_link_still_settles()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var old = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        p.Remote[old] = new("EXPIRED", 0, null, null);
        var fresh = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        Assert.NotEqual(old, fresh); Assert.Equal(2, p.Created.Count);
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal(fresh, c.PayosOrderCode);
        Assert.Equal(2, await With(db => db.MoneyCollectionOrders.CountAsync(o => o.CollectionId == c.Id)));
        // Tiền về muộn trên link cũ khi khoản thu còn PENDING: vẫn là tiền của khoản thu này.
        Assert.Equal(MoneyWebhookResult.Handled, await Svc(p, s => s.HandleWebhookAsync(Webhook(old, 500000))));
        c = await Collection("PICKUP", w.Id);
        Assert.Equal(("PAID", old), (c.Status, c.PaidOrderCode ?? 0));
    }

    [PostgresMoneyFact]
    public async Task Link_is_refused_when_source_is_not_payable_or_already_paid()
    {
        await Mode("PLATFORM"); var p = new FakeCollectionPayOs();
        var wrong = await NewPickup(status: "PAYMENT_SENT");
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(wrong.Owner.Id, "PICKUP", wrong.Id)));
        var fractional = await NewPickup(gross: 1000.5m);
        var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(fractional.Owner.Id, "PICKUP", fractional.Id)));
        Assert.Contains("nguyên VND", ex.Message);
        var w = await NewPickup(); var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000)));
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(w.Owner.Id, "PICKUP", w.Id)));
        Assert.Empty(p.Created.Where(c => c.Amount == 1000));
    }

    [PostgresMoneyFact]
    public async Task Concurrent_link_requests_yield_one_collection_and_consistent_links()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var codes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => LinkAsync(p, "PICKUP", w.Id, w.Owner.Id))));
        Assert.Equal(1, await With(db => db.MoneyCollections.CountAsync(c => c.SourceId == w.Id)));
        var c = await Collection("PICKUP", w.Id);
        // Mọi mã đơn đã cấp đều truy được về khoản thu, kể cả link bị bỏ rơi.
        Assert.Equal(p.Created.Count, await With(db => db.MoneyCollectionOrders.CountAsync(o => o.CollectionId == c.Id)));
        Assert.Contains(c.PayosOrderCode!.Value, codes);
    }

    // ---------- ghi nhận tiền về ----------

    [PostgresMoneyFact]
    public async Task Webhook_exact_amount_marks_paid_moves_pickup_and_is_idempotent()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        Assert.Equal(MoneyWebhookResult.Handled, await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000))));
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal(("PAID", 500000m, code), (c.Status, c.ReceivedAmount!.Value, c.PaidOrderCode!.Value)); Assert.NotNull(c.PaidAt);
        var pickup = await With(db => db.PickupRequests.AsNoTracking().SingleAsync(x => x.Id == w.Id));
        Assert.Equal("PAYMENT_SENT", pickup.Status); Assert.Null(pickup.PaymentProofUrl);
        Assert.False(await With(db => db.PlatformTransactions.AnyAsync(t => t.SourceId == w.Id))); // hoa hồng thay phí
        Assert.Equal(1, await With(db => db.Notifications.CountAsync(n => n.UserId == w.Seller.Id && n.PickupRequestId == w.Id)));
        // Gửi lại / đồng bộ muộn: không đổi gì.
        var paidAt = c.PaidAt;
        Assert.Equal(MoneyWebhookResult.Handled, await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000))));
        p.Pay(code, 500000);
        await Svc(p, s => s.SyncAsync(w.Owner.Id, "PICKUP", w.Id));
        Assert.True(await Svc(p, s => s.ReconcileAsync(c.Id)) == false);
        c = await Collection("PICKUP", w.Id);
        Assert.Equal(paidAt, c.PaidAt);
        Assert.Equal(["COLLECTION_LINK_CREATED", "COLLECTION_PAID"], (await Audit(c.Id)).OrderBy(x => x).ToList());
        Assert.Empty(await Anomalies(c.Id));
        Assert.Equal(1, await With(db => db.Notifications.CountAsync(n => n.UserId == w.Seller.Id && n.PickupRequestId == w.Id)));
    }

    [PostgresMoneyFact]
    public async Task Webhook_signature_and_unknown_order_and_failed_payment_are_handled_safely()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        Assert.Equal(MoneyWebhookResult.Invalid, await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000, signature: "bad"))));
        Assert.Equal(MoneyWebhookResult.NotMine, await Svc(p, s => s.HandleWebhookAsync(Webhook(123, 2000))));
        Assert.Equal(MoneyWebhookResult.Handled, await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000, success: false))));
        Assert.Equal("PENDING", (await Collection("PICKUP", w.Id)).Status);
        Assert.Equal("AWAITING_PAYMENT", (await With(db => db.PickupRequests.AsNoTracking().SingleAsync(x => x.Id == w.Id))).Status);
    }

    [PostgresMoneyFact]
    public async Task Underpaid_stays_pending_and_records_one_mismatch_even_on_replay()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 300000, "UNDER1")));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 300000, "UNDER1")));
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal("PENDING", c.Status); Assert.Null(c.PaidAt);
        var anomalies = await Anomalies(c.Id);
        var a = Assert.Single(anomalies);
        Assert.Equal((CollectionAnomalyKinds.Underpaid, 300000m, 500000m, 300000m, "OPEN"), (a.Kind, a.ReceivedAmount, a.ExpectedAmount, a.RefundAmount, a.Status));
        Assert.Equal("AWAITING_PAYMENT", (await With(db => db.PickupRequests.AsNoTracking().SingleAsync(x => x.Id == w.Id))).Status);
        Assert.Contains("COLLECTION_UNDERPAID", await Audit(c.Id));
        Assert.DoesNotContain("COLLECTION_PAID", await Audit(c.Id));
        // Remote báo UNDERPAID cho đồng bộ: cũng không thành PAID.
        p.Remote[code] = new("UNDERPAID", 300000, "UNDER1", null);
        Assert.Equal("PENDING", (await Svc(p, s => s.SyncAsync(w.Owner.Id, "PICKUP", w.Id))).Status);
        Assert.Single(await Anomalies(c.Id));
        Assert.True((await Svc(p, s => s.GetAsync(w.Owner.Id, "PICKUP", w.Id))).HasOpenAnomaly);
    }

    [PostgresMoneyFact]
    public async Task Overpaid_is_paid_and_excess_is_flagged_for_manual_refund_with_admin_notice()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var admin = NewUser("ADMIN"); await With(async db => { db.Add(admin); await db.SaveChangesAsync(); return 0; });
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 650000, "OVER1")));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 650000, "OVER1")));
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal(("PAID", 650000m), (c.Status, c.ReceivedAmount!.Value));
        Assert.Equal("PAYMENT_SENT", (await With(db => db.PickupRequests.AsNoTracking().SingleAsync(x => x.Id == w.Id))).Status);
        var a = Assert.Single(await Anomalies(c.Id));
        Assert.Equal((CollectionAnomalyKinds.Overpaid, 150000m), (a.Kind, a.RefundAmount));
        Assert.Equal(1, await With(db => db.Notifications.CountAsync(n => n.UserId == admin.Id && n.TargetId == c.Id)));
    }

    [PostgresMoneyFact]
    public async Task Late_money_on_cancelled_collection_is_recorded_for_refund_and_changes_nothing()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        Assert.True(await Svc(p, s => s.CancelUnpaidAsync("PICKUP", w.Id, "Đơn bị hủy", null)));
        Assert.False(await Svc(p, s => s.CancelUnpaidAsync("PICKUP", w.Id, "Đơn bị hủy", null)));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000, "LATE1")));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000, "LATE1")));
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal("CANCELLED", c.Status);
        var a = Assert.Single(await Anomalies(c.Id));
        Assert.Equal((CollectionAnomalyKinds.LatePayment, 500000m), (a.Kind, a.RefundAmount));
        Assert.Equal("AWAITING_PAYMENT", (await With(db => db.PickupRequests.AsNoTracking().SingleAsync(x => x.Id == w.Id))).Status);
    }

    [PostgresMoneyFact]
    public async Task Second_payment_on_another_link_after_paid_is_a_duplicate_to_refund()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var first = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        p.Remote[first] = new("CANCELLED", 0, null, null);
        var second = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        await Svc(p, s => s.HandleWebhookAsync(Webhook(second, 500000, "PAY2")));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(first, 500000, "PAY1")));   // trả nhầm link cũ
        await Svc(p, s => s.HandleWebhookAsync(Webhook(second, 500000, "PAY2")));  // gửi lại: no-op
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal(("PAID", second), (c.Status, c.PaidOrderCode!.Value));
        var a = Assert.Single(await Anomalies(c.Id));
        Assert.Equal((CollectionAnomalyKinds.DuplicatePayment, 500000m), (a.Kind, a.RefundAmount));
    }

    [PostgresMoneyFact]
    public async Task Concurrent_webhooks_and_syncs_produce_exactly_one_effect()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        p.Pay(code, 500000);
        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
        {
            if (i % 2 == 0) await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000)));
            else await Svc(p, s => s.SyncAsync(w.Owner.Id, "PICKUP", w.Id));
        }));
        await Task.WhenAll(tasks);
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal("PAID", c.Status);
        Assert.Equal(1, (await Audit(c.Id)).Count(a => a == "COLLECTION_PAID"));
        Assert.Equal(1, await With(db => db.Notifications.CountAsync(n => n.UserId == w.Seller.Id && n.PickupRequestId == w.Id)));
        Assert.Empty(await Anomalies(c.Id));
    }

    [PostgresMoneyFact]
    public async Task Sync_uses_only_what_payos_reports_and_reconcile_settles_pending_collections()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        Assert.Equal("PENDING", (await Svc(p, s => s.SyncAsync(w.Owner.Id, "PICKUP", w.Id))).Status);
        var c = await Collection("PICKUP", w.Id);
        Assert.Contains(c.Id, await Svc(p, s => s.PendingCollectionIdsAsync(200)));
        p.Pay(code, 500000, "FTR");
        Assert.True(await Svc(p, s => s.ReconcileAsync(c.Id)));
        Assert.DoesNotContain(c.Id, await Svc(p, s => s.PendingCollectionIdsAsync(200)));
        var dto = await Svc(p, s => s.SyncAsync(w.Owner.Id, "PICKUP", w.Id));
        Assert.Equal(("PAID", 500000m, false), (dto.Status, dto.ReceivedAmount!.Value, dto.CanPay));
        // Chưa từng tạo link thì đồng bộ báo lỗi rõ ràng.
        var other = await NewPickup();
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.SyncAsync(other.Owner.Id, "PICKUP", other.Id)));
    }

    // ---------- từng dòng tiền ----------

    [PostgresMoneyFact]
    public async Task Pickup_flow_end_to_end_creates_one_sale_line_when_seller_completes()
    {
        await Mode("PLATFORM"); await SetConfig("commission.default.PICKUP", "3");
        var w = await NewPickup(gross: 500000); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        // Luồng chuyển khoản tay bị chặn ở PLATFORM.
        await using (var db = Open())
        {
            var legacy = new DepotPaymentService(new DepotOwnerRepository(db), new DepotPaymentRepository(db), new DepotUnitOfWork(db), Hooks(db));
            await Assert.ThrowsAsync<DepotConflictException>(() => legacy.MarkSentAsync(w.Id, w.Owner.Id, "https://proof.test/a.png"));
        }
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000)));
        Assert.Empty(await Lines(w.Id)); // chưa DONE thì chưa có dòng sổ cái
        var before = DateTime.UtcNow;
        for (var i = 0; i < 2; i++)
            await using (var db = Open())
                Assert.Equal("DONE", (await new PickupService(new PickupRequestRepository(db), db, null!, Hooks(db)).MarkDoneAsync(w.Id, w.Seller.Id)).Status);
        var line = Assert.Single(await Lines(w.Id));
        Assert.Equal((PayableKinds.Sale, w.Seller.Id, "PICKUP", 500000m, 3m, 15000m, PayableStates.Held),
            (line.Kind, line.PayeeId, line.Flow, line.Gross, line.CommissionRate, line.Commission, line.State));
        Assert.Equal((await Collection("PICKUP", w.Id)).Id, line.CollectionId);
        Assert.InRange((line.AvailableAt - before).TotalMinutes, Hold - 1, Hold + 1);
    }

    [PostgresMoneyFact]
    public async Task Legacy_pickup_flow_is_unchanged_no_collections_no_ledger()
    {
        await Mode("LEGACY"); var w = await NewPickup(gross: 500000);
        await using (var db = Open())
        {
            var payments = new DepotPaymentService(new DepotOwnerRepository(db), new DepotPaymentRepository(db), new DepotUnitOfWork(db), Hooks(db));
            Assert.Equal("PAYMENT_SENT", (await payments.MarkSentAsync(w.Id, w.Owner.Id, "https://proof.test/a.png")).Status);
        }
        await using (var db = Open())
            Assert.Equal("DONE", (await new PickupService(new PickupRequestRepository(db), db, null!, Hooks(db)).MarkDoneAsync(w.Id, w.Seller.Id)).Status);
        Assert.Empty(await Lines(w.Id));
        Assert.False(await With(db => db.MoneyCollections.AnyAsync(c => c.SourceId == w.Id)));
        Assert.True(await With(db => db.PlatformTransactions.AnyAsync(t => t.SourceId == w.Id)));
    }

    [PostgresMoneyFact]
    public async Task Batch_flow_pays_batch_and_splits_gross_commission_and_vat_correctly()
    {
        await Mode("PLATFORM"); await SetConfig("commission.default.BATCH", "2");
        var w = await NewBatch(goods: 1_000_000, vat: 100_000); var p = new FakeCollectionPayOs();
        var notReady = await NewBatch(status: "SETTLEMENT_PENDING");
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(notReady.Factory.Id, "BATCH", notReady.Id)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.CreateLinkAsync(w.DepotOwner.Id, "BATCH", w.Id))); // chủ kho không phải người trả
        var code = await LinkAsync(p, "BATCH", w.Id, w.Factory.Id);
        Assert.Equal(1_100_000, p.Created.Single().Amount);
        // Chứng từ chuyển khoản tay bị chặn ở PLATFORM.
        await using (var db = Open())
        {
            var result = await new FactoryOrderService(db).ReportPaymentAsync(w.Factory.Id, w.Id, new BatchPaymentProofRequest { PaymentReference = "R", PaymentProofUrl = "https://x.test/p.png" }, default);
            Assert.Equal(ServiceOutcome.Conflict, result.Outcome);
        }
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 1_100_000)));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 1_100_000)));
        var batch = await With(db => db.InventoryBatches.AsNoTracking().SingleAsync(b => b.Id == w.Id));
        Assert.Equal("PAID", batch.Status); Assert.NotNull(batch.SettledAt); Assert.NotNull(batch.DepotPaymentConfirmedAt); Assert.StartsWith("PAYOS-", batch.PaymentReference);
        Assert.Equal(1, await With(db => db.BatchWorkflowEvents.CountAsync(e => e.BatchId == w.Id && e.Action == "PAYMENT_CONFIRMED")));
        Assert.False(await With(db => db.PlatformTransactions.AnyAsync(t => t.SourceId == w.Id)));
        // gross = hàng + VAT (VAT chi hộ kho); hoa hồng = 2% × tiền hàng chưa VAT = 20.000 (không phải 2% × 1.100.000).
        var line = Assert.Single(await Lines(w.Id));
        Assert.Equal(("BATCH", w.DepotOwner.Id, 1_100_000m, 2m, 20_000m), (line.Flow, line.PayeeId, line.Gross, line.CommissionRate, line.Commission));
        // Hoàn sau khi đã thanh toán: khoản thu REFUNDING + dòng REFUND khớp hoa hồng, chạy lại không tạo thêm.
        var admin = NewUser("ADMIN"); await With(async db => { db.Add(admin); await db.SaveChangesAsync(); return 0; });
        Assert.True(await Svc(p, s => s.MarkRefundingAsync("BATCH", w.Id, "Trả hàng sau thanh toán", admin.Id)));
        Assert.False(await Svc(p, s => s.MarkRefundingAsync("BATCH", w.Id, "Trả hàng sau thanh toán", admin.Id)));
        Assert.Equal("REFUNDING", (await Collection("BATCH", w.Id)).Status);
        var all = await Lines(w.Id);
        var refund = Assert.Single(all, l => l.Kind == PayableKinds.Refund);
        Assert.Equal((1_100_000m, 20_000m), (refund.Gross, refund.Commission));
        Assert.Equal(1, await With(db => db.Notifications.CountAsync(n => n.UserId == admin.Id && n.TargetId == line.CollectionId)));
    }

    [PostgresMoneyFact]
    public async Task Liquidation_flow_deposit_then_balance_then_single_sale_line_on_collection()
    {
        await Mode("PLATFORM"); await SetConfig("commission.default.LIQUIDATION", "5");
        var unsigned = await NewAgreement(signed: false); var p = new FakeCollectionPayOs();
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(unsigned.Buyer.Id, "LIQUIDATION_DEPOSIT", unsigned.AgreementId)));
        var w = await NewAgreement(amount: 10_000_000, deposit: 1_000_000);
        // Phần còn lại chỉ trả được sau khi cọc đã xác nhận.
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(w.Buyer.Id, "LIQUIDATION_BALANCE", w.AgreementId)));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => Svc(p, s => s.CreateLinkAsync(w.Seller.Id, "LIQUIDATION_DEPOSIT", w.AgreementId)));
        var depositCode = await LinkAsync(p, "LIQUIDATION_DEPOSIT", w.AgreementId, w.Buyer.Id);
        Assert.Equal(1_000_000, p.Created.Single().Amount);
        // Báo chuyển tay bị chặn ở PLATFORM.
        await using (var db = Open())
            await Assert.ThrowsAsync<DepotConflictException>(() => LiquidationSvc(db).AgreementActionAsync(w.Buyer.Id, w.DepotId, w.AgreementId, "REPORT_DEPOSIT", "TAY"));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(depositCode, 1_000_000)));
        var ag = await With(db => db.LiquidationAgreements.AsNoTracking().SingleAsync(a => a.Id == w.AgreementId));
        Assert.NotNull(ag.DepositReportedAt); Assert.NotNull(ag.DepositConfirmedAt); Assert.Null(ag.PaymentConfirmedAt);
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(w.Buyer.Id, "LIQUIDATION_DEPOSIT", w.AgreementId))); // cọc đã ghi nhận

        var balanceCode = await LinkAsync(p, "LIQUIDATION_BALANCE", w.AgreementId, w.Buyer.Id);
        Assert.Equal(9_000_000, p.Created.Last().Amount);
        await using (var db = Open())
            await Assert.ThrowsAsync<DepotConflictException>(() => LiquidationSvc(db).AgreementActionAsync(w.Buyer.Id, w.DepotId, w.AgreementId, "REPORT_PAYMENT", "TAY"));
        await Svc(p, s => s.HandleWebhookAsync(Webhook(balanceCode, 9_000_000, "BAL1")));
        ag = await With(db => db.LiquidationAgreements.AsNoTracking().SingleAsync(a => a.Id == w.AgreementId));
        Assert.NotNull(ag.PaymentReportedAt); Assert.NotNull(ag.PaymentConfirmedAt); Assert.Null(ag.OwnerCollectedAt);
        Assert.Empty(await Lines(w.AgreementId));

        // Bàn giao (Seller) và thu gom (chủ kho) vẫn là xác nhận thủ công; thu gom sinh đúng MỘT dòng SALE.
        await using (var db = Open())
            await SellerSvc(db).ConfirmHandoverAsync(w.Seller.Id, w.AgreementId);
        for (var i = 0; i < 2; i++)
            await using (var db = Open())
                await LiquidationSvc(db).AgreementActionAsync(w.Buyer.Id, w.DepotId, w.AgreementId, "CONFIRM_COLLECTED");
        var line = Assert.Single(await Lines(w.AgreementId));
        var balance = await Collection("LIQUIDATION_BALANCE", w.AgreementId);
        Assert.Equal(("LIQUIDATION", w.Seller.Id, 10_000_000m, 5m, 500_000m, balance.Id), (line.Flow, line.PayeeId, line.Gross, line.CommissionRate, line.Commission, line.CollectionId));
    }

    [PostgresMoneyFact]
    public async Task Liquidation_without_deposit_goes_straight_to_balance_for_full_amount()
    {
        await Mode("PLATFORM"); var w = await NewAgreement(amount: 5_000_000, deposit: 0, signed: false); var p = new FakeCollectionPayOs();
        await Assert.ThrowsAsync<DepotConflictException>(() => Svc(p, s => s.CreateLinkAsync(w.Buyer.Id, "LIQUIDATION_DEPOSIT", w.AgreementId)));
        var code = await LinkAsync(p, "LIQUIDATION_BALANCE", w.AgreementId, w.Buyer.Id);
        Assert.Equal(5_000_000, p.Created.Single().Amount);
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 5_000_000)));
        var ag = await With(db => db.LiquidationAgreements.AsNoTracking().SingleAsync(a => a.Id == w.AgreementId));
        Assert.NotNull(ag.PaymentConfirmedAt); Assert.Null(ag.DepositConfirmedAt);
    }

    [PostgresMoneyFact]
    public async Task Legacy_liquidation_collection_step_creates_no_ledger_line()
    {
        await Mode("LEGACY"); var w = await NewAgreement(deposit: 0, signed: false);
        await With(async db => { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE liquidation_agreements SET payment_reported_at = NOW(), payment_confirmed_at = NOW() WHERE id = {w.AgreementId}"); return 0; });
        await using (var db = Open())
            await LiquidationSvc(db).AgreementActionAsync(w.Buyer.Id, w.DepotId, w.AgreementId, "CONFIRM_COLLECTED");
        Assert.NotNull((await With(db => db.LiquidationAgreements.AsNoTracking().SingleAsync(a => a.Id == w.AgreementId))).OwnerCollectedAt);
        Assert.Empty(await Lines(w.AgreementId));
    }

    [PostgresMoneyFact]
    public async Task Source_that_changed_state_before_payment_sends_money_to_manual_refund()
    {
        await Mode("PLATFORM"); var w = await NewPickup(); var p = new FakeCollectionPayOs();
        var code = await LinkAsync(p, "PICKUP", w.Id, w.Owner.Id);
        await With(async db => { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE pickup_requests SET status = 'CANCELLED' WHERE id = {w.Id}"); return 0; });
        await Svc(p, s => s.HandleWebhookAsync(Webhook(code, 500000, "MIS1")));
        var c = await Collection("PICKUP", w.Id);
        Assert.Equal("REFUNDING", c.Status);
        Assert.Equal(CollectionAnomalyKinds.SourceMismatch, Assert.Single(await Anomalies(c.Id)).Kind);
        Assert.Equal("CANCELLED", (await With(db => db.PickupRequests.AsNoTracking().SingleAsync(x => x.Id == w.Id))).Status);
    }

    // ---------- dựng dịch vụ thanh lý ----------
    private static LiquidationService LiquidationSvc(AppDbContext db) => new(new LiquidationRepository(db),
        new DepotService(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db)), new DepotUnitOfWork(db), new DepotBatchRepository(db), TimeProvider.System, Hooks(db));
    private static Retrack.API.Services.Seller.SellerLiquidationService SellerSvc(AppDbContext db) => new(new SellerLiquidationRepository(db), new DepotUnitOfWork(db), new DepotBatchRepository(db), TimeProvider.System);
}
