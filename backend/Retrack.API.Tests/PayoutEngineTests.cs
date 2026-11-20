using Microsoft.EntityFrameworkCore;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>Quy tắc thuần của engine chi (không cần PostgreSQL).</summary>
public sealed class PayoutRulesTests
{
    private static MoneyConfig Cfg(decimal min = 50_000, decimal autoMax = 5_000_000) => new("PLATFORM", new Dictionary<string, decimal>(), false, "DAILY", 0, min, autoMax, "SIMULATED", false, 10_000);
    private static readonly BankCheck Ok = new(true, null);

    [Fact]
    public void DecideFollowsDesignOrder()
    {
        Assert.Equal("CARRIED", PayoutRules.Decide(-1, Cfg(), new(false, "x"), false).Status);
        Assert.Equal("SETTLED", PayoutRules.Decide(0, Cfg(), new(false, "x"), false).Status);
        Assert.Equal("DEFERRED", PayoutRules.Decide(49_999, Cfg(), new(false, "x"), false).Status); // dưới mức tối thiểu thắng ngân hàng chưa xác minh
        Assert.Equal("HELD", PayoutRules.Decide(60_000, Cfg(), new(false, "x"), false).Status);
        Assert.Equal("HELD", PayoutRules.Decide(9_000_000, Cfg(), new(false, "x"), false).Status); // HELD thắng chờ duyệt
        Assert.Equal("AWAITING_APPROVAL", PayoutRules.Decide(5_000_001, Cfg(), Ok, false).Status);
        Assert.Equal("PROCESSING", PayoutRules.Decide(5_000_001, Cfg(), Ok, true).Status);
        Assert.Equal("PROCESSING", PayoutRules.Decide(5_000_000, Cfg(), Ok, false).Status);
        Assert.Equal("PROCESSING", PayoutRules.Decide(50_000, Cfg(), Ok, false).Status);
    }

    [Fact]
    public void CheckBankRequiresVerifiedAndCompleteOnly()
    {
        Assert.True(PayoutRules.CheckBank("970436", "0123456789", "AN", "VERIFIED").Ready);
        Assert.False(PayoutRules.CheckBank("970436", "0123456789", "AN", "PENDING_VERIFICATION").Ready);
        Assert.False(PayoutRules.CheckBank("970436", "0123456789", "AN", null).Ready);
        var rejected = PayoutRules.CheckBank("970436", "0123456789", "AN", "REJECTED");
        Assert.False(rejected.Ready);
        Assert.Contains("từ chối", rejected.Reason);
        Assert.False(PayoutRules.CheckBank("97043", "0123456789", "AN", "VERIFIED").Ready);
        Assert.False(PayoutRules.CheckBank("970436", "12345", "AN", "VERIFIED").Ready);
        Assert.False(PayoutRules.CheckBank("970436", "0123456789", "", "VERIFIED").Ready);
        Assert.True(PayoutRules.CheckBank("970436", "012345", "AN", "VERIFIED").Ready);   // 6 số
        Assert.True(PayoutRules.CheckBank("970436", new string('9', 20), "AN", "VERIFIED").Ready); // 20 số
        Assert.False(PayoutRules.CheckBank("970436", new string('9', 21), "AN", "VERIFIED").Ready);
    }

    [Fact]
    public void TotalsFollowNetFormula()
    {
        PayableLine L(string kind, decimal g, decimal c) => new() { Kind = kind, Gross = g, Commission = c };
        var t = PayoutRules.Totals([L("SALE", 1_000_000, 30_000), L("SALE", 500_000, 10_000), L("REFUND", 400_000, 12_000)], 50_000);
        Assert.Equal(1_500_000, t.Gross);
        Assert.Equal(40_000, t.Commission);
        Assert.Equal(388_000, t.RefundAmount);
        Assert.Equal(50_000, t.CarriedDebt);
        Assert.Equal(1_500_000 - 40_000 - 388_000 - 50_000, t.Net);
        Assert.Equal(1_022_000, t.Net);
    }

    [Theory]
    [InlineData("2026-03-10T03:00:00Z", "DAILY", "2026-03-09T19:00:00Z")]   // 10:00 VN: mốc 0 giờ hôm nay
    [InlineData("2026-03-09T18:30:00Z", "DAILY", "2026-03-08T19:00:00Z")]   // 01:30 VN: chưa tới 02:00, mốc hôm qua
    [InlineData("2026-03-09T19:00:00Z", "DAILY", "2026-03-09T19:00:00Z")]   // đúng 02:00 VN
    [InlineData("2026-03-11T03:00:00Z", "WEEKLY", "2026-03-08T19:00:00Z")]  // thứ Tư 10:00 VN: thứ Hai 9/3 0 giờ VN
    [InlineData("2026-03-08T19:30:00Z", "WEEKLY", "2026-03-08T19:00:00Z")]  // thứ Hai 9/3 02:30 VN
    [InlineData("2026-03-08T18:30:00Z", "WEEKLY", "2026-03-01T19:00:00Z")]  // thứ Hai 01:30 VN: còn mốc tuần trước
    public void ScheduleBoundaryIsTheScheduledInstantAtDefaultTwoAm(string now, string schedule, string expected) =>
        Assert.Equal(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal), PayoutRules.ScheduleBoundaryUtc(DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal), schedule));

    [Theory]
    [InlineData("2026-03-10T03:00:00Z", "DAILY", 14, 1, "2026-03-09T07:00:00Z")]   // 10:00 VN, chạy 14:00: chưa tới giờ nên mốc là 14:00 hôm qua
    [InlineData("2026-03-10T08:00:00Z", "DAILY", 14, 1, "2026-03-10T07:00:00Z")]   // 15:00 VN: mốc 14:00 hôm nay
    [InlineData("2026-03-10T08:00:00Z", "DAILY", 0, 1, "2026-03-09T17:00:00Z")]    // chạy 00:00: mốc 0 giờ hôm nay
    [InlineData("2026-03-11T08:00:00Z", "WEEKLY", 9, 5, "2026-03-06T02:00:00Z")]   // thứ Tư 15:00 VN, chạy thứ Sáu 09:00: mốc thứ Sáu 6/3 09:00 VN
    [InlineData("2026-03-11T08:00:00Z", "WEEKLY", 16, 3, "2026-03-04T09:00:00Z")]  // thứ Tư 15:00 VN, chạy thứ Tư 16:00: chưa tới giờ, còn mốc tuần trước (4/3 16:00 VN)
    [InlineData("2026-03-14T20:00:00Z", "WEEKLY", 2, 7, "2026-03-14T19:00:00Z")]   // 03:00 VN Chủ nhật 15/3, chạy Chủ nhật 02:00: mốc 15/3 02:00 VN
    public void ScheduleBoundaryFollowsConfiguredHourAndWeekday(string now, string schedule, int hour, int weekday, string expected) =>
        Assert.Equal(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal), PayoutRules.ScheduleBoundaryUtc(DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal), schedule, hour, weekday));

    [Fact]
    public void CodeIsDeterministicWithLetterSuffixesAndKeysNeverCollide()
    {
        var payee = Guid.Parse("abcdef12-0000-0000-0000-000000000000");
        var s = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var e = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("PAY-260301-260305-abcdef12", PayoutRules.Code(payee, s, e));
        Assert.Equal("PAY-260301-260305-abcdef12-B", PayoutRules.Code(payee, s, e, 1));
        Assert.Equal("PAY-260301-260305-abcdef12-C", PayoutRules.Code(payee, s, e, 2));
        Assert.Equal("PAY-260301-260305-abcdef12", PayoutRules.KeyFor("PAY-260301-260305-abcdef12", 1));
        Assert.Equal("PAY-260301-260305-abcdef12-2", PayoutRules.KeyFor("PAY-260301-260305-abcdef12", 2));
        Assert.True(PayoutRules.Description(PayoutRules.Code(payee, s, e, 3)).Length <= 20);
    }

    [Fact]
    public void ValidatePeriodRejectsReversedAndFuture()
    {
        var now = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        Assert.NotNull(PayoutRules.ValidatePeriod(now, now, now));
        Assert.NotNull(PayoutRules.ValidatePeriod(now.AddDays(-1), now.AddSeconds(1), now));
        Assert.Null(PayoutRules.ValidatePeriod(now.AddDays(-1), now, now));
    }

    [Fact]
    public void CarryOverRules()
    {
        Assert.Equal(0, PayoutEngine.CarryOver(null));
        Assert.Equal(194_000, PayoutEngine.CarryOver(new() { Status = "CARRIED", Net = -194_000 }));
        Assert.Equal(20_000, PayoutEngine.CarryOver(new() { Status = "DEFERRED", Net = 40_000, CarriedDebt = 20_000 }));
        Assert.Equal(0, PayoutEngine.CarryOver(new() { Status = "PAID", Net = 40_000, CarriedDebt = 20_000 }));
        Assert.Equal(0, PayoutEngine.CarryOver(new() { Status = "HELD", Net = 40_000, CarriedDebt = 20_000 }));
    }

    [Fact]
    public void BankNameMatchingIgnoresDiacriticsCaseAndSpacing()
    {
        Assert.Equal("NGUYEN VAN DAT", PayoutBankRules.NormalizeName("  Nguyễn  Văn Đạt "));
        Assert.True(PayoutBankRules.NamesMatch("NGUYEN VAN DAT", "Nguyễn Văn Đạt"));
        Assert.False(PayoutBankRules.NamesMatch("NGUYEN VAN AN", "Nguyễn Văn Đạt"));
        Assert.False(PayoutBankRules.NamesMatch("", ""));
        Assert.Equal("****6789", PayoutBankRules.Mask("0123456789"));
        Assert.Null(PayoutBankRules.Mask(null));
        Assert.True(PayoutBankRules.IsValidAccountNumber("123456"));
        Assert.False(PayoutBankRules.IsValidAccountNumber("12345"));
        Assert.False(PayoutBankRules.IsValidAccountNumber("12345678901234567890" + "1"));
        Assert.False(PayoutBankRules.IsValidAccountNumber("12a456"));
        Assert.True(VietnameseBanks.All.Count >= 25 && VietnameseBanks.All.All(b => PayoutBankRules.IsValidBin(b.Bin)));
        Assert.Equal(VietnameseBanks.All.Count, VietnameseBanks.All.Select(b => b.Bin).Distinct().Count());
    }
}

public sealed class PayOsPayoutProviderTests
{
    private sealed class FakeClient : IPayOsPayoutClient
    {
        public bool IsConfigured { get; set; } = true;
        public PayOsPayoutResult? Create; public Exception? CreateThrows;
        public PayOsPayoutStatus? Find, Get; public decimal? Balance = 5;
        public (string Key, string Ref, long Amount, string Desc)? Sent;
        public Task<PayOsPayoutResult> CreatePayoutAsync(string idempotencyKey, string referenceId, long amount, string description, string toBin, string toAccountNumber, string? category = null, CancellationToken ct = default)
        {
            Sent = (idempotencyKey, referenceId, amount, description);
            if (CreateThrows != null) throw CreateThrows;
            return Task.FromResult(Create!);
        }
        public Task<PayOsPayoutStatus> GetPayoutAsync(string payoutId, CancellationToken ct = default) => Task.FromResult(Get!);
        public Task<PayOsPayoutStatus> FindByReferenceAsync(string referenceId, CancellationToken ct = default) => Task.FromResult(Find!);
        public Task<decimal?> GetBalanceAsync(CancellationToken ct = default) => Task.FromResult(Balance);
    }

    private static PayOsPayoutProvider P(FakeClient c) => new(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<PayOsPayoutProvider>.Instance);
    private static PayoutRequest Req() => new("REF", "KEY", 1000, "RETRACK ABC", "970436", "0123456789");
    private static PayOsPayoutStatus St(PayOsPayoutState s, string? id = null) => new(s, id, "REF", null, null, "00", "d");

    [Theory]
    [InlineData(PayOsPayoutOutcome.Accepted, PayoutOutcome.Accepted)]
    [InlineData(PayOsPayoutOutcome.DefiniteFailure, PayoutOutcome.DefiniteFailure)]
    [InlineData(PayOsPayoutOutcome.Unknown, PayoutOutcome.Unknown)]
    public async Task MapsOutcomes(PayOsPayoutOutcome from, PayoutOutcome to)
    {
        var c = new FakeClient { Create = new(from, "P1", "00", "d", 200, "KEY", "REF") };
        var r = await P(c).PayoutAsync(Req());
        Assert.Equal(to, r.Outcome);
        Assert.Equal("P1", r.ProviderRef);
        Assert.Equal(("KEY", "REF", 1000L, "RETRACK ABC"), c.Sent!.Value);
    }

    [Fact]
    public async Task ExceptionIsUnknownNeverDefinite()
    {
        var c = new FakeClient { CreateThrows = new TaskCanceledException() };
        Assert.Equal(PayoutOutcome.Unknown, (await P(c).PayoutAsync(Req())).Outcome);
    }

    [Fact]
    public async Task RejectsNonIntegerAmountWithoutCalling()
    {
        var c = new FakeClient { Create = new(PayOsPayoutOutcome.Accepted, "P1", "00", "d", 200, "KEY", "REF") };
        var r = await P(c).PayoutAsync(Req() with { Amount = 10.5m });
        Assert.Equal(PayoutOutcome.DefiniteFailure, r.Outcome);
        Assert.Null(c.Sent);
    }

    [Theory]
    [InlineData(PayOsPayoutState.Processing, PayoutStatus.Processing)]
    [InlineData(PayOsPayoutState.Succeeded, PayoutStatus.Succeeded)]
    [InlineData(PayOsPayoutState.Failed, PayoutStatus.Failed)]
    public async Task MapsLookupStates(PayOsPayoutState from, PayoutStatus to)
    {
        var c = new FakeClient { Find = St(from, "P9") };
        var s = await P(c).GetPayoutAsync("REF");
        Assert.Equal(to, s.Status);
        Assert.Equal("P9", s.ProviderRef);
    }

    [Fact]
    public async Task LookupFallsBackToPayoutIdThenNotFound()
    {
        var c = new FakeClient { Find = St(PayOsPayoutState.NotFound), Get = St(PayOsPayoutState.Succeeded, "P5") };
        Assert.Equal(PayoutStatus.Succeeded, (await P(c).GetPayoutAsync("P5")).Status);
        c.Get = St(PayOsPayoutState.NotFound);
        Assert.Equal(PayoutStatus.NotFound, (await P(c).GetPayoutAsync("X")).Status);
    }

    [Fact]
    public async Task LookupErrorThrowsInsteadOfGuessing()
    {
        var c = new FakeClient { Find = St(PayOsPayoutState.Unknown) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => P(c).GetPayoutAsync("REF"));
    }

    [Fact]
    public async Task BalanceAndConfiguredFlag()
    {
        var c = new FakeClient { Balance = 123_456 };
        Assert.Equal(123_456, await P(c).GetBalanceAsync());
        c.Balance = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => P(c).GetBalanceAsync());
        c.IsConfigured = false;
        Assert.False(P(c).IsConfigured);
        Assert.Equal("PAYOS", P(c).Name);
    }

    [Fact]
    public async Task ResolverNeverFallsBackToSimulatedForUnconfiguredPayos()
    {
        var sim = new SimulatedPayoutProvider(new(), TimeProvider.System);
        var resolver = new PayoutProviderResolver(null!, sim, P(new FakeClient { IsConfigured = false }));
        var ex = Assert.Throws<InvalidOperationException>(() => resolver.GetByName("PAYOS"));
        Assert.Contains("PayOSPayout", ex.Message);
        Assert.Same(sim, resolver.GetByName("SIMULATED"));
        Assert.Equal("PAYOS", P(new FakeClient()).Name);
        await Task.CompletedTask;
    }
}

public sealed class PayoutEngineTests : PayoutTestBase
{
    private static SimulatedPayoutProvider NewSim(MutableClock clock) => new(new SimulatedPayoutOptions { Delay = TimeSpan.Zero }, clock);

    private async Task<long> Count(string table)
    {
        await using var db = Open();
        return await db.Database.SqlQueryRaw<long>($"SELECT count(*) AS \"Value\" FROM {table}").SingleAsync();
    }

    private async Task<long> CountSql(string sql)
    {
        await using var db = Open();
        return await db.Database.SqlQueryRaw<long>(sql).SingleAsync();
    }

    [PostgresMoneyFact]
    public async Task PreviewWritesNothingAndShowsLinesNetAndStatus()
    {
        var payee = await NewPayee();
        await AddLine(payee, 1_000_000, 30_000);
        await AddLine(payee, 400_000, 12_000, "REFUND");
        await AddLine(payee, 100_000, 3_000, state: "HELD"); // đã hết giữ: vào kỳ khi chạy thật
        await AddLine(payee, 200_000, 6_000, availableAt: PEnd.AddDays(1)); // sau period_end: không vào
        PayoutBatchResult result;
        await using (var db = Open()) result = await Engine(db).PreviewAsync([payee], PStart, PEnd);
        var plan = result.Items.Single();
        Assert.Equal("NEW", plan.Outcome);
        Assert.Equal(3, plan.Lines.Count);
        Assert.Equal(1_100_000, plan.Gross);
        Assert.Equal(33_000, plan.Commission);
        Assert.Equal(388_000, plan.RefundAmount);
        Assert.Equal(0, plan.CarriedDebt);
        Assert.Equal(1_100_000 - 33_000 - 388_000, plan.Net);
        Assert.Equal("PROCESSING", plan.Status);
        Assert.Equal("VERIFIED", plan.BankStatus);
        Assert.Equal("****6789", plan.BankMasked);
        Assert.Contains(plan.Lines, l => l.Kind == "REFUND" && l.Net == -388_000);
        Assert.Equal(0, await Count("payout_runs"));
        Assert.Equal(0, await Count("audit_logs"));
        Assert.Equal(0, await Count("notifications"));
        Assert.Equal(0, await CountSql("SELECT count(*) AS \"Value\" FROM payable_lines WHERE payout_id IS NOT NULL OR state = 'IN_PAYOUT'"));
        Assert.Equal(1, await CountSql("SELECT count(*) AS \"Value\" FROM payable_lines WHERE state = 'HELD'")); // preview không chuyển HELD sang AVAILABLE
        Assert.Equal(0, Fake.PayoutCalls);
    }

    [PostgresMoneyFact]
    public async Task RunCreatesRunSnapshotsAuditsNotifiesAndCallsProviderOnce()
    {
        var payee = await NewPayee();
        await AddLine(payee, 1_000_000, 30_000);
        await AddLine(payee, 400_000, 12_000, "REFUND");
        var plan = await RunOne(payee);
        Assert.Equal("CREATED", plan.Outcome);
        Assert.Equal($"PAY-260301-260305-{payee:N}"[..26], plan.RunCode);
        var run = await RunOf(plan.RunId!.Value);
        Assert.Equal("PROCESSING", run.Status);
        Assert.Equal(582_000, run.Net);
        Assert.Equal(1_000_000, run.Gross); Assert.Equal(30_000, run.Commission); Assert.Equal(388_000, run.RefundAmount);
        Assert.StartsWith("FAKE-REF", run.ProviderRef);
        Assert.Equal(AdminUser, run.CreatedBy);
        Assert.NotNull(run.LastAttemptAt);
        Assert.Contains("970436", run.BankSnapshot!);
        var call = Assert.Single(Fake.Calls);
        Assert.Equal(run.Code, call.IdempotencyKey); Assert.Equal(run.Code, call.ReferenceId);
        Assert.Equal(582_000, call.Amount);
        Assert.True(call.Description.Length <= 20);
        Assert.Equal("970436", call.ToBin);
        Assert.All(await LinesOf(payee), l => { Assert.Equal("IN_PAYOUT", l.State); Assert.Equal(run.Id, l.PayoutId); });
        Assert.Equal(1, await CountSql("SELECT count(*) AS \"Value\" FROM audit_logs WHERE action = 'PAYOUT_RUN_CREATED'"));
        await using var db = Open();
        Assert.Single(await db.Notifications.Where(n => n.UserId == payee && n.Category == "PAYOUT").ToListAsync());
    }

    [PostgresMoneyFact]
    public async Task RunForSeveralPayeesIsolatesThem()
    {
        var a = await NewPayee("Người A"); var b = await NewPayee("Người B"); var c = await NewPayee("Người C", verified: false);
        await AddLine(a, 1_000_000, 30_000); await AddLine(b, 2_000_000, 40_000); await AddLine(c, 300_000, 9_000);
        PayoutBatchResult result;
        await using (var db = Open()) result = await Engine(db).RunAsync([a, b, c, a], PStart, PEnd, AdminUser);
        Assert.Equal(3, result.Items.Count);
        var byPayee = result.Items.ToDictionary(i => i.PayeeId);
        Assert.Equal(970_000, byPayee[a].Net); Assert.Equal("PROCESSING", byPayee[a].Status);
        Assert.Equal(1_960_000, byPayee[b].Net); Assert.Equal("PROCESSING", byPayee[b].Status);
        Assert.Equal("HELD", byPayee[c].Status);
        Assert.Equal(2, Fake.PayoutCalls);
        foreach (var p in new[] { a, b, c })
        {
            var run = await RunOf(byPayee[p].RunId!.Value);
            Assert.Equal(p, run.PayeeId);
            Assert.All(await LinesOf(p), l => Assert.Equal(run.Id, l.PayoutId));
        }
        Assert.Equal(3, await Count("payout_runs"));
    }

    [PostgresMoneyFact]
    public async Task SamePeriodTwiceIsIdempotentAndCallsProviderOnce()
    {
        var payee = await NewPayee();
        await AddLine(payee, 1_000_000, 30_000);
        var first = await RunOne(payee);
        var second = await RunOne(payee);
        Assert.Equal("CREATED", first.Outcome);
        Assert.Equal("EXISTS", second.Outcome);
        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(1, await Count("payout_runs"));
        Assert.Equal(1, Fake.PayoutCalls);
        Assert.Equal(first.Net, second.Net);
        Assert.Single(second.Lines);
    }

    [PostgresMoneyFact]
    public async Task OverlappingOrEarlierPeriodConflictsButAdjacentWorks()
    {
        var payee = await NewPayee();
        await AddLine(payee, 1_000_000, 30_000);
        Assert.Equal("CREATED", (await RunOne(payee)).Outcome);
        Assert.Equal("CONFLICT", (await RunOne(payee, PStart.AddDays(2), PEnd.AddDays(2))).Outcome);          // chồng
        Assert.Equal("CONFLICT", (await RunOne(payee, PStart.AddDays(-10), PStart.AddDays(1))).Outcome);      // chồng đầu
        Assert.Equal("CONFLICT", (await RunOne(payee, PStart.AddDays(-10), PStart.AddDays(-5))).Outcome);     // đi trước kỳ đã có
        await AddLine(payee, 500_000, 15_000, availableAt: PEnd.AddDays(1));
        Assert.Equal("CREATED", (await RunOne(payee, PEnd, PEnd.AddDays(3))).Outcome);                         // liền kề
        Assert.Equal(2, await Count("payout_runs"));
    }

    [PostgresMoneyFact]
    public async Task EightConcurrentRunsCreateOneRunAndOneProviderCall()
    {
        var payee = await NewPayee();
        await AddLine(payee, 1_000_000, 30_000);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = Open();
            return (await Engine(db).RunAsync([payee], PStart, PEnd, AdminUser)).Items.Single();
        }));
        Assert.Equal(1, results.Count(r => r.Outcome == "CREATED"));
        Assert.Equal(7, results.Count(r => r.Outcome == "EXISTS"));
        Assert.Single(results.Select(r => r.RunId).Distinct());
        Assert.Equal(1, await Count("payout_runs"));
        Assert.Equal(1, Fake.PayoutCalls);
    }

    [PostgresMoneyFact]
    public async Task CarriedDebtIsDeductedFromNextRunExactlyOnce()
    {
        var payee = await NewPayee();
        await AddLine(payee, 100_000, 3_000);
        await AddLine(payee, 300_000, 9_000, "REFUND");
        var first = await RunOne(payee);
        Assert.Equal("CARRIED", first.Status);
        Assert.Equal(-194_000, first.Net);
        Assert.Equal(0, Fake.PayoutCalls);
        Assert.All(await LinesOf(payee), l => { Assert.Equal("PAID", l.State); Assert.Equal(first.RunId, l.PayoutId); });

        await AddLine(payee, 500_000, 15_000, availableAt: PEnd.AddDays(1));
        var second = await RunOne(payee, PEnd, PEnd.AddDays(3));
        Assert.Equal(194_000, second.CarriedDebt);
        Assert.Equal(485_000 - 194_000, second.Net);
        Assert.Equal("PROCESSING", second.Status);
        Assert.Equal(291_000, Fake.Calls.Single().Amount);
        Assert.Equal(1, (await LinesOf(payee)).Count(l => l.PayoutId == second.RunId)); // chỉ dòng mới; dòng cũ đã cấn trừ ở kỳ trước

        // kỳ thứ ba không còn nợ: nợ đã dùng hết ở kỳ hai
        await AddLine(payee, 200_000, 6_000, availableAt: PEnd.AddDays(4));
        var third = await RunOne(payee, PEnd.AddDays(3), PEnd.AddDays(5));
        Assert.Equal(0, third.CarriedDebt);
        Assert.Equal(194_000, third.Net);
    }

    [PostgresMoneyFact]
    public async Task DeferredReturnsLinesToAvailableAndNeverLosesOrDoubleCountsMoney()
    {
        var payee = await NewPayee();
        await AddLine(payee, 30_000, 0);
        var first = await RunOne(payee);
        Assert.Equal("DEFERRED", first.Status);
        Assert.Equal(30_000, first.Net);
        var lines = await LinesOf(payee);
        Assert.All(lines, l => { Assert.Equal("AVAILABLE", l.State); Assert.Null(l.PayoutId); });
        Assert.Equal(0, Fake.PayoutCalls);

        await AddLine(payee, 40_000, 0, availableAt: PEnd.AddDays(1));
        var second = await RunOne(payee, PEnd, PEnd.AddDays(3));
        Assert.Equal(70_000, second.Net); // 30.000 cũ + 40.000 mới, không đếm đôi
        Assert.Equal("PROCESSING", second.Status);
        Assert.Equal(70_000, Fake.Calls.Single().Amount);
        Assert.All(await LinesOf(payee), l => Assert.Equal(second.RunId, l.PayoutId));
    }

    [PostgresMoneyFact]
    public async Task DeferredRunKeepsCarriedDebtForTheNextRun()
    {
        var payee = await NewPayee();
        await AddLine(payee, 10_000, 0); await AddLine(payee, 30_000, 0, "REFUND");
        Assert.Equal("CARRIED", (await RunOne(payee)).Status); // net -20.000
        await AddLine(payee, 60_000, 0, availableAt: PEnd.AddDays(1));
        var second = await RunOne(payee, PEnd, PEnd.AddDays(3));
        Assert.Equal(20_000, second.CarriedDebt);
        Assert.Equal(40_000, second.Net);
        Assert.Equal("DEFERRED", second.Status);
        await AddLine(payee, 20_000, 0, availableAt: PEnd.AddDays(4));
        var third = await RunOne(payee, PEnd.AddDays(3), PEnd.AddDays(5));
        Assert.Equal(20_000, third.CarriedDebt); // nợ vẫn còn vì kỳ hai chưa chi
        Assert.Equal(80_000 - 20_000, third.Net);
        Assert.Equal("PROCESSING", third.Status);
        Assert.Equal(60_000, Fake.Calls.Single().Amount);
    }

    [PostgresMoneyFact]
    public async Task SettledMarksLinesPaidWithoutTransfer()
    {
        var payee = await NewPayee();
        await AddLine(payee, 100_000, 0); await AddLine(payee, 100_000, 0, "REFUND");
        var plan = await RunOne(payee);
        Assert.Equal("SETTLED", plan.Status);
        Assert.Equal(0, plan.Net);
        Assert.Equal(0, Fake.PayoutCalls);
        var run = await RunOf(plan.RunId!.Value);
        Assert.NotNull(run.PaidAt);
        Assert.All(await LinesOf(payee), l => { Assert.Equal("PAID", l.State); Assert.Equal(run.Id, l.PayoutId); });
    }

    [PostgresMoneyFact]
    public async Task AboveAutoLimitWaitsForApprovalThenApproveSubmits()
    {
        await SetConfig(MoneyConfigKeys.PayoutAutoMaxAmount, "100000");
        var payee = await NewPayee();
        await AddLine(payee, 500_000, 0);
        var plan = await RunOne(payee);
        Assert.Equal("AWAITING_APPROVAL", plan.Status);
        Assert.Equal(0, Fake.PayoutCalls);
        PayoutRun run;
        await using (var db = Open()) run = await Engine(db).ApproveAsync(plan.RunId!.Value, AdminUser);
        Assert.Equal("PROCESSING", run.Status);
        Assert.Equal(AdminUser, run.ApprovedBy);
        Assert.Equal(1, Fake.PayoutCalls);
        await using var db2 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db2).ApproveAsync(plan.RunId!.Value, AdminUser));
    }

    [PostgresMoneyFact]
    public async Task UnverifiedBankHoldsThenReleaseSubmitsOnlyAfterVerification()
    {
        var payee = await NewPayee(verified: false);
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee);
        Assert.Equal("HELD", plan.Status);
        Assert.Contains("chưa được Admin duyệt", plan.Reason);
        Assert.Equal(0, Fake.PayoutCalls);
        await using (var db = Open()) await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db).ReleaseAsync(plan.RunId!.Value, AdminUser));
        await using (var db = Open()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET bank_status = 'VERIFIED' WHERE id = {payee}");
        PayoutRun run;
        await using (var db = Open()) run = await Engine(db).ReleaseAsync(plan.RunId!.Value, AdminUser);
        Assert.Equal("PROCESSING", run.Status);
        Assert.Equal(1, Fake.PayoutCalls);
        Assert.Equal(200_000, Fake.Calls.Single().Amount);
        await using var db2 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db2).ReleaseAsync(plan.RunId!.Value, AdminUser)); // không còn HELD
    }

    [PostgresMoneyFact]
    public async Task VerifiedBankIsNotHeldEvenIfChangedJustNow()
    {
        // Không còn thời gian giữ sau khi đổi: chỉ cần VERIFIED (kể cả cột bank_changed_at cũ còn dữ liệu).
        var payee = await NewPayee(changedAt: T0.AddMinutes(-1));
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee);
        Assert.Equal("PROCESSING", plan.Status);
        Assert.Equal(1, Fake.PayoutCalls);
    }

    [PostgresMoneyFact]
    public async Task UnknownThenReconcileWithSameKeyEndsPaid()
    {
        var sim = NewSim(Clock);
        sim.InjectFailure(SimulatedFailure.UnknownRecorded);
        var payee = await NewPayee();
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee, provider: sim);
        Assert.Equal("UNKNOWN", plan.Status);
        var run = await RunOf(plan.RunId!.Value);
        Assert.Null(run.ProviderRef);
        Assert.Equal(1, run.Attempt);
        PayoutRun reconciled;
        await using (var db = Open()) reconciled = await Engine(db, sim).ReconcileAsync(run.Id, AdminUser);
        Assert.Equal("PAID", reconciled.Status);
        Assert.NotNull(reconciled.ProviderRef);
        Assert.NotNull(reconciled.PaidAt);
        Assert.Equal(PayoutStatus.Succeeded, (await sim.GetPayoutAsync(run.Code)).Status); // đúng khóa gốc, không có lệnh mới
        Assert.Equal(PayoutStatus.NotFound, (await sim.GetPayoutAsync(run.Code + "-2")).Status);
        Assert.All(await LinesOf(payee), l => Assert.Equal("PAID", l.State));
        Assert.Equal(100_000_000 - 200_000, await sim.GetBalanceAsync()); // chỉ trừ một lần
        await using var db2 = Open();
        Assert.Equal(2, await db2.Notifications.CountAsync(n => n.UserId == payee)); // kỳ mới + đã chi
    }

    [PostgresMoneyFact]
    public async Task DefiniteFailureFailsThenRetryUsesNewKeyWithSuffix2()
    {
        var sim = NewSim(Clock);
        sim.InjectFailure(SimulatedFailure.RejectAtSubmit);
        var payee = await NewPayee();
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee, provider: sim);
        Assert.Equal("FAILED", plan.Status);
        Assert.All(await LinesOf(payee), l => Assert.Equal("IN_PAYOUT", l.State));
        var code = plan.RunCode!;
        PayoutRun retried;
        await using (var db = Open()) retried = await Engine(db, sim).RetryAsync(plan.RunId!.Value, AdminUser);
        Assert.Equal("PROCESSING", retried.Status);
        Assert.Equal(2, retried.Attempt);
        Assert.NotNull(retried.ProviderRef);
        Assert.NotEqual(PayoutStatus.NotFound, (await sim.GetPayoutAsync(code + "-2")).Status);
        Assert.Equal(PayoutStatus.NotFound, (await sim.GetPayoutAsync(code)).Status);
        await using (var db = Open()) Assert.Equal("PAID", (await Engine(db, sim).ReconcileAsync(plan.RunId!.Value)).Status);
        await using var db3 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db3, sim).RetryAsync(plan.RunId!.Value, AdminUser)); // PAID không gửi lại
    }

    [PostgresMoneyFact]
    public async Task ProviderFailureAfterAcceptMarksFailedOnReconcile()
    {
        var sim = NewSim(Clock);
        sim.InjectFailure(SimulatedFailure.FailAfterAccept);
        var payee = await NewPayee();
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee, provider: sim);
        Assert.Equal("PROCESSING", plan.Status);
        await using var db = Open();
        var run = await Engine(db, sim).ReconcileAsync(plan.RunId!.Value);
        Assert.Equal("FAILED", run.Status);
        Assert.All(await LinesOf(payee), l => Assert.Equal("IN_PAYOUT", l.State));
    }

    [PostgresMoneyFact]
    public async Task InsufficientBalanceParksPendingWithoutCallingProviderThenRetrySameKey()
    {
        Fake.Balance = 1_000;
        var payee = await NewPayee();
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee);
        Assert.Equal("PENDING", plan.Status);
        Assert.Contains("Số dư", plan.Reason);
        Assert.Equal(0, Fake.PayoutCalls);
        await using (var db = Open())
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == AdminUser && n.Category == "PAYOUT"));
        Fake.Balance = 10_000_000;
        PayoutRun run;
        await using (var db = Open()) run = await Engine(db).RetryAsync(plan.RunId!.Value, AdminUser);
        Assert.Equal("PROCESSING", run.Status);
        Assert.Equal(1, run.Attempt);
        Assert.Equal(run.Code, Fake.Calls.Single().IdempotencyKey);
    }

    [PostgresMoneyFact]
    public async Task BalanceLookupErrorParksPendingAndNeverCalls()
    {
        Fake.BalanceThrows = true;
        var payee = await NewPayee();
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee);
        Assert.Equal("PENDING", plan.Status);
        Assert.Equal(0, Fake.PayoutCalls);
    }

    [PostgresMoneyFact]
    public async Task NotFoundBeforeTenMinutesWaitsThenPendingThenSameKeyRetry()
    {
        var sim = NewSim(Clock);
        sim.InjectFailure(SimulatedFailure.UnknownNotRecorded);
        var payee = await NewPayee();
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee, provider: sim);
        Assert.Equal("UNKNOWN", plan.Status);
        Clock.Advance(TimeSpan.FromMinutes(9));
        await using (var db = Open()) Assert.Equal("UNKNOWN", (await Engine(db, sim).ReconcileAsync(plan.RunId!.Value)).Status);
        Clock.Advance(TimeSpan.FromMinutes(2));
        await using (var db = Open()) Assert.Equal("PENDING", (await Engine(db, sim).ReconcileAsync(plan.RunId!.Value)).Status) ;
        // PENDING không được worker đụng tới; Admin gửi lại và dùng CÙNG khóa
        await using (var db = Open())
        {
            var run = await Engine(db, sim).RetryAsync(plan.RunId!.Value, AdminUser);
            Assert.Equal("PROCESSING", run.Status);
            Assert.Equal(1, run.Attempt);
        }
        Assert.NotEqual(PayoutStatus.NotFound, (await sim.GetPayoutAsync(plan.RunCode!)).Status);
        await using var db2 = Open();
        Assert.Equal("PAID", (await Engine(db2, sim).ReconcileAsync(plan.RunId!.Value)).Status);
    }

    [PostgresMoneyFact]
    public async Task MarkPaidManuallyGuards()
    {
        var a = await NewPayee("Người A"); var b = await NewPayee("Người B");
        await AddLine(a, 200_000, 0); await AddLine(b, 300_000, 0);
        var pa = await RunOne(a); var pb = await RunOne(b); // cả hai PROCESSING (provider giả chấp nhận)
        Assert.Equal("PROCESSING", pa.Status);
        await using (var db = Open()) await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db).MarkPaidManuallyAsync(pa.RunId!.Value, AdminUser, "BANK-1", null, confirmed: false));
        await using (var db = Open()) await Assert.ThrowsAsync<ArgumentException>(() => Engine(db).MarkPaidManuallyAsync(pa.RunId!.Value, AdminUser, "  ", null, confirmed: true));
        PayoutRun paid;
        await using (var db = Open()) paid = await Engine(db).MarkPaidManuallyAsync(pa.RunId!.Value, AdminUser, "BANK-1", "Chuyển tay", confirmed: true);
        Assert.Equal("PAID", paid.Status);
        Assert.Equal("BANK-1", paid.ProviderRef);
        Assert.Equal("MANUAL", paid.GatewayStatus);
        Assert.All(await LinesOf(a), l => Assert.Equal("PAID", l.State));
        await using (var db = Open()) await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db).MarkPaidManuallyAsync(pb.RunId!.Value, AdminUser, "BANK-1", null, true)); // provider_ref trùng
        await using (var db = Open()) await Assert.ThrowsAsync<DepotConflictException>(() => Engine(db).MarkPaidManuallyAsync(pa.RunId!.Value, AdminUser, "BANK-2", null, true)); // đã PAID
        Assert.Equal("PROCESSING", (await RunOf(pb.RunId!.Value)).Status);
        Assert.Equal(1, await CountSql("SELECT count(*) AS \"Value\" FROM audit_logs WHERE action = 'PAYOUT_MARKED_PAID'"));
    }

    [PostgresMoneyFact]
    public async Task MarkPaidOnHeldDoesNotNeedConfirm()
    {
        var payee = await NewPayee(verified: false);
        await AddLine(payee, 200_000, 0);
        var plan = await RunOne(payee);
        Assert.Equal("HELD", plan.Status);
        await using var db = Open();
        Assert.Equal("PAID", (await Engine(db).MarkPaidManuallyAsync(plan.RunId!.Value, AdminUser, "BANK-77", null, confirmed: false)).Status);
    }

    [PostgresMoneyFact]
    public async Task RunValidatesPeriodAndPayeeList()
    {
        var payee = await NewPayee();
        await using var db = Open();
        var engine = Engine(db);
        await Assert.ThrowsAsync<ArgumentException>(() => engine.RunAsync([payee], PEnd, PStart, AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.RunAsync([payee], PStart, T0.AddHours(1), AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.RunAsync([], PStart, PEnd, AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => engine.PreviewAsync(Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList(), PStart, PEnd));
        Assert.Equal("ERROR", (await engine.RunAsync([Guid.NewGuid()], PStart, PEnd, AdminUser)).Items.Single().Outcome);
        Assert.Equal("NO_LINES", (await engine.RunAsync([payee], PStart, PEnd, AdminUser)).Items.Single().Outcome);
    }

    [PostgresMoneyFact]
    public async Task InactivePayeeAndDisabledAutoAreSkipped()
    {
        var inactive = await NewPayee("Người khóa", active: false);
        var off = await NewPayee("Người tắt tự chi");
        await AddLine(inactive, 200_000, 0); await AddLine(off, 200_000, 0);
        await using (var db = Open()) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO payee_payout_settings (user_id, auto_enabled) VALUES ({off}, false)");
        Assert.Equal("SKIPPED", (await RunOne(inactive)).Outcome);
        Assert.Equal("SKIPPED", (await RunOne(off, autoOnly: true)).Outcome);
        Assert.Equal("CREATED", (await RunOne(off, autoOnly: false)).Outcome); // chạy thủ công bỏ qua công tắc
        Assert.Equal(1, await Count("payout_runs"));
    }

    // ============================ CHUYỂN THỬ ============================

    private async Task<TestTransferResult> Transfer(IPayoutProvider provider, string key, decimal amount, string bin = "970436", string account = "0123456789")
    {
        await using var db = Open();
        return await Engine(db, provider).TestTransferAsync(AdminUser, new(key, amount, bin, account));
    }

    [PostgresMoneyFact]
    public async Task TestTransferGuardrails()
    {
        const string key = "0a1b2c3d4e5f";
        await Assert.ThrowsAsync<InvalidOperationException>(() => Transfer(Fake, key, 2000)); // đang tắt
        await SetConfig(MoneyConfigKeys.PayoutTestEnabled, "true");
        await SetConfig(MoneyConfigKeys.PayoutTestMaxAmount, "10000");
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, key, 999));
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, key, 10_001));
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, key, 2000.5m));
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, key, 2000, bin: "12345"));
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, key, 2000, account: "12345"));
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, key, 2000, account: "1234567890123456789012"));
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(Fake, "not-hex!", 2000));
        Fake.Balance = 1500;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Transfer(Fake, key, 2000));
        Assert.Equal(0, Fake.PayoutCalls);
        Fake.Balance = 50_000;
        var ok = await Transfer(Fake, key, 2000);
        Assert.Equal("ACCEPTED", ok.Outcome);
        Assert.Equal("TEST-" + key, ok.Key);
        var call = Assert.Single(Fake.Calls);
        Assert.Equal("TEST-" + key, call.IdempotencyKey); Assert.Equal(2000, call.Amount); Assert.True(call.Description.Length <= 20);
        // không tạo kỳ chi, không ghi sổ cái
        Assert.Equal(0, await Count("payout_runs")); Assert.Equal(0, await Count("payable_lines"));
        // audit chỉ có 4 số cuối
        await using var db = Open();
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Action == "PAYOUT_TEST_TRANSFER");
        Assert.Contains("****6789", audit.NewData);
        Assert.DoesNotContain("0123456789", audit.NewData);
        Assert.Equal(AdminUser, audit.UserId);
    }

    [PostgresMoneyFact]
    public async Task TestTransferIsIdempotentByClientKeyAndStatusIsQueryable()
    {
        await SetConfig(MoneyConfigKeys.PayoutTestEnabled, "true");
        var sim = NewSim(Clock);
        var first = await Transfer(sim, "AABBCCDD11", 3000);
        var second = await Transfer(sim, "aabbccdd11", 3000); // cùng khóa (không phân biệt hoa thường)
        Assert.Equal("ACCEPTED", first.Outcome); Assert.Equal("ACCEPTED", second.Outcome);
        Assert.Equal(first.ProviderRef, second.ProviderRef);
        Assert.Equal(100_000_000 - 3000, await sim.GetBalanceAsync()); // chỉ trừ một lần
        await using var db = Open();
        var status = await Engine(db, sim).GetTestTransferStatusAsync("aabbccdd11");
        Assert.Equal("SUCCEEDED", status.Status);
        Assert.Equal("NOT_FOUND", (await Engine(db, sim).GetTestTransferStatusAsync("ffffffff")).Status);
        await Assert.ThrowsAsync<ArgumentException>(() => Engine(db, sim).GetTestTransferStatusAsync("zz"));
    }
}
