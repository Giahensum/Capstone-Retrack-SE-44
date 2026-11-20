using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Money;
using Xunit;

namespace Retrack.API.Tests;

/// <summary>Nền test hoàn tiền: người trả có tài khoản đã duyệt, hàm dựng khoản thu và bất thường.</summary>
public abstract class RefundTestBase : PayoutTestBase
{
    protected Guid Seller;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Ready) return;
        await GiveBank(Payer, "VERIFIED");
        Seller = await NewPayee("Người bán");
    }

    protected RefundEngine Refunds(AppDbContext db, IPayoutProvider? provider = null) =>
        new(db, new MoneyConfigReader(db), new FixedResolver(provider ?? Fake), Clock, NullLogger<RefundEngine>.Instance);

    protected async Task GiveBank(Guid user, string? status, bool clear = false)
    {
        await using var db = Open();
        if (clear)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET bank_name = NULL, bank_bin = NULL, bank_account_number = NULL, bank_account_name = NULL, bank_status = NULL WHERE id = {user}");
        else
            await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE users SET bank_name = 'Vietcombank', bank_bin = '970436', bank_account_number = '0987654321', bank_account_name = 'NGUOI TRA', bank_status = {status} WHERE id = {user}");
    }

    protected async Task<Guid> NewCollection(string status = "PAID", decimal amount = 1_000_000, string flow = "PICKUP", Guid? payer = null, DateTime? createdAt = null)
    {
        await using var db = Open();
        var sourceType = flow == "BATCH" ? MoneySources.InventoryBatch : flow.StartsWith("LIQUIDATION") ? MoneySources.LiquidationAgreement : MoneySources.PickupRequest;
        var c = new MoneyCollection { Flow = flow, SourceType = sourceType, SourceId = Guid.NewGuid(), PayerId = payer ?? Payer, PayeeId = Seller, Amount = amount, Status = status,
            ReceivedAmount = amount, PaidAt = T0, CreatedAt = createdAt ?? T0 };
        db.MoneyCollections.Add(c); await db.SaveChangesAsync(); return c.Id;
    }

    protected async Task<Guid> NewAnomaly(Guid collection, string kind = "OVERPAID", decimal refund = 50_000, string status = "OPEN", DateTime? createdAt = null)
    {
        await using var db = Open();
        var a = new MoneyCollectionAnomaly { CollectionId = collection, OrderCode = 1, Kind = kind, ReceivedAmount = refund, ExpectedAmount = 1_000_000, RefundAmount = refund,
            Reference = "REF-" + Guid.NewGuid().ToString("N")[..10], Status = status, Note = "ghi nhận", CreatedAt = createdAt ?? T0 };
        db.MoneyCollectionAnomalies.Add(a); await db.SaveChangesAsync(); return a.Id;
    }

    protected async Task<MoneyRefund> RefundOf(Guid id)
    {
        await using var db = Open();
        return await db.MoneyRefunds.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    protected async Task<MoneyCollection> CollectionOf(Guid id)
    {
        await using var db = Open();
        return await db.MoneyCollections.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    protected async Task<MoneyCollectionAnomaly> AnomalyOf(Guid id)
    {
        await using var db = Open();
        return await db.MoneyCollectionAnomalies.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    protected async Task<MoneyRefund> CreateAnomalyRefund(Guid anomaly, decimal? amount = null, IPayoutProvider? provider = null)
    {
        await using var db = Open();
        return await Refunds(db, provider).CreateForAnomalyAsync(anomaly, amount, "Hoàn tiền theo yêu cầu", AdminUser);
    }

    protected async Task<MoneyRefund> Mutate(Func<RefundEngine, Task<MoneyRefund>> action, IPayoutProvider? provider = null)
    {
        await using var db = Open();
        return await action(Refunds(db, provider));
    }
}

public sealed class RefundEngineTests : RefundTestBase
{
    [PostgresMoneyFact]
    public async Task OverpaidRefundPaysExcessOnceUsesCodeAsKeyAndResolvesAnomaly()
    {
        var c = await NewCollection(amount: 1_000_000);
        var a = await NewAnomaly(c, "OVERPAID", 50_000);
        var refund = await CreateAnomalyRefund(a);
        Assert.Equal("PROCESSING", refund.Status);
        Assert.Matches(@"^RF-\d{6}-[0-9A-F]{8}$", refund.Code);
        Assert.Equal("EXCESS", refund.Kind);
        Assert.Equal(50_000, refund.Amount);
        var call = Assert.Single(Fake.Calls);
        Assert.Equal(refund.Code, call.IdempotencyKey);
        Assert.Equal(refund.Code, call.ReferenceId);
        Assert.Equal(50_000, call.Amount);
        Assert.StartsWith("RETRACK HOAN ", call.Description);
        Assert.True(call.Description.Length <= 20);
        Assert.Equal("970436", call.ToBin);
        Assert.Equal(1, Fake.BalanceCalls);

        // đối soát: Succeeded → PAID, bất thường tự đóng, khoản thu không đổi, sổ cái không có dòng nào
        Fake.Status = new(PayoutStatus.Succeeded, "FAKE-REF-1");
        var paid = await Mutate(e => e.ReconcileAsync(refund.Id, AdminUser));
        Assert.Equal("PAID", paid.Status);
        Assert.NotNull(paid.PaidAt);
        var anomaly = await AnomalyOf(a);
        Assert.Equal("RESOLVED", anomaly.Status);
        Assert.Contains(refund.Code, anomaly.Note);
        Assert.Equal("PAID", (await CollectionOf(c)).Status);
        await using var db = Open();
        Assert.Equal(0, await db.PayableLines.CountAsync());
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.UserId == Payer && n.Category == "REFUND")); // tạo + đã hoàn
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "REFUND_CREATED" && x.EntityId == refund.Id && x.UserId == AdminUser));
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "REFUND_PAID" && x.EntityId == refund.Id));
        Assert.Equal(1, Fake.PayoutCalls); // đối soát không gửi lại
    }

    [PostgresMoneyFact]
    public async Task EveryAnomalyKindMapsToItsRefundKindAndSourceMismatchEndsRefunded()
    {
        Fake.Status = new(PayoutStatus.Succeeded, null);
        foreach (var (kind, expected, collectionStatus) in new[]
        {
            ("OVERPAID", "EXCESS", "PAID"), ("DUPLICATE_PAYMENT", "DUPLICATE", "PAID"), ("LATE_PAYMENT", "LATE", "EXPIRED"), ("UNDERPAID", "FULL", "PENDING"),
        })
        {
            var c = await NewCollection(collectionStatus, 1_000_000);
            var a = await NewAnomaly(c, kind, 40_000);
            var r = await CreateAnomalyRefund(a);
            Assert.Equal(expected, r.Kind);
            await Mutate(e => e.ReconcileAsync(r.Id));
            Assert.Equal("PAID", (await RefundOf(r.Id)).Status);
            Assert.Equal("RESOLVED", (await AnomalyOf(a)).Status);
            Assert.Equal(collectionStatus, (await CollectionOf(c)).Status); // trạng thái khoản thu không bị đổi
        }
        // SOURCE_MISMATCH: khoản thu REFUNDING, hoàn đủ → REFUNDED
        var cm = await NewCollection("REFUNDING", 1_000_000);
        var am = await NewAnomaly(cm, "SOURCE_MISMATCH", 1_000_000);
        var rm = await CreateAnomalyRefund(am);
        Assert.Equal("FULL", rm.Kind);
        Assert.Equal("REFUNDING", (await CollectionOf(cm)).Status);
        await Mutate(e => e.ReconcileAsync(rm.Id));
        Assert.Equal("REFUNDED", (await CollectionOf(cm)).Status);
        // khoản thu có bất thường nguồn đổi đang mở: không cho hoàn trực tiếp để tránh hoàn trùng
        var c2 = await NewCollection("REFUNDING", 500_000);
        await NewAnomaly(c2, "SOURCE_MISMATCH", 500_000);
        await using var db = Open();
        var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForCollectionAsync(c2, "FULL", null, "Hoàn trực tiếp khoản thu", AdminUser));
        Assert.Contains("Bất thường", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresMoneyFact]
    public async Task AnomalyRefundValidatesAmountReasonAndStatus()
    {
        var c = await NewCollection();
        var a = await NewAnomaly(c, "OVERPAID", 50_000);
        await using (var db = Open())
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).CreateForAnomalyAsync(a, 50_001, "Lý do hợp lệ", AdminUser));
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).CreateForAnomalyAsync(a, 0, "Lý do hợp lệ", AdminUser));
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).CreateForAnomalyAsync(a, 100.5m, "Lý do hợp lệ", AdminUser));
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).CreateForAnomalyAsync(a, null, "abc", AdminUser));
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).CreateForAnomalyAsync(a, null, null, AdminUser));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Refunds(db).CreateForAnomalyAsync(Guid.NewGuid(), null, "Lý do hợp lệ", AdminUser));
        }
        Assert.Equal(0, Fake.PayoutCalls);
        var partial = await CreateAnomalyRefund(a, 30_000); // hoàn một phần số tiền của bất thường là được
        Assert.Equal(30_000, partial.Amount);
        var resolved = await NewAnomaly(c, "OVERPAID", 10_000, status: "RESOLVED");
        await using var db2 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db2).CreateForAnomalyAsync(resolved, null, "Lý do hợp lệ", AdminUser));
    }

    [PostgresMoneyFact]
    public async Task UnverifiedOrMissingPayerBankIsRefusedWithVietnameseMessageAndNoProviderCall()
    {
        foreach (var status in new[] { "PENDING_VERIFICATION", "REJECTED", null })
        {
            await GiveBank(Payer, status);
            var c = await NewCollection();
            var a = await NewAnomaly(c);
            await using var db = Open();
            var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForAnomalyAsync(a, null, "Hoàn tiền dư", AdminUser));
            Assert.Contains("Admin duyệt", ex.Message);
            Assert.Contains("nhập tài khoản ngân hàng", ex.Message);
            await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForCollectionAsync(c, "FULL", null, "Hoàn đơn bị hủy", AdminUser));
        }
        await GiveBank(Payer, null, clear: true);
        var c2 = await NewCollection();
        var a2 = await NewAnomaly(c2);
        await using (var db = Open()) await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForAnomalyAsync(a2, null, "Hoàn tiền dư", AdminUser));
        await GiveBank(Payer, "VERIFIED");
        await SetInactive(Payer);
        var c3 = await NewCollection();
        var a3 = await NewAnomaly(c3);
        await using var db3 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db3).CreateForAnomalyAsync(a3, null, "Hoàn tiền dư", AdminUser));
        Assert.Equal(0, Fake.PayoutCalls);
        Assert.Equal(0, await db3.MoneyRefunds.CountAsync());
    }

    private async Task SetInactive(Guid user)
    {
        await using var db = Open();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET is_active = FALSE WHERE id = {user}");
    }

    [PostgresMoneyFact]
    public async Task InsufficientBalanceLeavesPendingWithoutProviderCallThenRetriesWithSameKey()
    {
        Fake.Balance = 10_000;
        var a = await NewAnomaly(await NewCollection(), "OVERPAID", 50_000);
        var refund = await CreateAnomalyRefund(a);
        Assert.Equal("PENDING", refund.Status);
        Assert.Contains("Số dư", refund.FailReason);
        Assert.Equal(0, Fake.PayoutCalls);
        await using (var db = Open()) Assert.True(await db.Notifications.AnyAsync(n => n.UserId == AdminUser && n.Category == "REFUND")); // báo Admin
        Fake.Balance = 1_000_000;
        var retried = await Mutate(e => e.RetryAsync(refund.Id, AdminUser));
        Assert.Equal("PROCESSING", retried.Status);
        Assert.Equal(1, retried.Attempt); // PENDING: gửi lại CÙNG khóa
        Assert.Equal(refund.Code, Assert.Single(Fake.Calls).IdempotencyKey);
        // không kiểm tra được số dư: cũng không gọi chi
        var other = await NewAnomaly(await NewCollection(), "OVERPAID", 20_000);
        Fake.BalanceThrows = true;
        var p2 = await CreateAnomalyRefund(other);
        Assert.Equal("PENDING", p2.Status);
        Assert.Equal(1, Fake.PayoutCalls);
    }

    [PostgresMoneyFact]
    public async Task UnknownOutcomeGoesUnknownThenReconcilesToPaidWithSameKey()
    {
        var sim = new SimulatedPayoutProvider(new SimulatedPayoutOptions { Delay = TimeSpan.Zero }, Clock);
        sim.InjectFailure(SimulatedFailure.UnknownRecorded);
        var a = await NewAnomaly(await NewCollection(), "DUPLICATE_PAYMENT", 70_000);
        var refund = await CreateAnomalyRefund(a, provider: sim);
        Assert.Equal("UNKNOWN", refund.Status);
        Assert.Null(refund.ProviderRef);
        Assert.Equal(1, refund.Attempt);
        var paid = await Mutate(e => e.ReconcileAsync(refund.Id, AdminUser), sim);
        Assert.Equal("PAID", paid.Status);
        Assert.NotNull(paid.ProviderRef);
        Assert.Equal(PayoutStatus.Succeeded, (await sim.GetPayoutAsync(refund.Code)).Status); // đúng khóa gốc
        Assert.Equal(PayoutStatus.NotFound, (await sim.GetPayoutAsync(refund.Code + "-2")).Status);
        Assert.Equal(100_000_000 - 70_000, await sim.GetBalanceAsync()); // chỉ trừ một lần
        await using var db = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db, sim).RetryAsync(refund.Id, AdminUser)); // đã xong: không gửi lại
    }

    [PostgresMoneyFact]
    public async Task UnknownNotFoundAfterTenMinutesGoesPendingAndResendsSameKey()
    {
        Fake.Default = new(PayoutOutcome.Unknown, null, "TIMEOUT");
        var a = await NewAnomaly(await NewCollection(), "OVERPAID", 60_000);
        var refund = await CreateAnomalyRefund(a);
        Assert.Equal("UNKNOWN", refund.Status);
        Fake.Status = new(PayoutStatus.NotFound, null);
        Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("UNKNOWN", (await Mutate(e => e.ReconcileAsync(refund.Id))).Status); // chưa đủ 10 phút
        Clock.Advance(TimeSpan.FromMinutes(6));
        var pending = await Mutate(e => e.ReconcileAsync(refund.Id));
        Assert.Equal("PENDING", pending.Status);
        Fake.Default = new(PayoutOutcome.Accepted, "FAKE-REF");
        var again = await Mutate(e => e.RetryAsync(refund.Id, AdminUser));
        Assert.Equal("PROCESSING", again.Status);
        Assert.Equal(1, again.Attempt);
        Assert.Equal([refund.Code, refund.Code], Fake.Calls.Select(x => x.IdempotencyKey).ToArray());
    }

    [PostgresMoneyFact]
    public async Task DefiniteFailureGoesFailedThenRetryUsesSuffixedKey()
    {
        Fake.Enqueue(new(PayoutOutcome.DefiniteFailure, null, "BAD_ACCOUNT", "Sai tài khoản"));
        var a = await NewAnomaly(await NewCollection(), "OVERPAID", 80_000);
        var refund = await CreateAnomalyRefund(a);
        Assert.Equal("FAILED", refund.Status);
        Assert.Contains("Sai tài khoản", refund.FailReason);
        await using (var db = Open())
        {
            Assert.True(await db.Notifications.AnyAsync(n => n.UserId == Payer && n.Title.Contains("chưa thành công")));
            // đang FAILED: không cho tạo thêm lệnh khác cho cùng bất thường
            await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForAnomalyAsync(a, null, "Tạo lại lệnh hoàn", AdminUser));
        }
        var retry = await Mutate(e => e.RetryAsync(refund.Id, AdminUser));
        Assert.Equal("PROCESSING", retry.Status);
        Assert.Equal(2, retry.Attempt);
        Assert.Equal([refund.Code, refund.Code + "-2"], Fake.Calls.Select(x => x.IdempotencyKey).ToArray());
        // hủy lệnh FAILED khác rồi tạo lại được
        Fake.Enqueue(new(PayoutOutcome.DefiniteFailure, null, "BAD_ACCOUNT", "Sai tài khoản"));
        var a2 = await NewAnomaly(await NewCollection(), "OVERPAID", 20_000);
        var failed = await CreateAnomalyRefund(a2);
        Assert.Equal("FAILED", failed.Status);
        Assert.Equal("CANCELLED", (await Mutate(e => e.CancelAsync(failed.Id, AdminUser, "Admin hủy"))).Status);
        var recreated = await CreateAnomalyRefund(a2);
        Assert.Equal("PROCESSING", recreated.Status);
        Assert.NotEqual(failed.Code, recreated.Code);
    }

    [PostgresMoneyFact]
    public async Task AboveAutoThresholdWaitsForApprovalThenApproveSends()
    {
        await SetConfig(MoneyConfigKeys.PayoutAutoMaxAmount, "100000");
        var a = await NewAnomaly(await NewCollection(), "OVERPAID", 200_000);
        var refund = await CreateAnomalyRefund(a);
        Assert.Equal("AWAITING_APPROVAL", refund.Status);
        Assert.Contains("Admin duyệt", refund.FailReason);
        Assert.Equal(0, Fake.PayoutCalls);
        var approved = await Mutate(e => e.ApproveAsync(refund.Id, AdminUser));
        Assert.Equal("PROCESSING", approved.Status);
        Assert.Equal(AdminUser, approved.ApprovedBy);
        Assert.Equal(1, Fake.PayoutCalls);
        await using var db = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).ApproveAsync(refund.Id, AdminUser)); // không còn chờ duyệt
        // đúng ngưỡng thì tự chi
        var b = await NewAnomaly(await NewCollection(), "OVERPAID", 100_000);
        Assert.Equal("PROCESSING", (await CreateAnomalyRefund(b)).Status);
        // duyệt khi tài khoản người trả không còn VERIFIED: 409, chưa gửi
        var c = await NewAnomaly(await NewCollection(), "OVERPAID", 300_000);
        var waiting = await CreateAnomalyRefund(c);
        await GiveBank(Payer, "PENDING_VERIFICATION");
        var calls = Fake.PayoutCalls;
        await using var db2 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db2).ApproveAsync(waiting.Id, AdminUser));
        Assert.Equal(calls, Fake.PayoutCalls);
        Assert.Equal("AWAITING_APPROVAL", (await RefundOf(waiting.Id)).Status);
    }

    [PostgresMoneyFact]
    public async Task MarkPaidGuardsRequireConfirmUniqueReferenceAndRunningState()
    {
        var a1 = await NewAnomaly(await NewCollection(), "OVERPAID", 50_000);
        var running = await CreateAnomalyRefund(a1); // PROCESSING
        await using (var db = Open())
        {
            await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).MarkPaidManuallyAsync(running.Id, AdminUser, "BANK-001", "đã chuyển", confirmed: false));
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).MarkPaidManuallyAsync(running.Id, AdminUser, "  ", null, confirmed: true));
            await Assert.ThrowsAsync<ArgumentException>(() => Refunds(db).MarkPaidManuallyAsync(running.Id, AdminUser, new string('x', 101), null, confirmed: true));
        }
        var marked = await Mutate(e => e.MarkPaidManuallyAsync(running.Id, AdminUser, "BANK-001", "Đã chuyển khoản tay", confirmed: true));
        Assert.Equal("MANUAL_PAID", marked.Status);
        Assert.Equal("BANK-001", marked.ProviderRef);
        Assert.Equal("MANUAL", marked.GatewayStatus);
        Assert.Equal("RESOLVED", (await AnomalyOf(a1)).Status);
        await using (var db = Open())
        {
            await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).MarkPaidManuallyAsync(running.Id, AdminUser, "BANK-002", null, confirmed: true)); // đã xong
            Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "REFUND_MARKED_PAID" && x.EntityId == running.Id));
        }
        // mã giao dịch phải duy nhất; lệnh FAILED/PENDING không cần confirm
        Fake.Enqueue(new(PayoutOutcome.DefiniteFailure, null, "X", "Từ chối"));
        var failed = await CreateAnomalyRefund(await NewAnomaly(await NewCollection(), "OVERPAID", 30_000));
        Assert.Equal("FAILED", failed.Status);
        await using (var db = Open()) await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).MarkPaidManuallyAsync(failed.Id, AdminUser, "BANK-001", null, confirmed: false));
        Assert.Equal("MANUAL_PAID", (await Mutate(e => e.MarkPaidManuallyAsync(failed.Id, AdminUser, "BANK-777", null, confirmed: false))).Status);
        // đã hủy: không đánh dấu được
        await SetConfig(MoneyConfigKeys.PayoutAutoMaxAmount, "1");
        var waiting = await CreateAnomalyRefund(await NewAnomaly(await NewCollection(), "OVERPAID", 20_000));
        await Mutate(e => e.CancelAsync(waiting.Id, AdminUser, null));
        await using var db3 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db3).MarkPaidManuallyAsync(waiting.Id, AdminUser, "BANK-888", null, confirmed: true));
    }

    [PostgresMoneyFact]
    public async Task EightConcurrentRequestsForTheSameAnomalyCreateOneRefundAndOneProviderCall()
    {
        var a = await NewAnomaly(await NewCollection(), "DUPLICATE_PAYMENT", 90_000);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await using var db = Open(); return ((MoneyRefund?)await Refunds(db).CreateForAnomalyAsync(a, null, "Hoàn tiền trùng", AdminUser), (Exception?)null); }
            catch (Exception ex) { return ((MoneyRefund?)null, (Exception?)ex); }
        }));
        Assert.Equal(1, results.Count(r => r.Item2 == null));
        Assert.All(results.Where(r => r.Item2 != null), r => Assert.IsType<DepotConflictException>(r.Item2));
        await using var check = Open();
        Assert.Equal(1, await check.MoneyRefunds.CountAsync(r => r.AnomalyId == a));
        Assert.Equal(1, Fake.PayoutCalls);
    }

    [PostgresMoneyFact]
    public async Task ConcurrentPartialRefundsNeverExceedCollectionAmountAndKeepLedgerInStep()
    {
        var line = await AddLine(Seller, 1_000_000, 30_000, state: "AVAILABLE");
        Guid c;
        await using (var db = Open()) c = await db.PayableLines.Where(l => l.Id == line).Select(l => l.CollectionId).SingleAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await using var db = Open(); return ((MoneyRefund?)await Refunds(db).CreateForCollectionAsync(c, "PARTIAL", 300_000, "Trả hàng một phần", AdminUser), (Exception?)null); }
            catch (Exception ex) { return ((MoneyRefund?)null, (Exception?)ex); }
        }));
        Assert.Equal(3, results.Count(r => r.Item2 == null)); // 3 x 300.000 = 900.000; lần thứ 4 vượt phần còn lại 100.000
        Assert.All(results.Where(r => r.Item2 != null), r => Assert.True(r.Item2 is ArgumentException or DepotConflictException, r.Item2!.Message));
        await using var check = Open();
        Assert.Equal(900_000, await check.MoneyRefunds.Where(r => r.CollectionId == c && r.Status != "CANCELLED").SumAsync(r => r.Amount));
        var refundLines = await check.PayableLines.AsNoTracking().Where(l => l.Kind == "REFUND").ToListAsync();
        Assert.Equal(3, refundLines.Count);
        Assert.Equal(900_000, refundLines.Sum(l => l.Gross));
        Assert.Equal(27_000, refundLines.Sum(l => l.Commission)); // 3% của 900.000: cộng dồn khớp tuyệt đối
        Assert.Equal(3, Fake.PayoutCalls);
        Assert.Equal("PAID", (await CollectionOf(c)).Status); // hoàn dở dang: nguồn còn hiệu lực
        // phần còn lại 100.000 hoàn được, sau đó khoản thu sang REFUNDING và không hoàn thêm được
        await using var db2 = Open();
        var last = await Refunds(db2).CreateForCollectionAsync(c, "FULL", null, "Hoàn nốt phần còn lại", AdminUser);
        Assert.Equal(100_000, last.Amount);
        Assert.Equal("REFUNDING", (await CollectionOf(c)).Status);
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db2).CreateForCollectionAsync(c, "PARTIAL", 1, "Hoàn thêm", AdminUser));
    }

    [PostgresMoneyFact]
    public async Task FullRefundBeforePayoutCreatesRefundLineAndNetArithmeticIsRight()
    {
        var line = await AddLine(Seller, 1_000_000, 30_000, state: "AVAILABLE");
        Guid c;
        await using (var db = Open()) c = await db.PayableLines.Where(l => l.Id == line).Select(l => l.CollectionId).SingleAsync();
        var first = await Mutate(e => e.CreateForCollectionAsync(c, "PARTIAL", 400_000, "Khách trả một phần hàng", AdminUser));
        Assert.Equal("PARTIAL", first.Kind);
        var second = await Mutate(e => e.CreateForCollectionAsync(c, "FULL", null, "Trả nốt phần còn lại", AdminUser));
        Assert.Equal(600_000, second.Amount);
        Assert.Equal("FULL", second.Kind);
        var lines = (await LinesOf(Seller)).Where(l => l.Kind == "REFUND").OrderBy(l => l.Gross).ToList();
        Assert.Equal([400_000m, 600_000m], lines.Select(l => l.Gross).ToArray());
        Assert.Equal([12_000m, 18_000m], lines.Select(l => l.Commission).ToArray()); // tỷ lệ tích lũy: tổng đúng 30.000
        Assert.All(lines, l => { Assert.Equal("AVAILABLE", l.State); Assert.Equal(c, l.CollectionId); });
        Assert.Equal(new[] { first.Code, second.Code }.OrderBy(x => x), lines.Select(l => l.RefundKey!).OrderBy(x => x));
        Assert.Equal("REFUNDING", (await CollectionOf(c)).Status);

        // kỳ chi: 970.000 - 388.000 - 582.000 = 0 → SETTLED, không chuyển tiền cho người bán
        var payoutProvider = new ControllableProvider();
        var plan = await RunOne(Seller, PStart, T0, payoutProvider);
        Assert.Equal("SETTLED", plan.Status);
        Assert.Equal(0, plan.Net);
        Assert.Equal(970_000, plan.Gross - plan.Commission);
        Assert.Equal(970_000, plan.RefundAmount);
        Assert.Equal(0, payoutProvider.PayoutCalls);

        // hoàn xong → REFUNDED
        Fake.Status = new(PayoutStatus.Succeeded, null);
        await Mutate(e => e.ReconcileAsync(first.Id));
        Assert.Equal("REFUNDING", (await CollectionOf(c)).Status); // mới hoàn xong 400.000
        await Mutate(e => e.ReconcileAsync(second.Id));
        Assert.Equal("REFUNDED", (await CollectionOf(c)).Status);
    }

    [PostgresMoneyFact]
    public async Task PartialRefundReducesNextPayoutByNetOfProportionalCommission()
    {
        var line = await AddLine(Seller, 1_000_000, 30_000, state: "AVAILABLE");
        Guid c;
        await using (var db = Open()) c = await db.PayableLines.Where(l => l.Id == line).Select(l => l.CollectionId).SingleAsync();
        await Mutate(e => e.CreateForCollectionAsync(c, "PARTIAL", 400_000, "Khách trả một phần hàng", AdminUser));
        Assert.Equal("PAID", (await CollectionOf(c)).Status);
        var payoutProvider = new ControllableProvider();
        var plan = await RunOne(Seller, PStart, T0, payoutProvider);
        Assert.Equal("PROCESSING", plan.Status);
        Assert.Equal(582_000, plan.Net); // 970.000 - (400.000 - 12.000)
        Assert.Equal(582_000, payoutProvider.Calls.Single().Amount);
    }

    [PostgresMoneyFact]
    public async Task RefundAfterSaleLineIsInPayoutOrPaidIsRefused()
    {
        var line = await AddLine(Seller, 1_000_000, 30_000, state: "AVAILABLE");
        Guid c;
        await using (var db = Open()) c = await db.PayableLines.Where(l => l.Id == line).Select(l => l.CollectionId).SingleAsync();
        var payoutProvider = new ControllableProvider();
        var plan = await RunOne(Seller, PStart, PEnd, payoutProvider);
        Assert.Equal("PROCESSING", plan.Status);
        await using (var db = Open())
        {
            var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForCollectionAsync(c, "FULL", null, "Hoàn đơn đã vào kỳ chi", AdminUser));
            Assert.Contains("kỳ chi", ex.Message);
        }
        payoutProvider.Status = new(PayoutStatus.Succeeded, null);
        await using (var db = Open()) await Engine(db, payoutProvider).ReconcileAsync(plan.RunId!.Value, AdminUser);
        Assert.Equal("PAID", (await LinesOf(Seller)).Single().State);
        await using (var db = Open())
            await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db).CreateForCollectionAsync(c, "PARTIAL", 100_000, "Hoàn đơn đã chi", AdminUser));
        await using var check = Open();
        Assert.Equal(0, await check.MoneyRefunds.CountAsync());
        Assert.Equal(1, await check.PayableLines.CountAsync()); // không có dòng REFUND nào
        Assert.Equal("PAID", (await CollectionOf(c)).Status);
    }

    [PostgresMoneyFact]
    public async Task CollectionRefundValidatesKindAmountStatusAndLimits()
    {
        var c = await NewCollection("PAID", 500_000);
        await using var db = Open();
        var e = Refunds(db);
        await Assert.ThrowsAsync<ArgumentException>(() => e.CreateForCollectionAsync(c, "EXCESS", 1, "Lý do hợp lệ", AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => e.CreateForCollectionAsync(c, "PARTIAL", null, "Lý do hợp lệ", AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => e.CreateForCollectionAsync(c, "PARTIAL", 500_001, "Lý do hợp lệ", AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => e.CreateForCollectionAsync(c, "PARTIAL", -5, "Lý do hợp lệ", AdminUser));
        await Assert.ThrowsAsync<ArgumentException>(() => e.CreateForCollectionAsync(c, "FULL", 100_000, "Lý do hợp lệ", AdminUser)); // FULL phải đúng số còn lại
        await Assert.ThrowsAsync<ArgumentException>(() => e.CreateForCollectionAsync(c, "FULL", null, "ngắn", AdminUser));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => e.CreateForCollectionAsync(Guid.NewGuid(), "FULL", null, "Lý do hợp lệ", AdminUser));
        foreach (var status in new[] { "PENDING", "EXPIRED", "CANCELLED", "REFUNDED" })
            await Assert.ThrowsAsync<DepotConflictException>(() => e.CreateForCollectionAsync(NewCollection(status, 100_000).GetAwaiter().GetResult(), "FULL", null, "Lý do hợp lệ", AdminUser));
        Assert.Equal(0, Fake.PayoutCalls);
        var ok = await e.CreateForCollectionAsync(c, "FULL", 500_000, "Đơn bị hủy hoàn toàn", AdminUser); // amount trùng số còn lại cũng được
        Assert.Equal(500_000, ok.Amount);
        Assert.Equal("PROCESSING", ok.Status);
    }

    [PostgresMoneyFact]
    public async Task CancelReversesLedgerLineRestoresCollectionAndIsBlockedOnceInPayout()
    {
        await SetConfig(MoneyConfigKeys.PayoutAutoMaxAmount, "1");
        var line = await AddLine(Seller, 1_000_000, 30_000, state: "AVAILABLE");
        Guid c;
        await using (var db = Open()) c = await db.PayableLines.Where(l => l.Id == line).Select(l => l.CollectionId).SingleAsync();
        var waiting = await Mutate(e => e.CreateForCollectionAsync(c, "FULL", null, "Đơn bị hủy hoàn toàn", AdminUser));
        Assert.Equal("AWAITING_APPROVAL", waiting.Status);
        Assert.Equal("REFUNDING", (await CollectionOf(c)).Status);
        var cancelled = await Mutate(e => e.CancelAsync(waiting.Id, AdminUser, "Nhập nhầm"));
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Equal("PAID", (await CollectionOf(c)).Status);
        Assert.Equal("REVERSED", (await LinesOf(Seller)).Single(l => l.Kind == "REFUND").State);
        // dòng REVERSED không vào kỳ chi: người bán vẫn nhận đủ 970.000
        var plan = await RunOne(Seller, PStart, T0, new ControllableProvider());
        Assert.Equal(970_000, plan.Net);
        // đang chạy thì không hủy được
        await SetConfig(MoneyConfigKeys.PayoutAutoMaxAmount, "5000000");
        var c2 = await NewCollection("PAID", 200_000);
        var running = await Mutate(e => e.CreateForCollectionAsync(c2, "FULL", null, "Đơn bị hủy hoàn toàn", AdminUser));
        Assert.Equal("PROCESSING", running.Status);
        await using var db2 = Open();
        await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db2).CancelAsync(running.Id, AdminUser, null));
    }

    [PostgresMoneyFact]
    public async Task CancelIsRefusedWhenRefundLineAlreadyInPayout()
    {
        await SetConfig(MoneyConfigKeys.PayoutAutoMaxAmount, "1");
        var line = await AddLine(Seller, 1_000_000, 30_000, state: "AVAILABLE");
        Guid c;
        await using (var db = Open()) c = await db.PayableLines.Where(l => l.Id == line).Select(l => l.CollectionId).SingleAsync();
        var waiting = await Mutate(e => e.CreateForCollectionAsync(c, "PARTIAL", 100_000, "Khách trả một phần", AdminUser));
        await using (var db = Open()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payable_lines SET state = 'IN_PAYOUT' WHERE kind = 'REFUND' AND source_id = (SELECT source_id FROM money_collections WHERE id = {c})");
        await using var db2 = Open();
        var ex = await Assert.ThrowsAsync<DepotConflictException>(() => Refunds(db2).CancelAsync(waiting.Id, AdminUser, null));
        Assert.Contains("kỳ chi", ex.Message);
        Assert.Equal("AWAITING_APPROVAL", (await RefundOf(waiting.Id)).Status);
    }

    [PostgresMoneyFact]
    public async Task RefundReconcileWorkerPollsOnlyOldProcessingAndUnknownRefunds()
    {
        var oldOne = await CreateAnomalyRefund(await NewAnomaly(await NewCollection(), "OVERPAID", 50_000)); // PROCESSING, lần gửi lúc T0
        Clock.Advance(TimeSpan.FromMinutes(5));
        var freshOne = await CreateAnomalyRefund(await NewAnomaly(await NewCollection(), "OVERPAID", 50_000)); // vừa gửi
        Fake.Status = new(PayoutStatus.Succeeded, null);
        var s = new ServiceCollection();
        s.AddLogging(); s.AddSingleton<TimeProvider>(Clock); s.AddDbContext<AppDbContext>(o => o.UseNpgsql(Connection));
        s.AddScoped<MoneyConfigReader>(); s.AddScoped<IPayoutProviderResolver>(_ => new FixedResolver(Fake)); s.AddScoped<RefundEngine>();
        await using var sp = s.BuildServiceProvider();
        var worker = new RefundReconcileWorker(sp.GetRequiredService<IServiceScopeFactory>(), Options.Create(new PayoutWorkerOptions()), NullLogger<RefundReconcileWorker>.Instance);
        Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(1, await worker.RunOnceAsync(default));
        Assert.Equal("PAID", (await RefundOf(oldOne.Id)).Status);
        Assert.Equal("PROCESSING", (await RefundOf(freshOne.Id)).Status);
        Assert.Equal(2, Fake.PayoutCalls); // worker chỉ hỏi trạng thái, không gửi thêm
    }

    [PostgresMoneyFact]
    public async Task ReconcileOfNeverSentRefundSubmitsItOnce()
    {
        // Sập giữa commit và gửi: lệnh PROCESSING chưa có last_attempt_at → đối soát gửi lần đầu (cùng khóa), không gửi hai lần.
        var c = await NewCollection();
        var a = await NewAnomaly(c, "OVERPAID", 50_000);
        var refund = await CreateAnomalyRefund(a);
        await using (var db = Open()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE money_refunds SET last_attempt_at = NULL, provider_ref = NULL, status = 'PROCESSING' WHERE id = {refund.Id}");
        var calls = Fake.PayoutCalls;
        var after = await Mutate(e => e.ReconcileAsync(refund.Id));
        Assert.Equal("PROCESSING", after.Status);
        Assert.Equal(calls + 1, Fake.PayoutCalls);
        Assert.Equal(refund.Code, Fake.Calls.Last().IdempotencyKey);
    }

    [Fact]
    public void RefundRulesBuildCodesDescriptionsAndKeys()
    {
        var at = new DateTime(2026, 3, 10, 18, 0, 0, DateTimeKind.Utc); // 01:00 ngày 11 giờ Việt Nam
        Assert.Equal("RF-260311-ABCDEF12", RefundRules.Code(at, "abcdef12"));
        Assert.Equal("RF-260311-ABCDEF12-B", RefundRules.Code(at, "abcdef12", 1));
        Assert.Equal("RETRACK HOAN ABCDEF", RefundRules.Description("RF-260311-ABCDEF12"));
        Assert.Equal("RETRACK HOAN ABCDEF", RefundRules.Description("RF-260311-ABCDEF12-B"));
        Assert.True(RefundRules.Description("RF-260311-ABCDEF12-B").Length <= 20);
        Assert.Equal("RF-260311-ABCDEF12-2", PayoutRules.KeyFor("RF-260311-ABCDEF12", 2));
        Assert.Equal("EXCESS", RefundRules.KindFor("OVERPAID"));
        Assert.Equal("DUPLICATE", RefundRules.KindFor("DUPLICATE_PAYMENT"));
        Assert.Equal("LATE", RefundRules.KindFor("LATE_PAYMENT"));
        Assert.Equal("FULL", RefundRules.KindFor("UNDERPAID"));
    }
}
