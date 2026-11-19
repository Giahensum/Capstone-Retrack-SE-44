using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

public sealed class PostgresMoneyFactAttribute : FactAttribute
{
    public PostgresMoneyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RETRACK_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL test riêng (RETRACK_TEST_POSTGRES) để kiểm tra sổ cái thu/chi.";
    }
}

/// <summary>Mỗi lớp test chạy trong schema riêng, xóa sau khi xong (mẫu của ContractEsignGuardsPostgresTests).</summary>
public sealed class MoneyLedgerPostgresTests : IAsyncLifetime
{
    private readonly string schema = "money_" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection admin = null!;
    private string connection = null!;
    private string schemaSql = null!;
    private readonly Guid payer = Guid.NewGuid(), payee = Guid.NewGuid();
    private static readonly DateTime Trigger = new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

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
        connection = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema + ",public", Pooling = false }.ConnectionString;
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../db/bootstrap/retrack-system.sql")));
        schemaSql = sql[..sql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.Ordinal)] + "\nCOMMIT;";
        await ApplySchema();
        await using var db = Open();
        db.Users.AddRange(NewUser(payer, "DEPOT_OWNER"), NewUser(payee, "SELLER"));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (admin == null) return;
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await drop.ExecuteNonQueryAsync(); await admin.DisposeAsync();
    }

    private async Task ApplySchema()
    {
        await using var setup = new NpgsqlConnection(connection); await setup.OpenAsync();
        await using var command = new NpgsqlCommand(schemaSql, setup) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static User NewUser(Guid id, string role) => new() { Id = id, Role = role, Email = Guid.NewGuid() + "@test.invalid", FullName = "Người dùng thử" };
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private static PayableLedgerService Ledger(AppDbContext db) => new(db, new MoneyConfigReader(db), new FixedClock(Trigger));

    private async Task SetConfig(string key, string value)
    {
        await using var db = Open();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO system_configs (config_key, config_value) VALUES ({key}, {value}) ON CONFLICT (config_key) DO UPDATE SET config_value = EXCLUDED.config_value");
    }

    private async Task<MoneyCollection> PaidCollection(Guid? sourceId = null, string flow = "PICKUP", decimal amount = 1000)
    {
        await using var db = Open();
        var c = new MoneyCollection { Flow = flow, SourceType = MoneySources.PickupRequest, SourceId = sourceId ?? Guid.NewGuid(), PayerId = payer, PayeeId = payee, Amount = amount, Status = "PAID", PaidAt = Trigger };
        db.MoneyCollections.Add(c); await db.SaveChangesAsync(); return c;
    }

    private static SaleLineRequest Sale(MoneyCollection c, decimal gross = 1000, string flow = "PICKUP") =>
        new(c.Id, c.PayeeId, flow, c.SourceType, c.SourceId, gross, Trigger);

    private async Task<LedgerWriteResult> InTx(Func<PayableLedgerService, Task<LedgerWriteResult>> act)
    {
        await using var db = Open(); await using var tx = await db.Database.BeginTransactionAsync();
        var result = await act(Ledger(db)); await tx.CommitAsync(); return result;
    }

    [PostgresMoneyFact]
    public async Task SchemaScriptAppliesTwiceAndCreatesMoneyObjects()
    {
        await ApplySchema();
        await using var db = Open();
        var tables = await db.Database.SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = {schema} AND table_name IN ('money_collections','payable_lines','payout_runs','payee_commissions','payee_payout_settings')").ToListAsync();
        Assert.Equal(5, tables.Count);
        var columns = await db.Database.SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = {schema} AND table_name = 'users' AND column_name IN ('bank_bin','bank_status','bank_verified_at')").ToListAsync();
        Assert.Equal(3, columns.Count);
        Assert.Equal("LEGACY", (await new MoneyConfigReader(db).GetAsync()).Mode);
    }

    [PostgresMoneyFact]
    public async Task DatabaseRejectsDuplicatesAndBadRows()
    {
        var c = await PaidCollection();
        await InTx(l => l.CreateSaleLineAsync(Sale(c)));
        await using var db = Open();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db.PayableLines.Add(new() { CollectionId = c.Id, PayeeId = payee, Flow = "PICKUP", SourceType = c.SourceType, SourceId = c.SourceId, Gross = 10, CommissionRate = 1, Commission = 0, AvailableAt = Trigger });
            await db.SaveChangesAsync();
        });
        await using var db2 = Open();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db2.PayableLines.Add(new() { CollectionId = c.Id, PayeeId = payee, Flow = "PICKUP", SourceType = c.SourceType, SourceId = c.SourceId, Kind = "REFUND", Gross = 10, CommissionRate = 1, Commission = 0, AvailableAt = Trigger });
            await db2.SaveChangesAsync();
        });
        await using var db3 = Open();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db3.MoneyCollections.Add(new() { Flow = "PICKUP", SourceType = c.SourceType, SourceId = c.SourceId, PayerId = payer, PayeeId = payee, Amount = 5 });
            await db3.SaveChangesAsync(); // đã có khoản PAID cho cùng (flow, source_id)
        });
    }

    [PostgresMoneyFact]
    public async Task DuplicateCreateSaleLineIsNoOp()
    {
        var c = await PaidCollection();
        var first = await InTx(l => l.CreateSaleLineAsync(Sale(c)));
        var second = await InTx(l => l.CreateSaleLineAsync(Sale(c)));
        Assert.True(first.Created); Assert.False(second.Created);
        Assert.Equal(first.Line.Id, second.Line.Id);
        await using var db = Open();
        Assert.Equal(1, await db.PayableLines.CountAsync(l => l.SourceId == c.SourceId));
    }

    [PostgresMoneyFact]
    public async Task ConcurrentCreationYieldsExactlyOneRow()
    {
        var c = await PaidCollection();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => InTx(l => l.CreateSaleLineAsync(Sale(c))))));
        Assert.Equal(1, results.Count(r => r.Created));
        Assert.Single(results.Select(r => r.Line.Id).Distinct());
        await using var db = Open();
        Assert.Equal(1, await db.PayableLines.CountAsync(l => l.SourceId == c.SourceId));
    }

    [PostgresMoneyFact]
    public async Task WriteOutsideTransactionIsRefused()
    {
        var c = await PaidCollection();
        await using var db = Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ledger(db).CreateSaleLineAsync(Sale(c)));
    }

    [PostgresMoneyFact]
    public async Task UnpaidOrMismatchedCollectionIsRefused()
    {
        var c = await PaidCollection();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InTx(l => l.CreateSaleLineAsync(Sale(c) with { PayeeId = payer })));
        await using (var db = Open()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE money_collections SET status = 'PENDING' WHERE id = {c.Id}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => InTx(l => l.CreateSaleLineAsync(Sale(c))));
    }

    [PostgresMoneyFact]
    public async Task RateIsSnapshottedAndNotChangedByLaterConfigOrOverride()
    {
        await SetConfig("commission.default.PICKUP", "3");
        var old = await PaidCollection();
        var oldLine = (await InTx(l => l.CreateSaleLineAsync(Sale(old, 1000)))).Line;
        Assert.Equal(3m, oldLine.CommissionRate); Assert.Equal(30m, oldLine.Commission);

        await SetConfig("commission.default.PICKUP", "9");
        await using (var db = Open()) { db.PayeeCommissions.Add(new() { UserId = payee, Flow = "PICKUP", Rate = 12.5m }); await db.SaveChangesAsync(); }

        await using (var check = Open())
        {
            var again = await check.PayableLines.AsNoTracking().SingleAsync(l => l.Id == oldLine.Id);
            Assert.Equal(3m, again.CommissionRate); Assert.Equal(30m, again.Commission);
        }
        var fresh = await PaidCollection();
        var freshLine = (await InTx(l => l.CreateSaleLineAsync(Sale(fresh, 1000)))).Line;
        Assert.Equal(12.5m, freshLine.CommissionRate); Assert.Equal(125m, freshLine.Commission);

        // Ghi lại cùng nguồn cũ sau khi đổi cấu hình vẫn giữ nguyên dòng cũ.
        var retry = await InTx(l => l.CreateSaleLineAsync(Sale(old, 1000)));
        Assert.False(retry.Created); Assert.Equal(3m, retry.Line.CommissionRate);
    }

    [PostgresMoneyFact]
    public async Task AvailableAtIsTriggerPlusHoldAndMarkAvailableReleasesDueLinesOnly()
    {
        await SetConfig("payout.hold_minutes", "90");
        await SetConfig("commission.default.BATCH", "2");
        var c = await PaidCollection(flow: "BATCH");
        var line = (await InTx(l => l.CreateSaleLineAsync(Sale(c, 5000, "BATCH")))).Line;
        Assert.Equal(Trigger.AddMinutes(90), line.AvailableAt.ToUniversalTime());
        Assert.Equal("HELD", line.State); Assert.Equal(100m, line.Commission);

        await using (var db = Open())
        {
            var svc = Ledger(db);
            Assert.Equal(0, await svc.MarkAvailableAsync(Trigger.AddMinutes(89)));
            Assert.Equal("HELD", (await db.PayableLines.AsNoTracking().SingleAsync(l => l.Id == line.Id)).State);
            Assert.Equal(1, await svc.MarkAvailableAsync(Trigger.AddMinutes(90)));
            Assert.Equal(0, await svc.MarkAvailableAsync(Trigger.AddMinutes(500)));
            Assert.Equal("AVAILABLE", (await db.PayableLines.AsNoTracking().SingleAsync(l => l.Id == line.Id)).State);
        }
    }

    [PostgresMoneyFact]
    public async Task PartialRefundsSplitCommissionExactlyAndAreIdempotentPerKey()
    {
        await SetConfig("commission.default.PICKUP", "5");
        var c = await PaidCollection(amount: 100);
        var sale = (await InTx(l => l.CreateSaleLineAsync(Sale(c, 100)))).Line;
        Assert.Equal(5m, sale.Commission);
        RefundLineRequest R(string key, decimal gross) => new(c.SourceType, c.SourceId, key, gross, Trigger);

        var r1 = await InTx(l => l.CreateRefundLineAsync(R("r1", 33)));
        var r1again = await InTx(l => l.CreateRefundLineAsync(R("r1", 33)));
        var r2 = await InTx(l => l.CreateRefundLineAsync(R("r2", 33)));
        var r3 = await InTx(l => l.CreateRefundLineAsync(R("r3", 34)));
        Assert.True(r1.Created); Assert.False(r1again.Created); Assert.Equal(r1.Line.Id, r1again.Line.Id);
        Assert.Equal([2m, 1m, 2m], new[] { r1.Line.Commission, r2.Line.Commission, r3.Line.Commission });
        Assert.All(new[] { r1, r2, r3 }, r => { Assert.Equal("REFUND", r.Line.Kind); Assert.Equal("AVAILABLE", r.Line.State); Assert.Equal(5m, r.Line.CommissionRate); });

        await Assert.ThrowsAsync<InvalidOperationException>(() => InTx(l => l.CreateRefundLineAsync(R("r1", 40)))); // khóa cũ, số tiền khác
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => InTx(l => l.CreateRefundLineAsync(R("r4", 1)))); // vượt gốc
        await Assert.ThrowsAsync<KeyNotFoundException>(() => InTx(l => l.CreateRefundLineAsync(new(c.SourceType, Guid.NewGuid(), "x", 1, Trigger))));
        await using var db = Open();
        Assert.Equal(3, await db.PayableLines.CountAsync(l => l.SourceId == c.SourceId && l.Kind == "REFUND"));
    }

    [PostgresMoneyFact]
    public async Task ConcurrentRefundsWithDifferentKeysNeverExceedOriginal()
    {
        await SetConfig("commission.default.PICKUP", "5");
        var c = await PaidCollection(amount: 100);
        await InTx(l => l.CreateSaleLineAsync(Sale(c, 100)));
        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Task.Run(async () =>
        {
            try { return (await InTx(l => l.CreateRefundLineAsync(new(c.SourceType, c.SourceId, "k" + i, 20, Trigger)))).Created; }
            catch (ArgumentOutOfRangeException) { return false; }
        })));
        Assert.Equal(5, attempts.Count(x => x)); // 5 x 20 = 100, lần thứ sáu vượt gốc
        await using var db = Open();
        var refunds = await db.PayableLines.AsNoTracking().Where(l => l.SourceId == c.SourceId && l.Kind == "REFUND").ToListAsync();
        Assert.Equal(100m, refunds.Sum(r => r.Gross)); Assert.Equal(5m, refunds.Sum(r => r.Commission));
    }
}
