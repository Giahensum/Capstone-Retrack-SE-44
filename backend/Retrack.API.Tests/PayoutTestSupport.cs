using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

public sealed class MutableClock(DateTime utc) : TimeProvider
{
    private DateTime now = utc;
    public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    public void Advance(TimeSpan by) => now += by;
    public void Set(DateTime utcTime) => now = utcTime;
}

/// <summary>Nhà cung cấp chi giả điều khiển được: đếm lệnh, đặt kết quả và số dư. An toàn đa luồng.</summary>
public sealed class ControllableProvider : IPayoutProvider
{
    private readonly object gate = new();
    private readonly Queue<PayoutResult> queued = new();
    private readonly List<PayoutRequest> calls = [];
    public string Name { get; set; } = "SIMULATED";
    public bool IsConfigured => true;
    public decimal Balance { get; set; } = 100_000_000m;
    public bool BalanceThrows { get; set; }
    public PayoutResult Default { get; set; } = new(PayoutOutcome.Accepted, "FAKE-REF");
    public PayoutStatusResult Status { get; set; } = new(PayoutStatus.Processing, null);
    public int BalanceCalls;
    public void Enqueue(PayoutResult r) { lock (gate) queued.Enqueue(r); }
    public IReadOnlyList<PayoutRequest> Calls { get { lock (gate) return calls.ToList(); } }
    public int PayoutCalls => Calls.Count;

    public Task<PayoutResult> PayoutAsync(PayoutRequest request, CancellationToken ct = default)
    {
        lock (gate)
        {
            calls.Add(request);
            var r = queued.Count > 0 ? queued.Dequeue() : Default;
            return Task.FromResult(r.Outcome == PayoutOutcome.Accepted && r.ProviderRef == "FAKE-REF" ? r with { ProviderRef = "FAKE-REF-" + calls.Count } : r);
        }
    }

    public Task<PayoutStatusResult> GetPayoutAsync(string referenceOrProviderRef, CancellationToken ct = default) => Task.FromResult(Status);

    public Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref BalanceCalls);
        if (BalanceThrows) throw new InvalidOperationException("Không tra cứu được số dư.");
        return Task.FromResult(Balance);
    }
}

public sealed class FixedResolver(IPayoutProvider provider) : IPayoutProviderResolver
{
    public Task<IPayoutProvider> ResolveCurrentAsync(CancellationToken ct = default) => Task.FromResult(provider);
    public IPayoutProvider GetByName(string name) => provider;
}

/// <summary>Nền chung cho test engine chi: schema PostgreSQL riêng cho mỗi lần chạy, đồng hồ điều khiển được, hàm dựng dữ liệu.</summary>
public abstract class PayoutTestBase : IAsyncLifetime
{
    protected readonly string Schema = "payout_" + Guid.NewGuid().ToString("N");
    protected NpgsqlConnection Admin = null!;
    protected string Connection = null!;
    protected static readonly DateTime T0 = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime PStart = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime PEnd = new(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime LineAvail = new(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
    protected readonly MutableClock Clock = new(T0);
    protected readonly ControllableProvider Fake = new();
    protected Guid AdminUser, Payer;
    private bool ready;

    public virtual async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(source)) return;
        var parsed = new NpgsqlConnectionStringBuilder(source);
        Assert.Equal("Retrack_TV2_test", parsed.Database);
        Assert.Equal("Retrack_TV2_test", new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")).Database);
        Assert.Contains(parsed.Host, new[] { "localhost", "127.0.0.1" });
        Admin = new(source); await Admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {Schema}", Admin)) await create.ExecuteNonQueryAsync();
        Connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = Schema + ",public", Pooling = false }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        var schemaSql = sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;";
        await using (var setup = new NpgsqlConnection(Connection))
        {
            await setup.OpenAsync();
            await using var command = new NpgsqlCommand(schemaSql, setup) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
        AdminUser = await NewUser("ADMIN", "Quản trị viên", bank: false);
        Payer = await NewUser("DEPOT_OWNER", "Chủ kho trả", bank: false);
        ready = true;
    }

    public virtual async Task DisposeAsync()
    {
        if (Admin == null) return;
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {Schema} CASCADE", Admin); await drop.ExecuteNonQueryAsync(); await Admin.DisposeAsync();
    }

    protected bool Ready => ready;
    protected AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options);
    protected PayoutEngine Engine(AppDbContext db, IPayoutProvider? provider = null) =>
        new(db, new MoneyConfigReader(db), new FixedResolver(provider ?? Fake), Clock, NullLogger<PayoutEngine>.Instance);

    protected async Task<Guid> NewUser(string role, string name, bool bank = true, bool active = true, bool verified = true, DateTime? changedAt = null)
    {
        await using var db = Open();
        var u = new User { Id = Guid.NewGuid(), Role = role, Email = Guid.NewGuid() + "@test.invalid", FullName = name, IsActive = active };
        if (bank)
        {
            u.BankName = "Vietcombank"; u.BankBin = "970436"; u.BankAccountNumber = "0123456789"; u.BankAccountName = name;
            u.BankStatus = verified ? "VERIFIED" : "PENDING_VERIFICATION"; u.BankVerifiedAt = verified ? T0.AddDays(-30) : null; u.BankChangedAt = changedAt;
        }
        db.Users.Add(u); await db.SaveChangesAsync(); return u.Id;
    }

    protected Task<Guid> NewPayee(string name = "Nguyễn Văn An", bool verified = true, bool bank = true, bool active = true, DateTime? changedAt = null) =>
        NewUser("SELLER", name, bank, active, verified, changedAt);

    /// <summary>Thêm một dòng sổ cái (mỗi dòng một khoản thu PAID riêng). REFUND dùng nguồn riêng (đủ cho test engine).</summary>
    protected async Task<Guid> AddLine(Guid payee, decimal gross, decimal commission, string kind = "SALE", DateTime? availableAt = null, string state = "AVAILABLE", string flow = "PICKUP")
    {
        await using var db = Open();
        var source = Guid.NewGuid();
        var c = new MoneyCollection { Flow = flow, SourceType = MoneySources.PickupRequest, SourceId = source, PayerId = Payer, PayeeId = payee, Amount = Math.Max(1, gross), Status = "PAID", PaidAt = T0 };
        db.MoneyCollections.Add(c); await db.SaveChangesAsync();
        var l = new PayableLine
        {
            CollectionId = c.Id, PayeeId = payee, Flow = flow, SourceType = c.SourceType, SourceId = source, Kind = kind, RefundKey = kind == "REFUND" ? "R-" + Guid.NewGuid().ToString("N")[..8] : null,
            Gross = gross, Commission = commission, CommissionRate = gross == 0 ? 0 : decimal.Round(commission * 100m / gross, 4), State = state, AvailableAt = availableAt ?? LineAvail, CreatedAt = T0,
        };
        db.PayableLines.Add(l); await db.SaveChangesAsync(); return l.Id;
    }

    protected async Task SetConfig(string key, string value)
    {
        await using var db = Open();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO system_configs (config_key, config_value) VALUES ({key}, {value}) ON CONFLICT (config_key) DO UPDATE SET config_value = EXCLUDED.config_value");
    }

    protected async Task<PayoutRun> RunOf(Guid id)
    {
        await using var db = Open();
        return await db.PayoutRuns.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    protected async Task<List<PayableLine>> LinesOf(Guid payee)
    {
        await using var db = Open();
        return await db.PayableLines.AsNoTracking().Where(l => l.PayeeId == payee).OrderBy(l => l.Gross).ToListAsync();
    }

    protected async Task<PayoutPlan> RunOne(Guid payee, DateTime? start = null, DateTime? end = null, IPayoutProvider? provider = null, bool autoOnly = false)
    {
        await using var db = Open();
        var batch = await Engine(db, provider).RunAsync([payee], start ?? PStart, end ?? PEnd, AdminUser, autoOnly);
        return batch.Items.Single();
    }
}
