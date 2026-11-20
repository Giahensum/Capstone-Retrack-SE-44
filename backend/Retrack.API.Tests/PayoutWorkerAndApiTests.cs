using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Retrack.API.Controllers.Money;
using Retrack.API.Data;
using Retrack.API.Middleware;
using Retrack.API.Models;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

public sealed class PayoutWorkerTests : PayoutTestBase
{
    private ServiceProvider Services(IPayoutProvider provider)
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton<TimeProvider>(Clock);
        s.AddDbContext<AppDbContext>(o => o.UseNpgsql(Connection));
        s.AddScoped<MoneyConfigReader>();
        s.AddScoped<IPayoutProviderResolver>(_ => new FixedResolver(provider));
        s.AddScoped<PayoutEngine>();
        return s.BuildServiceProvider();
    }

    private AutoPayoutWorker AutoWorker(ServiceProvider sp) =>
        new(sp.GetRequiredService<IServiceScopeFactory>(), Options.Create(new PayoutWorkerOptions()), Clock, NullLogger<AutoPayoutWorker>.Instance);

    private async Task<string?> Marker()
    {
        await using var db = Open();
        return await db.SystemConfigs.AsNoTracking().Where(c => c.ConfigKey == PayoutConfigKeys.AutoLastBoundary).Select(c => c.ConfigValue).FirstOrDefaultAsync();
    }

    [PostgresMoneyFact]
    public async Task AutoWorkerRunsAtScheduleSkipsDisabledPayeesAndPersistsMarker()
    {
        Clock.Set(new(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc)); // 10:00 giờ Việt Nam
        await SetConfig(MoneyConfigKeys.PayoutAutoEnabled, "true");
        var a = await NewPayee("Người A"); var b = await NewPayee("Người B");
        await AddLine(a, 1_000_000, 30_000, availableAt: new(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        await AddLine(b, 1_000_000, 30_000, availableAt: new(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        await using (var db = Open()) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO payee_payout_settings (user_id, auto_enabled) VALUES ({b}, false)");
        await using var sp = Services(Fake);
        var worker = AutoWorker(sp);
        var boundary = new DateTime(2026, 3, 9, 17, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, await worker.RunOnceAsync(default));
        await using (var db = Open())
        {
            var run = await db.PayoutRuns.AsNoTracking().SingleAsync();
            Assert.Equal(a, run.PayeeId);
            Assert.Null(run.CreatedBy); // do hệ thống tự chạy
            Assert.Equal(boundary, run.PeriodEnd);
            Assert.Equal(boundary.AddDays(-1), run.PeriodStart); // chưa có kỳ nào: lùi 1 ngày
        }
        Assert.All(await LinesOf(b), l => Assert.Equal("AVAILABLE", l.State)); // người tắt tự chi: dòng không mất
        Assert.Equal(1, Fake.PayoutCalls);
        Assert.Equal(boundary.ToString("o", CultureInfo.InvariantCulture), await Marker());

        Assert.Equal(0, await worker.RunOnceAsync(default)); // cùng mốc: không chạy lại
        Assert.Equal(0, await AutoWorker(sp).RunOnceAsync(default)); // khởi động lại: mốc bền vững
        Assert.Equal(1, Fake.PayoutCalls);

        Clock.Set(new(2026, 3, 11, 3, 0, 0, DateTimeKind.Utc)); // hôm sau
        Assert.Equal(0, await worker.RunOnceAsync(default));
        Assert.Equal(new DateTime(2026, 3, 10, 17, 0, 0, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture), await Marker());

        // Admin chạy thủ công cho người đã tắt tự chi: dòng cũ vẫn được chi
        await using var db2 = Open();
        var manual = (await Engine(db2).RunAsync([b], boundary, new DateTime(2026, 3, 10, 17, 0, 0, DateTimeKind.Utc), AdminUser)).Items.Single();
        Assert.Equal("CREATED", manual.Outcome);
        Assert.Equal(970_000, manual.Net);
    }

    [PostgresMoneyFact]
    public async Task AutoWorkerDoesNothingWhenDisabledOrBeforeSchedule()
    {
        Clock.Set(new(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc));
        var a = await NewPayee();
        await AddLine(a, 1_000_000, 30_000, availableAt: new(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        await using var sp = Services(Fake);
        Assert.Equal(0, await AutoWorker(sp).RunOnceAsync(default)); // payout.auto_enabled mặc định false
        Assert.Null(await Marker());
        Assert.Equal(0, await Count());
    }

    private async Task<long> Count()
    {
        await using var db = Open();
        return await db.PayoutRuns.LongCountAsync();
    }

    private async Task<Guid> InsertRun(Guid payee, string status, int n, DateTime? lastAttempt, DateTime created, string? providerRef = "REF", decimal net = 100_000)
    {
        await using var db = Open();
        var run = new PayoutRun
        {
            Code = $"PAY-TEST-{n:D4}", PayeeId = payee, PeriodStart = PStart.AddDays(-n * 2 - 2), PeriodEnd = PStart.AddDays(-n * 2 - 1), Gross = net, Net = net, Status = status, Provider = "SIMULATED",
            ProviderRef = providerRef == "REF" ? "REF-" + n : providerRef, LastAttemptAt = lastAttempt, CreatedAt = created,
            BankSnapshot = "{\"bankName\":\"Vietcombank\",\"bin\":\"970436\",\"account\":\"0123456789\",\"holder\":\"AN\"}",
        };
        db.PayoutRuns.Add(run); await db.SaveChangesAsync();
        var line = await AddLine(payee, net, 0, state: "IN_PAYOUT");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payable_lines SET payout_id = {run.Id} WHERE id = {line}");
        return run.Id;
    }

    [PostgresMoneyFact]
    public async Task ReconcileWorkerPollsOnlyOldProcessingAndUnknownRunsAndSubmitsNeverSentOnes()
    {
        var payee = await NewPayee();
        Fake.Status = new(PayoutStatus.Succeeded, null);
        var old = await InsertRun(payee, "PROCESSING", 1, T0.AddMinutes(-5), T0.AddMinutes(-6));
        var fresh = await InsertRun(payee, "PROCESSING", 2, T0.AddSeconds(-10), T0.AddMinutes(-6));
        var unknown = await InsertRun(payee, "UNKNOWN", 3, T0.AddMinutes(-2), T0.AddMinutes(-6), providerRef: null);
        var neverSent = await InsertRun(payee, "PROCESSING", 4, null, T0.AddMinutes(-2), providerRef: null);
        var other = await InsertRun(payee, "FAILED", 5, T0.AddMinutes(-9), T0.AddMinutes(-9));
        await using var sp = Services(Fake);
        var worker = new PayoutReconcileWorker(sp.GetRequiredService<IServiceScopeFactory>(), Options.Create(new PayoutWorkerOptions()), NullLogger<PayoutReconcileWorker>.Instance);
        Assert.Equal(3, await worker.RunOnceAsync(default));
        Assert.Equal("PAID", (await RunOf(old)).Status);
        Assert.Equal("PAID", (await RunOf(unknown)).Status);
        Assert.Equal("PROCESSING", (await RunOf(fresh)).Status);   // còn mới: chưa động tới
        Assert.Equal("FAILED", (await RunOf(other)).Status);       // trạng thái khác: không đụng
        var sent = await RunOf(neverSent);
        Assert.Equal("PROCESSING", sent.Status);
        Assert.NotNull(sent.LastAttemptAt); // đã được gửi lần đầu (khôi phục sau sập giữa commit và gửi)
        Assert.Equal(1, Fake.PayoutCalls);
        Assert.Equal(sent.Code, Fake.Calls.Single().IdempotencyKey);
    }

    [PostgresMoneyFact]
    public async Task ReconcileBatchIsBoundedAndOldestFirst()
    {
        var payee = await NewPayee();
        var ids = new List<Guid>();
        for (var i = 1; i <= 4; i++) ids.Add(await InsertRun(payee, "PROCESSING", i, T0.AddMinutes(-10 + i), T0.AddMinutes(-20)));
        await using var db = Open();
        var due = await Engine(db).DueForReconcileAsync(TimeSpan.FromMinutes(1), 2);
        Assert.Equal([ids[0], ids[1]], due);
        Assert.Empty(await Engine(db).DueForReconcileAsync(TimeSpan.FromMinutes(60), 50));
    }
}

public sealed partial class PayoutApiTests : PayoutTestBase
{
    private WebApplication app = null!;
    private HttpClient client = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Ready) return;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(Connection));
        builder.Services.AddScoped<MoneyConfigReader>();
        builder.Services.AddScoped<IPayoutProviderResolver>(_ => new FixedResolver(Fake));
        builder.Services.AddScoped<PayoutEngine>();
        builder.Services.AddScoped<RefundEngine>();
        builder.Services.AddScoped<MoneyAdminService>();
        builder.Services.AddScoped<MyMoneyService>();
        builder.Services.AddSingleton(Options.Create(new PayOsPayoutOptions()));
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminMoneyController).Assembly);
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuth>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    public override async Task DisposeAsync()
    {
        client?.Dispose();
        if (app != null) await app.DisposeAsync();
        await base.DisposeAsync();
    }

    private sealed class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Fixture-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Fixture-User"].ToString()), new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private async Task<(HttpStatusCode Status, JsonElement Body, string Raw)> Call(string method, string path, string? role, Guid? user, object? body = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (role != null) { request.Headers.Add("X-Fixture-Role", role); request.Headers.Add("X-Fixture-User", (user ?? Guid.NewGuid()).ToString()); }
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        JsonElement el = default;
        if (raw.StartsWith('{')) el = JsonDocument.Parse(raw).RootElement.Clone();
        return (response.StatusCode, el, raw);
    }

    private Task<(HttpStatusCode Status, JsonElement Body, string Raw)> AsAdmin(string method, string path, object? body = null) => Call(method, path, "ADMIN", AdminUser, body);

    [PostgresMoneyFact]
    public async Task AdminEndpointsRejectAnonymousAndNonAdminRoles()
    {
        var seller = await NewPayee();
        string[] paths = ["/api/admin/money/settings", "/api/admin/money/commissions", "/api/admin/money/payouts", "/api/admin/money/balance",
            "/api/admin/money/collections", "/api/admin/money/ledger", "/api/admin/money/payees", $"/api/admin/money/payees/{seller}/bank", "/api/admin/money/test-transfer/status?key=aabbccdd"];
        foreach (var path in paths)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Call("GET", path, null, null)).Status);
            foreach (var role in new[] { "SELLER", "DEPOT_OWNER", "FACTORY", "DEPOT_EMPLOYEE", "DRIVER" })
                Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", path, role, seller)).Status);
            var okCall = await AsAdmin("GET", path);
            Assert.True(okCall.Status == HttpStatusCode.OK, path + " " + okCall.Raw);
        }
        foreach (var (method, path) in new[] { ("PUT", "/api/admin/money/settings"), ("POST", "/api/admin/money/payouts/preview"), ("POST", "/api/admin/money/payouts/run"),
            ("POST", $"/api/admin/money/payouts/{Guid.NewGuid()}/approve"), ("POST", $"/api/admin/money/payouts/{Guid.NewGuid()}/mark-paid"), ("POST", "/api/admin/money/test-transfer"),
            ("POST", $"/api/admin/money/payees/{seller}/bank/verify"), ("PUT", "/api/admin/money/commissions/default") })
            Assert.Equal(HttpStatusCode.Forbidden, (await Call(method, path, "SELLER", seller, new { })).Status);
    }

    [PostgresMoneyFact]
    public async Task PayeeSeesOnlyOwnEarningsAndPayoutsAndBankIsMasked()
    {
        var a = await NewPayee("Người A"); var b = await NewPayee("Người B");
        await AddLine(a, 1_000_000, 30_000, state: "HELD", availableAt: T0.AddDays(1));
        await AddLine(a, 200_000, 6_000);
        await AddLine(b, 5_000_000, 100_000);
        var runB = (await RunOne(b, provider: Fake)).RunId!.Value;
        await using (var db = Open()) await Engine(db, Fake).ReconcileAsync(runB); // Fake.Status = Processing: giữ PROCESSING
        var earn = await Call("GET", "/api/money/my/earnings", "SELLER", a);
        Assert.Equal(HttpStatusCode.OK, earn.Status);
        var d = earn.Body.GetProperty("data");
        Assert.Equal(970_000, d.GetProperty("held").GetDecimal());
        Assert.Equal(194_000, d.GetProperty("available").GetDecimal());
        Assert.Equal(0, d.GetProperty("inPayout").GetDecimal());
        Assert.Equal(0, d.GetProperty("paid").GetDecimal());
        Assert.Equal("VND", d.GetProperty("currency").GetString());
        Assert.Equal(T0.AddDays(1), d.GetProperty("nextReleaseAt").GetDateTime().ToUniversalTime());
        var pa = await Call("GET", "/api/money/my/payouts", "SELLER", a);
        Assert.Equal(0, pa.Body.GetProperty("data").GetProperty("totalCount").GetInt32());
        var pb = await Call("GET", "/api/money/my/payouts", "SELLER", b);
        var items = pb.Body.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(4_900_000, items[0].GetProperty("net").GetDecimal());
        Assert.False(items[0].TryGetProperty("providerRef", out _)); // không lộ thông tin nhà cung cấp cho người nhận
        Assert.False(items[0].TryGetProperty("failReason", out _));
        var bank = await Call("GET", "/api/money/my/bank", "SELLER", a);
        Assert.Equal("****6789", bank.Body.GetProperty("data").GetProperty("accountNumberMasked").GetString());
        Assert.DoesNotContain("0123456789", bank.Raw);
        // sai vai trò, và Admin không dùng API người nhận
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", "/api/money/my/earnings", "DEPOT_EMPLOYEE", a)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", "/api/money/my/earnings", "ADMIN", AdminUser)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call("GET", "/api/money/my/earnings", null, null)).Status);
    }

    [PostgresMoneyFact]
    public async Task InactiveOrUnknownAccountIsBlockedEvenWithValidToken()
    {
        var inactive = await NewPayee("Người khóa", active: false);
        foreach (var path in new[] { "/api/money/my/earnings", "/api/money/my/payouts", "/api/money/my/bank" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", path, "SELLER", inactive)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("PUT", "/api/money/my/bank", "SELLER", inactive, new { bankName = "ACB", bankBin = "970416", accountNumber = "123456789", accountName = "Người khóa" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("GET", "/api/money/my/earnings", "SELLER", Guid.NewGuid())).Status);
    }

    [PostgresMoneyFact]
    public async Task BanksListRequiresLoginAndHasBins()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call("GET", "/api/money/banks", null, null)).Status);
        var ok = await Call("GET", "/api/money/banks", "FACTORY", Guid.NewGuid());
        var data = ok.Body.GetProperty("data");
        Assert.True(data.GetArrayLength() >= 25);
        Assert.All(data.EnumerateArray(), b => Assert.Equal(6, b.GetProperty("bin").GetString()!.Length));
    }

    [PostgresMoneyFact]
    public async Task PayeeBankUpdateAlwaysNeedsAdminApprovalAndHasNoHold()
    {
        var u = await NewPayee("Nguyễn Văn Đạt", bank: false);
        var bad = await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "ACB", bankBin = "1234", accountNumber = "123456789", accountName = "Nguyễn Văn Đạt" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "ACB", bankBin = "970416", accountNumber = "12ab", accountName = "Nguyễn Văn Đạt" })).Status);
        // Tên chủ tài khoản KHỚP họ tên nhưng vẫn phải chờ Admin: không tự xác minh.
        var match = await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "", bankBin = "970416", accountNumber = "123456789", accountName = "NGUYEN VAN DAT" });
        Assert.Equal(HttpStatusCode.OK, match.Status);
        var d = match.Body.GetProperty("data");
        Assert.Equal("PENDING_VERIFICATION", d.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("verifiedAt").ValueKind);
        Assert.Equal("ACB", d.GetProperty("bankName").GetString()); // lấy tên từ danh sách BIN khi bỏ trống
        Assert.Equal("****6789", d.GetProperty("accountNumberMasked").GetString());
        Assert.NotNull(d.GetProperty("changedAt").GetString());
        Assert.False(d.TryGetProperty("holdUntil", out _)); // không còn thời gian giữ
        Assert.Equal(JsonValueKind.Null, d.GetProperty("rejectReason").ValueKind);
        // lưu lại y nguyên: không đổi gì (kể cả thời điểm đổi)
        var again = await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "ACB", bankBin = "970416", accountNumber = "123456789", accountName = "NGUYEN VAN DAT" });
        Assert.Equal(d.GetProperty("changedAt").GetString(), again.Body.GetProperty("data").GetProperty("changedAt").GetString());
        // Admin duyệt rồi người dùng đổi tài khoản: quay về chờ duyệt, xóa verifiedAt
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("POST", $"/api/admin/money/payees/{u}/bank/verify", new { expectedAccountNumber = "123456789" })).Status);
        Assert.Equal("VERIFIED", (await Call("GET", "/api/money/my/bank", "SELLER", u)).Body.GetProperty("data").GetProperty("status").GetString());
        var same = await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "ACB", bankBin = "970416", accountNumber = "123456789", accountName = "NGUYEN VAN DAT" });
        Assert.Equal("VERIFIED", same.Body.GetProperty("data").GetProperty("status").GetString()); // lưu y nguyên không đổi trạng thái
        var other = await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "ACB", bankBin = "970416", accountNumber = "123456789", accountName = "TRAN THI B" });
        var o = other.Body.GetProperty("data");
        Assert.Equal("PENDING_VERIFICATION", o.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, o.GetProperty("verifiedAt").ValueKind);
        await using var db = Open();
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "PAYEE_BANK_UPDATED" && a.UserId == u));
        Assert.DoesNotContain("123456789", (await db.AuditLogs.AsNoTracking().Where(a => a.Action == "PAYEE_BANK_UPDATED").Select(a => a.NewData).ToListAsync()).First()!.Replace("****6789", ""));
    }

    [PostgresMoneyFact]
    public async Task AdminBankVerifyRequiresExpectedAccountNumber()
    {
        var u = await NewPayee("Người nhận", verified: false);
        var detail = await AsAdmin("GET", $"/api/admin/money/payees/{u}/bank");
        Assert.Equal("0123456789", detail.Body.GetProperty("data").GetProperty("accountNumber").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/payees/{u}/bank/verify", new { expectedAccountNumber = "9999999999" })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/payees/{u}/bank/verify", new { expectedAccountNumber = "" })).Status);
        // người nhận đổi tài khoản sau khi Admin xem: verify theo số cũ phải bị chặn
        Assert.Equal(HttpStatusCode.OK, (await Call("PUT", "/api/money/my/bank", "SELLER", u, new { bankName = "ACB", bankBin = "970416", accountNumber = "555555555", accountName = "Ai Đó" })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/payees/{u}/bank/verify", new { expectedAccountNumber = "0123456789" })).Status);
        var ok = await AsAdmin("POST", $"/api/admin/money/payees/{u}/bank/verify", new { expectedAccountNumber = "555555555" });
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal("VERIFIED", ok.Body.GetProperty("data").GetProperty("status").GetString());
        await using var db = Open();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "PAYEE_BANK_VERIFIED" && a.UserId == AdminUser));
    }

    [PostgresMoneyFact]
    public async Task AdminPreviewRunListDetailAndActionsOverHttp()
    {
        var a = await NewPayee("Người A");
        await AddLine(a, 1_000_000, 30_000);
        var body = new { payeeIds = new[] { a }, periodStart = PStart, periodEnd = PEnd };
        var prev = await AsAdmin("POST", "/api/admin/money/payouts/preview", body);
        Assert.Equal(HttpStatusCode.OK, prev.Status);
        var item = prev.Body.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("NEW", item.GetProperty("outcome").GetString());
        Assert.Equal(970_000, item.GetProperty("net").GetDecimal());
        Assert.Equal("PROCESSING", item.GetProperty("status").GetString());
        Assert.Equal(1, item.GetProperty("lines").GetArrayLength());
        Assert.Equal(0, await AsCount("payout_runs"));

        var run = await AsAdmin("POST", "/api/admin/money/payouts/run", body);
        Assert.Equal(HttpStatusCode.OK, run.Status);
        var runItem = run.Body.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("CREATED", runItem.GetProperty("outcome").GetString());
        var runId = runItem.GetProperty("runId").GetGuid();

        var list = await AsAdmin("GET", "/api/admin/money/payouts?pageSize=1000&status=PROCESSING");
        var ld = list.Body.GetProperty("data");
        Assert.Equal(100, ld.GetProperty("pageSize").GetInt32()); // giới hạn 100
        Assert.Equal(1, ld.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await AsAdmin("GET", "/api/admin/money/payouts?status=PAID")).Body.GetProperty("data").GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await AsAdmin("GET", $"/api/admin/money/payouts?payeeId={Guid.NewGuid()}")).Body.GetProperty("data").GetProperty("totalCount").GetInt32());

        var detail = await AsAdmin("GET", $"/api/admin/money/payouts/{runId}");
        var dd = detail.Body.GetProperty("data");
        Assert.Equal(1, dd.GetProperty("lines").GetArrayLength());
        Assert.Equal("****6789", dd.GetProperty("bank").GetProperty("accountMasked").GetString());
        Assert.DoesNotContain("0123456789", detail.Raw);
        Assert.Equal(dd.GetProperty("run").GetProperty("code").GetString(), dd.GetProperty("idempotencyKey").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/payouts/{runId}/mark-paid", new { reference = "BANK-1", confirm = false })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/payouts/{runId}/approve")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AsAdmin("POST", $"/api/admin/money/payouts/{runId}/release")).Status);
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("POST", $"/api/admin/money/payouts/{runId}/reconcile")).Status);
        var paid = await AsAdmin("POST", $"/api/admin/money/payouts/{runId}/mark-paid", new { reference = "BANK-1", confirm = true });
        Assert.Equal(HttpStatusCode.OK, paid.Status);
        Assert.Equal("PAID", paid.Body.GetProperty("data").GetProperty("run").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await AsAdmin("GET", $"/api/admin/money/payouts/{Guid.NewGuid()}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", "/api/admin/money/payouts/run", new { payeeIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray(), periodStart = PStart, periodEnd = PEnd })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", "/api/admin/money/payouts/run", new { payeeIds = new[] { a }, periodStart = PEnd, periodEnd = PStart })).Status);

        var ledger = await AsAdmin("GET", $"/api/admin/money/ledger?payeeId={a}&state=PAID&flow=PICKUP");
        Assert.Equal(1, ledger.Body.GetProperty("data").GetProperty("totalCount").GetInt32());
        var payees = await AsAdmin("GET", "/api/admin/money/payees?search=Ng%C6%B0%E1%BB%9Di");
        Assert.DoesNotContain("0123456789", payees.Raw);
        Assert.True(payees.Body.GetProperty("data").GetProperty("totalCount").GetInt32() >= 1);
        var coll = await AsAdmin("GET", "/api/admin/money/collections");
        Assert.Equal(HttpStatusCode.OK, coll.Status);
        Assert.DoesNotContain("checkout", coll.Raw, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<long> AsCount(string table)
    {
        await using var db = Open();
        return await db.Database.SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM {table}").SingleAsync();
    }

    [PostgresMoneyFact]
    public async Task SettingsCommissionsBalanceAndTestTransferOverHttp()
    {
        var s = await AsAdmin("GET", "/api/admin/money/settings");
        var d = s.Body.GetProperty("data");
        Assert.Equal("LEGACY", d.GetProperty("mode").GetString());
        Assert.Equal("SIMULATED", d.GetProperty("payoutProvider").GetString());
        Assert.False(d.GetProperty("payosPayoutConfigured").GetBoolean());
        Assert.Equal(3, d.GetProperty("defaultCommission").GetProperty("PICKUP").GetDecimal());
        Assert.False(d.TryGetProperty("payoutBankChangeHoldHours", out _)); // đã bỏ thời gian giữ sau khi đổi ngân hàng

        var upd = await AsAdmin("PUT", "/api/admin/money/settings", new { mode = "platform", payoutHoldMinutes = 5, payoutAutoMaxAmount = 2_000_000, defaultCommission = new Dictionary<string, decimal> { ["BATCH"] = 2.5m }, payoutTestEnabled = true });
        Assert.Equal(HttpStatusCode.OK, upd.Status);
        var u = upd.Body.GetProperty("data");
        Assert.Equal("PLATFORM", u.GetProperty("mode").GetString());
        Assert.Equal(5, u.GetProperty("payoutHoldMinutes").GetInt32());
        Assert.Equal(2.5m, u.GetProperty("defaultCommission").GetProperty("BATCH").GetDecimal());
        Assert.True(u.GetProperty("payoutTestEnabled").GetBoolean());
        await using (var db = Open()) Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "MONEY_SETTINGS_UPDATED" && a.UserId == AdminUser));

        // kiểm tra phạm vi
        foreach (var bad in new object[] { new { mode = "X" }, new { payoutSchedule = "MONTHLY" }, new { payoutHoldMinutes = -1 }, new { payoutMinAmount = 1.5 }, new { payoutProvider = "FOO" },
            new { payoutTestMaxAmount = 999 }, new { payoutTestMaxAmount = 100_001 }, new { defaultCommission = new Dictionary<string, decimal> { ["PICKUP"] = 101m } },
            new { defaultCommission = new Dictionary<string, decimal> { ["NOPE"] = 1m } } })
            Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("PUT", "/api/admin/money/settings", bad)).Status);
        var payos = await AsAdmin("PUT", "/api/admin/money/settings", new { payoutProvider = "PAYOS" });
        Assert.Equal(HttpStatusCode.BadRequest, payos.Status);
        Assert.Contains("PayOSPayout", payos.Raw);
        Assert.Equal("SIMULATED", (await AsAdmin("GET", "/api/admin/money/settings")).Body.GetProperty("data").GetProperty("payoutProvider").GetString());

        var payee = await NewPayee("Người A");
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("PUT", $"/api/admin/money/commissions/{payee}", new { flow = "PICKUP", rate = 1.5 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("PUT", $"/api/admin/money/commissions/{payee}", new { flow = "PICKUP", rate = 120 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("PUT", $"/api/admin/money/commissions/{payee}", new { flow = "BOGUS", rate = 1 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("PUT", $"/api/admin/money/commissions/{AdminUser}", new { flow = "PICKUP", rate = 1 })).Status); // Admin không phải người nhận
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("PUT", "/api/admin/money/commissions/default", new { flow = "LIQUIDATION", rate = 4 })).Status);
        var comm = await AsAdmin("GET", $"/api/admin/money/commissions?userId={payee}");
        var cd = comm.Body.GetProperty("data");
        Assert.Equal(1, cd.GetProperty("overrides").GetProperty("totalCount").GetInt32());
        Assert.Equal(1.5m, cd.GetProperty("overrides").GetProperty("items")[0].GetProperty("rate").GetDecimal());
        Assert.Equal(4, cd.GetProperty("defaults").GetProperty("LIQUIDATION").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("PUT", $"/api/admin/money/commissions/{payee}", new { flow = "PICKUP", rate = (decimal?)null })).Status); // xóa ghi đè
        Assert.Equal(0, (await AsAdmin("GET", $"/api/admin/money/commissions?userId={payee}")).Body.GetProperty("data").GetProperty("overrides").GetProperty("totalCount").GetInt32());

        Fake.Balance = 12_345_678;
        var bal = await AsAdmin("GET", "/api/admin/money/balance");
        var bd = bal.Body.GetProperty("data");
        Assert.Equal("12345678", bd.GetProperty("balance").GetString());
        Assert.Equal("SIMULATED", bd.GetProperty("provider").GetString());
        Assert.True(bd.GetProperty("configured").GetBoolean());

        var key = "0123456789abcdef";
        var tt = await AsAdmin("POST", "/api/admin/money/test-transfer", new { clientKey = key, amount = 2000, toBin = "970436", toAccountNumber = "9876543210" });
        Assert.Equal(HttpStatusCode.OK, tt.Status);
        Assert.Equal("ACCEPTED", tt.Body.GetProperty("data").GetProperty("outcome").GetString());
        Assert.DoesNotContain("9876543210", tt.Raw);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("POST", "/api/admin/money/test-transfer", new { clientKey = key, amount = 50, toBin = "970436", toAccountNumber = "9876543210" })).Status);
        Fake.Status = new(PayoutStatus.Succeeded, "FAKE-REF-1");
        var st = await AsAdmin("GET", $"/api/admin/money/test-transfer/status?key={key}");
        Assert.Equal("SUCCEEDED", st.Body.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await AsAdmin("GET", "/api/admin/money/test-transfer/status?key=zz")).Status);
        Assert.Equal(0, await AsCount("payout_runs"));
        Assert.Equal(0, await AsCount("payable_lines"));
    }

    [PostgresMoneyFact]
    public async Task AutoPayoutSwitchIsAdminOnlyAndListedPerPayee()
    {
        var u = await NewPayee("Người A");
        await AddLine(u, 100_000, 3_000);
        Assert.Equal(HttpStatusCode.Forbidden, (await Call("PUT", $"/api/admin/money/payees/{u}/auto-payout", "SELLER", u, new { autoEnabled = false })).Status);
        Assert.Equal(HttpStatusCode.OK, (await AsAdmin("PUT", $"/api/admin/money/payees/{u}/auto-payout", new { autoEnabled = false })).Status);
        var item = (await AsAdmin("GET", "/api/admin/money/payees")).Body.GetProperty("data").GetProperty("items").EnumerateArray().Single(i => i.GetProperty("userId").GetGuid() == u);
        Assert.False(item.GetProperty("autoPayoutEnabled").GetBoolean());
        Assert.Equal(97_000, item.GetProperty("availableAmount").GetDecimal());
        Assert.Equal("VERIFIED", item.GetProperty("bank").GetProperty("status").GetString());
    }
}
