using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

public sealed class CommissionCalculatorTests
{
    [Theory]
    [InlineData(105, 10, 11)]      // 10.5 làm tròn nửa lên
    [InlineData(100, 5, 5)]
    [InlineData(0, 5, 0)]
    [InlineData(1000, 0, 0)]
    [InlineData(1000, 100, 1000)]
    [InlineData(99, 2.5, 2)]       // 2.475
    [InlineData(10, 5, 1)]         // 0.5 làm tròn lên
    [InlineData(1, 3, 0)]
    public void CommissionRoundsHalfUpToWholeDong(decimal gross, decimal rate, decimal expected) =>
        Assert.Equal(expected, CommissionCalculator.Commission(gross, rate));

    [Fact]
    public void FullRateTakesEverythingAndZeroGrossIsZero()
    {
        Assert.Equal(123456m, CommissionCalculator.Commission(123456, 100));
        Assert.Equal(0m, CommissionCalculator.Commission(0, 100));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(100, -0.01)]
    [InlineData(100, 100.01)]
    public void InvalidInputsAreRejected(decimal gross, decimal rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CommissionCalculator.Commission(gross, rate));

    [Fact]
    public void PayeeOverrideBeatsDefaultAndZeroOverrideIsHonoured()
    {
        Assert.Equal(7m, CommissionCalculator.ResolveRate(7m, 3m));
        Assert.Equal(3m, CommissionCalculator.ResolveRate(null, 3m));
        Assert.Equal(0m, CommissionCalculator.ResolveRate(0m, 3m));
    }

    [Fact]
    public void PartialRefundsOf33_33_34On100At5PercentSumToExactOriginalCommission()
    {
        var original = CommissionCalculator.Commission(100, 5);
        var c1 = CommissionCalculator.RefundCommission(100, original, 0, 0, 33);
        var c2 = CommissionCalculator.RefundCommission(100, original, 33, c1, 33);
        var c3 = CommissionCalculator.RefundCommission(100, original, 66, c1 + c2, 34);
        Assert.Equal([2m, 1m, 2m], new[] { c1, c2, c3 });
        Assert.Equal(original, c1 + c2 + c3);
    }

    [Fact]
    public void ManySmallRefundsNeverDriftFromOriginalCommission()
    {
        decimal gross = 1_000_003, rate = 3.7m;
        var original = CommissionCalculator.Commission(gross, rate);
        decimal refundedGross = 0, refundedCommission = 0;
        var step = 7_919m;
        while (refundedGross < gross)
        {
            var part = Math.Min(step, gross - refundedGross);
            refundedCommission += CommissionCalculator.RefundCommission(gross, original, refundedGross, refundedCommission, part);
            refundedGross += part;
        }
        Assert.Equal(original, refundedCommission);
    }

    [Fact]
    public void RefundOverOriginalOrNonPositiveIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CommissionCalculator.RefundCommission(100, 5, 90, 4, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommissionCalculator.RefundCommission(100, 5, 0, 0, 0));
    }
}

public sealed class SimulatedPayoutProviderTests
{
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (SimulatedPayoutProvider Provider, TestClock Clock) Build(decimal balance = 1_000_000)
    {
        var clock = new TestClock();
        return (new SimulatedPayoutProvider(new() { Delay = TimeSpan.FromSeconds(10), InitialBalance = balance }, clock), clock);
    }

    private static PayoutRequest Req(string reference = "PO-1", string key = "key-1", decimal amount = 100_000) =>
        new(reference, key, amount, "RETRACK PO-1", "970436", "0123456789");

    [Fact]
    public async Task SucceedsAfterConfiguredDelayAndDebitsBalanceOnce()
    {
        var (p, clock) = Build();
        var r = await p.PayoutAsync(Req());
        Assert.Equal(PayoutOutcome.Accepted, r.Outcome);
        Assert.Equal(PayoutStatus.Processing, (await p.GetPayoutAsync("PO-1")).Status);
        clock.Now += TimeSpan.FromSeconds(11);
        var done = await p.GetPayoutAsync(r.ProviderRef!);
        Assert.Equal(PayoutStatus.Succeeded, done.Status);
        Assert.Equal(900_000m, await p.GetBalanceAsync());
    }

    [Fact]
    public async Task SameIdempotencyKeyDoesNotPayTwice()
    {
        var (p, _) = Build();
        var first = await p.PayoutAsync(Req());
        var again = await p.PayoutAsync(Req());
        Assert.Equal(first.ProviderRef, again.ProviderRef);
        Assert.Equal(900_000m, await p.GetBalanceAsync());
        var other = await p.PayoutAsync(Req(amount: 5));
        Assert.Equal("IDEMPOTENCY_MISMATCH", other.ErrorCode);
    }

    [Fact]
    public async Task UnknownNotFoundAndInvalidRequestsAreClassified()
    {
        var (p, _) = Build(50_000);
        Assert.Equal(PayoutStatus.NotFound, (await p.GetPayoutAsync("nope")).Status);
        Assert.Equal("INSUFFICIENT_BALANCE", (await p.PayoutAsync(Req())).ErrorCode);
        Assert.Equal(PayoutOutcome.DefiniteFailure, (await p.PayoutAsync(Req() with { Description = new string('x', 21) })).Outcome);
        Assert.Equal(PayoutOutcome.DefiniteFailure, (await p.PayoutAsync(Req() with { ToBin = "12345" })).Outcome);
        Assert.Equal(PayoutOutcome.DefiniteFailure, (await p.PayoutAsync(Req() with { Amount = 0 })).Outcome);
        Assert.Equal(50_000m, await p.GetBalanceAsync());
    }

    [Fact]
    public async Task FailureInjectionCoversEveryClassification()
    {
        var (p, clock) = Build();
        p.InjectFailure(SimulatedFailure.RejectAtSubmit);
        Assert.Equal(PayoutOutcome.DefiniteFailure, (await p.PayoutAsync(Req())).Outcome);
        Assert.Equal(PayoutStatus.NotFound, (await p.GetPayoutAsync("PO-1")).Status);

        p.InjectFailure(SimulatedFailure.UnknownNotRecorded);
        Assert.Equal(PayoutOutcome.Unknown, (await p.PayoutAsync(Req())).Outcome);
        Assert.Equal(PayoutStatus.NotFound, (await p.GetPayoutAsync("PO-1")).Status);
        Assert.Equal(PayoutOutcome.Accepted, (await p.PayoutAsync(Req())).Outcome); // gửi lại cùng khóa chạy bình thường

        p.InjectFailure(SimulatedFailure.UnknownRecorded);
        var req2 = Req("PO-2", "key-2");
        Assert.Equal(PayoutOutcome.Unknown, (await p.PayoutAsync(req2)).Outcome);
        Assert.NotEqual(PayoutStatus.NotFound, (await p.GetPayoutAsync("PO-2")).Status);
        Assert.Equal(PayoutOutcome.Accepted, (await p.PayoutAsync(req2)).Outcome);
        Assert.Equal(800_000m, await p.GetBalanceAsync()); // PO-1 và PO-2 mỗi lệnh trừ đúng một lần

        p.InjectFailure(SimulatedFailure.FailAfterAccept);
        Assert.Equal(PayoutOutcome.Accepted, (await p.PayoutAsync(Req("PO-3", "key-3"))).Outcome);
        clock.Now += TimeSpan.FromSeconds(11);
        Assert.Equal(PayoutStatus.Failed, (await p.GetPayoutAsync("PO-3")).Status);
        Assert.Equal(PayoutStatus.Failed, (await p.GetPayoutAsync("PO-3")).Status);
        Assert.Equal(800_000m, await p.GetBalanceAsync()); // hoàn lại đúng một lần
    }
}
