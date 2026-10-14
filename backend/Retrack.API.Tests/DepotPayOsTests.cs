using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>Kiểm thử chữ ký PayOS và luồng thanh toán hóa đơn phí nền tảng (TV5 - phần PayOS của Depot Owner).</summary>
public sealed class DepotPayOsTests
{
    private const string Key = "test-checksum-key";
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Depot = Guid.NewGuid();

    // ---------- chữ ký (vector tính độc lập bằng HMAC-SHA256 ở Python) ----------
    [Fact]
    public void Create_request_signature_matches_known_vector()
    {
        var payload = PayOsClient.CreateRequestPayload(150000, "https://x.test/cancel", "RETRACK 09/2026", 1700000000123, "https://x.test/return");
        Assert.Equal("6e23126d11dd1863842fbfe635d18552f5db44476412cd0ef54dec6dcc591f1e", PayOsClient.Sign(Key, payload));
    }

    [Fact]
    public void Webhook_payload_sorts_keys_and_verifies_known_signature()
    {
        var client = NewClient();
        using var doc = JsonDocument.Parse("""{"reference":"FT123","orderCode":1700000000123,"desc":"success","code":"00","amount":150000}""");
        Assert.Equal("amount=150000&code=00&desc=success&orderCode=1700000000123&reference=FT123", PayOsClient.WebhookPayload(doc.RootElement));
        Assert.True(client.VerifyWebhookData(doc.RootElement, "6c56fa92168fb66e9c09b6e02e68ab5707dbeab6643d59d22612cfcf38392cde"));
        Assert.False(client.VerifyWebhookData(doc.RootElement, "0000"));
        Assert.False(client.VerifyWebhookData(doc.RootElement, ""));
    }

    [Fact]
    public void Webhook_verification_fails_when_not_configured()
    {
        var client = new PayOsClient(new HttpClient(), Options.Create(new PayOsOptions()));
        using var doc = JsonDocument.Parse("""{"amount":1}""");
        Assert.False(client.VerifyWebhookData(doc.RootElement, PayOsClient.Sign(Key, "amount=1")));
    }

    // ---------- webhook ----------
    [Fact]
    public async Task Webhook_marks_invoice_paid_once_and_is_idempotent()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123);
        Assert.True(await f.Service.HandleWebhookAsync(f.Webhook(1700000000123, 150000)));
        Assert.Equal("PAID", inv.Status); Assert.NotNull(inv.PaidAt);
        var paidAt = inv.PaidAt; f.Saves = 0;
        Assert.True(await f.Service.HandleWebhookAsync(f.Webhook(1700000000123, 150000)));
        Assert.Equal(paidAt, inv.PaidAt); Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Webhook_with_bad_signature_is_rejected_and_changes_nothing()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123);
        Assert.False(await f.Service.HandleWebhookAsync(f.Webhook(1700000000123, 150000, signature: "deadbeef")));
        Assert.Equal("PENDING", inv.Status);
    }

    [Fact]
    public async Task Webhook_with_wrong_amount_keeps_invoice_for_admin_reconcile()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123);
        Assert.True(await f.Service.HandleWebhookAsync(f.Webhook(1700000000123, 1000)));
        Assert.Equal("PENDING", inv.Status);
    }

    [Fact]
    public async Task Webhook_failure_or_unknown_order_is_acknowledged_without_change()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123);
        Assert.True(await f.Service.HandleWebhookAsync(f.Webhook(1700000000123, 150000, code: "01")));
        Assert.True(await f.Service.HandleWebhookAsync(f.Webhook(123, 2000))); // ping kiểm tra của PayOS
        Assert.Equal("PENDING", inv.Status);
    }

    // ---------- tạo link ----------
    [Fact]
    public async Task Create_link_stores_order_code_and_checkout_url()
    {
        var f = new Fixture(); var inv = f.Invoice();
        var link = await f.Service.CreateLinkAsync(Owner, Depot, inv.Id);
        Assert.StartsWith("https://pay.test/checkout?orderCode=", link.CheckoutUrl);
        Assert.Equal(DepotPayOsService.OrderCodeOf(inv), link.OrderCode); Assert.Equal(link.CheckoutUrl, inv.PaymentProofUrl); Assert.Equal("PENDING", inv.Status);
        Assert.Equal(150000, f.Payos.CreatedAmount); Assert.Equal("RETRACK 09/2026", f.Payos.CreatedDescription);
    }

    [Fact]
    public async Task Create_link_reuses_pending_link_without_creating_a_new_order()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123, checkout: "https://pay.test/old");
        f.Payos.Remote = new("PENDING", 0, null, null);
        var link = await f.Service.CreateLinkAsync(Owner, Depot, inv.Id);
        Assert.Equal("https://pay.test/old?orderCode=1700000000123", link.CheckoutUrl); Assert.Equal(0, f.Payos.Creates);
    }

    [Fact]
    public async Task Create_link_issues_new_order_when_old_link_expired()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123, checkout: "https://pay.test/old");
        f.Payos.Remote = new("EXPIRED", 0, null, null);
        var link = await f.Service.CreateLinkAsync(Owner, Depot, inv.Id);
        Assert.StartsWith("https://pay.test/checkout?orderCode=", link.CheckoutUrl); Assert.NotEqual(1700000000123, link.OrderCode);
    }

    [Fact]
    public async Task Create_link_rejects_other_payer_non_pending_and_unconfigured()
    {
        var f = new Fixture(); var other = f.Invoice(payer: Guid.NewGuid());
        await Assert.ThrowsAsync<DepotForbiddenException>(() => f.Service.CreateLinkAsync(Owner, Depot, other.Id));
        var paid = f.Invoice(status: "PAID");
        await Assert.ThrowsAsync<DepotConflictException>(() => f.Service.CreateLinkAsync(Owner, Depot, paid.Id));
        var fraction = f.Invoice(amount: 1000.5m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateLinkAsync(Owner, Depot, fraction.Id));
        f.Payos.Configured = false;
        var pending = f.Invoice();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CreateLinkAsync(Owner, Depot, pending.Id));
    }

    // ---------- đối soát chủ động ----------
    [Fact]
    public async Task Sync_marks_paid_when_payos_confirms_and_amount_matches()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123);
        f.Payos.Remote = new("PAID", 150000, "FT9", new DateTime(2026, 9, 5, 1, 0, 0, DateTimeKind.Utc));
        Assert.Equal("PAID", await f.Service.SyncAsync(Owner, Depot, inv.Id));
        Assert.Equal("PAID", inv.Status); Assert.Equal(new DateTime(2026, 9, 5, 1, 0, 0, DateTimeKind.Utc), inv.PaidAt);
    }

    [Fact]
    public async Task Sync_does_not_pay_when_amount_differs_or_still_pending()
    {
        var f = new Fixture(); var inv = f.Invoice(orderCode: 1700000000123);
        f.Payos.Remote = new("PENDING", 0, null, null);
        Assert.Equal("PENDING", await f.Service.SyncAsync(Owner, Depot, inv.Id));
        f.Payos.Remote = new("PAID", 1, null, null);
        await Assert.ThrowsAsync<DepotConflictException>(() => f.Service.SyncAsync(Owner, Depot, inv.Id));
        Assert.Equal("PENDING", inv.Status);
    }

    [Fact]
    public async Task Sync_requires_existing_payos_link_and_ownership()
    {
        var f = new Fixture();
        var noLink = f.Invoice();
        await Assert.ThrowsAsync<DepotConflictException>(() => f.Service.SyncAsync(Owner, Depot, noLink.Id));
        var other = f.Invoice(payer: Guid.NewGuid(), orderCode: 1700000000999);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => f.Service.SyncAsync(Owner, Depot, other.Id));
    }

    // ---------- fixture & fakes ----------
    private static PayOsClient NewClient() => new(new HttpClient(), Options.Create(new PayOsOptions
        { ClientId = "id", ApiKey = "key", ChecksumKey = Key, ReturnUrl = "https://x.test/return", CancelUrl = "https://x.test/cancel" }));

    private sealed class Fixture
    {
        private readonly List<PlatformInvoice> invoices = [];
        public int Saves;
        public FakePayOs Payos { get; } = new();
        public IDepotPayOsService Service { get; }

        public Fixture()
        {
            var repo = Fake<IDepotReportRepository>((m, a) => m switch
            {
                nameof(IDepotReportRepository.LockInvoiceAsync) => Task.FromResult(invoices.SingleOrDefault(i => i.Id == (Guid)a[0]!)),
                nameof(IDepotReportRepository.LockInvoiceByPayosOrderAsync) => Task.FromResult(invoices.SingleOrDefault(i => DepotPayOsService.OrderCodeOf(i) == (long)a[0]!)),
                _ => throw new NotSupportedException(m)
            });
            var work = Fake<IDepotUnitOfWork>((m, _) => m switch
            {
                nameof(IDepotUnitOfWork.BeginAsync) => Task.FromResult(Fake<IDepotTransaction>((t, _) => t switch
                {
                    nameof(IDepotTransaction.CommitAsync) => Task.CompletedTask,
                    nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                    _ => throw new NotSupportedException(t)
                })),
                nameof(IDepotUnitOfWork.SaveAsync) => Count(),
                _ => throw new NotSupportedException(m)
            });
            var scope = Fake<IDepotService>((m, _) => m == nameof(IDepotService.RequireOwnerAsync) ? Task.CompletedTask : throw new NotSupportedException(m));
            Service = new DepotPayOsService(work, repo, scope, Payos, NullLogger<DepotPayOsService>.Instance);
            Task Count() { Saves++; return Task.CompletedTask; }
        }

        public PlatformInvoice Invoice(Guid? payer = null, string status = "PENDING", decimal amount = 150000m, long? orderCode = null, string? checkout = null)
        {
            var invoice = new PlatformInvoice { PayerId = payer ?? Owner, PeriodYear = 2026, PeriodMonth = 9, TotalFeeAmount = amount, Status = status,
                PaymentProofUrl = orderCode == null ? null : DepotPayOsService.WithOrderCode(checkout ?? "https://pay.test/old", orderCode.Value) };
            invoices.Add(invoice);
            return invoice;
        }

        public JsonElement Webhook(long orderCode, long amount, string code = "00", string? signature = null)
        {
            var data = $$"""{"orderCode":{{orderCode}},"amount":{{amount}},"code":"{{code}}","desc":"success","reference":"FT123"}""";
            using var doc = JsonDocument.Parse(data);
            signature ??= PayOsClient.Sign(Key, PayOsClient.WebhookPayload(doc.RootElement));
            return JsonDocument.Parse($$"""{"code":"{{code}}","success":{{(code == "00" ? "true" : "false")}},"data":{{data}},"signature":"{{signature}}"}""").RootElement.Clone();
        }
    }

    private sealed class FakePayOs : IPayOsClient
    {
        public bool Configured = true; public int Creates; public long CreatedAmount; public string? CreatedDescription;
        public PayOsPayment Remote = new("PENDING", 0, null, null);
        public bool IsConfigured => Configured;
        public Task<PayOsLink> CreatePaymentLinkAsync(long orderCode, long amount, string description, CancellationToken ct = default)
        { Creates++; CreatedAmount = amount; CreatedDescription = description; return Task.FromResult(new PayOsLink("https://pay.test/checkout", "link-1")); }
        public Task<PayOsPayment> GetPaymentAsync(long orderCode, CancellationToken ct = default) => Task.FromResult(Remote);
        public bool VerifyWebhookData(JsonElement data, string signature) => NewClient().VerifyWebhookData(data, signature);
    }

    /// <summary>Tạo bản giả của interface bằng DispatchProxy, tránh phụ thuộc thư viện mock.</summary>
    private static T Fake<T>(Func<string, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, FakeProxy>();
        ((FakeProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class FakeProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!.Name, args ?? []);
    }
}
