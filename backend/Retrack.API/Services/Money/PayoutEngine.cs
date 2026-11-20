using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Money;

/// <summary>
/// Engine chi: xem trước, chạy kỳ chi cho người nhận, gọi nhà cung cấp NGOÀI transaction, đối soát, duyệt/gửi lại/giải phóng/đánh dấu đã chi.
/// Trạng thái kỳ chi chỉ đổi bằng compare-and-swap trên (status, version) để mọi worker/API đồng thời không giẫm lên nhau.
/// Mỗi nguồn tiền chỉ vào một kỳ chi (payable_lines.payout_id), mỗi kỳ chỉ có một khóa idempotency tại một thời điểm (code hoặc code-N).
/// </summary>
public sealed partial class PayoutEngine(AppDbContext db, MoneyConfigReader configReader, IPayoutProviderResolver providers, TimeProvider clock, ILogger<PayoutEngine> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [GeneratedRegex("^[0-9a-fA-F]{8,32}$")] private static partial Regex HexKey();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public sealed record BankSnapshot(string BankName, string Bin, string Account, string Holder);

    // ============================ XEM TRƯỚC (không ghi gì) ============================

    public async Task<PayoutBatchResult> PreviewAsync(IReadOnlyCollection<Guid> payeeIds, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        var (ids, start, end) = NormalizeRequest(payeeIds, periodStart, periodEnd);
        var cfg = await configReader.GetAsync(ct);
        var now = Now;
        var users = await LoadUsersAsync(ids, ct);
        var items = new List<PayoutPlan>();
        foreach (var id in ids)
            items.Add(await PreviewOneAsync(id, users.GetValueOrDefault(id), start, end, cfg, now, ct));
        return new(start, end, items);
    }

    private async Task<PayoutPlan> PreviewOneAsync(Guid id, User? user, DateTime start, DateTime end, MoneyConfig cfg, DateTime now, CancellationToken ct)
    {
        if (user == null) return Empty(id, "", "ERROR", "Không tìm thấy người nhận.");
        if (!user.IsActive) return Empty(user, "SKIPPED", "Tài khoản người nhận không hoạt động.");
        var same = await db.PayoutRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PayeeId == id && r.PeriodStart == start && r.PeriodEnd == end, ct);
        if (same != null) return await ExistingAsync(user, same, ct);
        if (await db.PayoutRuns.AsNoTracking().AnyAsync(r => r.PayeeId == id && r.PeriodEnd > start, ct))
            return Empty(user, "CONFLICT", ConflictMessage);
        // Giống điều kiện của lần chạy: AVAILABLE, chưa vào kỳ nào, hết giữ tới period_end. Dòng HELD đã hết giữ sẽ được MarkAvailable chuyển khi chạy thật.
        var lines = await db.PayableLines.AsNoTracking().Where(l => l.PayeeId == id && l.PayoutId == null && l.AvailableAt <= end
            && (l.State == PayableStates.Available || (l.State == PayableStates.Held && l.Kind == PayableKinds.Sale && l.AvailableAt <= now)))
            .OrderBy(l => l.AvailableAt).ThenBy(l => l.Id).ToListAsync(ct);
        if (lines.Count == 0) return Empty(user, "NO_LINES", "Không có dòng sổ cái nào đủ điều kiện chi trong kỳ này.");
        var carried = CarryOver(await LatestRunAsync(id, ct));
        var totals = PayoutRules.Totals(lines, carried);
        var bank = PayoutRules.CheckBank(user.BankBin, user.BankAccountNumber, user.BankAccountName, user.BankStatus);
        var (status, reason) = PayoutRules.Decide(totals.Net, cfg, bank, alreadyApproved: false);
        return Plan(user, "NEW", null, null, null, lines, totals, status, reason);
    }

    // ============================ CHẠY ============================

    /// <summary>
    /// Chạy kỳ chi cho một hoặc nhiều người nhận (tối đa 200). Mỗi người một transaction riêng nên lỗi một người không chặn người khác.
    /// autoOnly: bỏ qua người nhận có payee_payout_settings.auto_enabled = false (chạy thủ công của Admin thì không bỏ qua).
    /// Kỳ PROCESSING được gửi cho nhà cung cấp SAU KHI commit.
    /// </summary>
    public async Task<PayoutBatchResult> RunAsync(IReadOnlyCollection<Guid> payeeIds, DateTime periodStart, DateTime periodEnd, Guid? actorId, bool autoOnly = false, CancellationToken ct = default)
    {
        var (ids, start, end) = NormalizeRequest(payeeIds, periodStart, periodEnd);
        var provider = await providers.ResolveCurrentAsync(ct); // lỗi rõ ràng nếu chọn PAYOS mà thiếu khóa
        var cfg = await configReader.GetAsync(ct);
        await new PayableLedgerService(db, configReader, clock).MarkAvailableAsync(Now, ct);
        var items = new List<PayoutPlan>();
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            PayoutPlan plan;
            try { plan = await RunOneAsync(id, start, end, actorId, autoOnly, cfg, provider.Name, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                log.LogWarning(ex, "Chạy kỳ chi cho người nhận {Payee} thất bại.", id);
                plan = Empty(id, "", "ERROR", "Không tạo được kỳ chi cho người nhận này. Hãy thử lại.");
            }
            if (plan is { Outcome: "CREATED", Status: PayoutStatuses.Processing, RunId: { } runId })
            {
                try
                {
                    var run = await SubmitAsync(runId, ct);
                    plan = plan with { Status = run.Status, Reason = run.FailReason };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "Gửi lệnh chi cho kỳ {Run} thất bại, sẽ đối soát sau.", runId);
                    plan = plan with { Message = "Đã tạo kỳ chi nhưng chưa gửi được lệnh chi; worker đối soát sẽ xử lý." };
                }
            }
            items.Add(plan);
        }
        return new(start, end, items);
    }

    /// <summary>
    /// Lịch tự chi: với mỗi người nhận còn dòng đủ điều kiện (và chưa tắt tự chi), kỳ = (max(mốc đầu chung, period_end gần nhất của người đó), end).
    /// Dòng cũ chưa chi (người nhận tắt tự chi, kỳ DEFERRED) tự dồn sang kỳ sau nhờ quy tắc đủ điều kiện không có cận dưới.
    /// </summary>
    public async Task<IReadOnlyList<PayoutPlan>> RunAutoAsync(DateTime endUtc, DateTime fallbackStartUtc, CancellationToken ct = default)
    {
        var end = PayoutRules.TruncateSeconds(endUtc);
        var now = Now;
        await new PayableLedgerService(db, configReader, clock).MarkAvailableAsync(now, ct);
        var payeeIds = await db.PayableLines.AsNoTracking().Where(l => l.PayoutId == null && l.State == PayableStates.Available && l.AvailableAt <= end)
            .Select(l => l.PayeeId).Distinct().OrderBy(x => x).Take(5000).ToListAsync(ct);
        var results = new List<PayoutPlan>();
        foreach (var id in payeeIds)
        {
            var latestEnd = await db.PayoutRuns.AsNoTracking().Where(r => r.PayeeId == id).MaxAsync(r => (DateTime?)r.PeriodEnd, ct);
            var start = PayoutRules.TruncateSeconds(latestEnd != null && latestEnd > fallbackStartUtc ? latestEnd.Value : fallbackStartUtc);
            if (start >= end) continue; // người này đã có kỳ tới mốc này
            var batch = await RunAsync([id], start, end, null, autoOnly: true, ct);
            results.AddRange(batch.Items);
        }
        return results;
    }

    private async Task<PayoutPlan> RunOneAsync(Guid id, DateTime start, DateTime end, Guid? actor, bool autoOnly, MoneyConfig cfg, string providerName, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return Empty(id, "", "ERROR", "Không tìm thấy người nhận.");
        if (!user.IsActive) return Empty(user, "SKIPPED", "Tài khoản người nhận không hoạt động.");
        if (autoOnly)
        {
            var setting = await db.PayeePayoutSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == id, ct);
            if (setting is { AutoEnabled: false }) return Empty(user, "SKIPPED", "Người nhận tắt tự chi: chờ Admin chạy thủ công.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Khóa tuần tự theo người nhận: kể cả khi chưa có kỳ nào để khóa dòng (FOR UPDATE bên dưới khóa dòng khi đã có).
        var lockKey = "payout:" + id;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
        var latest = (await db.PayoutRuns.FromSqlInterpolated($@"SELECT * FROM payout_runs WHERE payee_id = {id} ORDER BY period_end DESC, created_at DESC, id DESC LIMIT 1 FOR UPDATE")
            .AsNoTracking().ToListAsync(ct)).FirstOrDefault();

        var same = await db.PayoutRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PayeeId == id && r.PeriodStart == start && r.PeriodEnd == end, ct);
        if (same != null) return await ExistingAsync(user, same, ct); // chạy lại đúng kỳ cũ: idempotent
        if (await db.PayoutRuns.AsNoTracking().AnyAsync(r => r.PayeeId == id && r.PeriodEnd > start, ct))
            return Empty(user, "CONFLICT", ConflictMessage);

        var lines = await db.PayableLines.FromSqlInterpolated($@"SELECT * FROM payable_lines WHERE payee_id = {id} AND payout_id IS NULL AND state = 'AVAILABLE' AND available_at <= {end} ORDER BY id FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        if (lines.Count == 0) return Empty(user, "NO_LINES", "Không có dòng sổ cái nào đủ điều kiện chi trong kỳ này.");

        var totals = PayoutRules.Totals(lines, CarryOver(latest));
        var now = Now;
        var bank = PayoutRules.CheckBank(user.BankBin, user.BankAccountNumber, user.BankAccountName, user.BankStatus);
        var (status, reason) = PayoutRules.Decide(totals.Net, cfg, bank, alreadyApproved: false);
        var code = await NextCodeAsync(id, start, end, ct);
        var run = new PayoutRun
        {
            Code = code, PayeeId = id, PeriodStart = start, PeriodEnd = end, Gross = totals.Gross, Commission = totals.Commission,
            RefundAmount = totals.RefundAmount, CarriedDebt = totals.CarriedDebt, Net = totals.Net, Status = status,
            Provider = providerName, FailReason = status == PayoutStatuses.Processing ? null : reason, CreatedBy = actor, CreatedAt = now,
            PaidAt = status == PayoutStatuses.Settled ? now : null, Attempt = 1,
            BankSnapshot = status is PayoutStatuses.Processing or PayoutStatuses.AwaitingApproval or PayoutStatuses.Held ? SnapshotJson(user) : null,
        };
        db.PayoutRuns.Add(run);
        await db.SaveChangesAsync(ct);
        // Số tiền của kỳ DEFERRED không bị mất: dòng ở lại AVAILABLE (không gán payout_id) và sẽ vào kỳ sau, nợ mang sang giữ trên chính kỳ DEFERRED.
        if (status != PayoutStatuses.Deferred)
        {
            var lineState = status is PayoutStatuses.Carried or PayoutStatuses.Settled ? PayableStates.Paid : PayableStates.InPayout;
            var lineIds = lines.Select(l => l.Id).ToList();
            await db.PayableLines.Where(l => lineIds.Contains(l.Id) && l.PayoutId == null && l.State == PayableStates.Available)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.PayoutId, run.Id).SetProperty(l => l.State, lineState), ct);
        }
        db.AuditLogs.Add(Audit(actor, "PAYOUT_RUN_CREATED", run.Id, null, new { run.Code, run.Status, run.Gross, run.Commission, run.RefundAmount, run.CarriedDebt, run.Net, lines = lines.Count, run.PeriodStart, run.PeriodEnd, run.Provider }));
        db.Notifications.Add(Notify(id, run, CreatedTitle(status), CreatedMessage(run, reason)));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return Plan(user, "CREATED", null, run.Id, run.Code, lines, totals, status, reason);
    }

    // ============================ GỬI LỆNH CHI (NGOÀI TRANSACTION) ============================

    /// <summary>
    /// Gửi lệnh chi cho kỳ PROCESSING. Không mở transaction. Bước: chiếm lượt gửi (CAS) → kiểm tra số dư → PayoutAsync → ghi kết quả (CAS).
    /// Accepted: giữ PROCESSING kèm provider_ref. DefiniteFailure: FAILED. Unknown: UNKNOWN (không gửi lại bằng khóa mới).
    /// </summary>
    public async Task<PayoutRun> SubmitAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run.Status != PayoutStatuses.Processing) return run;
        var provider = providers.GetByName(run.Provider);
        var snap = ParseSnapshot(run.BankSnapshot);
        if (snap == null)
        {
            await CasAsync(run, PayoutStatuses.Held, "Thiếu thông tin tài khoản ngân hàng của người nhận.");
            return await LoadRunAsync(runId, ct);
        }
        // Chiếm lượt gửi: chỉ một tiến trình thắng. Version tăng nên các CAS sau dùng version mới.
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE payout_runs SET last_attempt_at = {Now}, version = version + 1
            WHERE id = {run.Id} AND status = 'PROCESSING' AND version = {run.Version}", ct);
        if (claimed == 0) return await LoadRunAsync(runId, ct);
        run.Version++;

        decimal balance;
        try { balance = await provider.GetBalanceAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Không kiểm tra được số dư trước khi chi kỳ {Run}.", run.Code);
            await ToPendingAsync(run, "Không kiểm tra được số dư tài khoản chi, chưa gửi lệnh. Hãy thử lại sau.", notifyAdmins: false, ct);
            return await LoadRunAsync(runId, ct);
        }
        if (balance < run.Net)
        {
            await ToPendingAsync(run, $"Số dư tài khoản chi ({MoneyFormat.Vnd(balance)}) không đủ cho kỳ {run.Code} ({MoneyFormat.Vnd(run.Net)}). Chưa gửi lệnh.", notifyAdmins: true, ct);
            return await LoadRunAsync(runId, ct);
        }

        var key = PayoutRules.KeyFor(run.Code, run.Attempt);
        PayoutResult result;
        try { result = await provider.PayoutAsync(new(key, key, run.Net, PayoutRules.Description(run.Code), snap.Bin, snap.Account), ct); }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gọi nhà cung cấp chi kỳ {Run} bị gián đoạn, coi là chưa rõ.", run.Code);
            result = new(PayoutOutcome.Unknown, null, "EXCEPTION", "Không nhận được phản hồi từ nhà cung cấp chi.");
        }

        // Ghi kết quả dù yêu cầu bị hủy giữa chừng: đã gọi ra ngoài thì phải ghi lại.
        switch (result.Outcome)
        {
            case PayoutOutcome.Accepted:
                await CasAsync(run, PayoutStatuses.Processing, null, providerRef: result.ProviderRef, gateway: "ACCEPTED");
                break;
            case PayoutOutcome.DefiniteFailure:
                await CasAsync(run, PayoutStatuses.Failed, Trim($"Nhà cung cấp từ chối lệnh chi: {result.Message ?? result.ErrorCode}"), gateway: "REJECTED");
                break;
            default:
                await CasAsync(run, PayoutStatuses.Unknown, "Chưa rõ lệnh chi đã được nhận chưa (timeout/lỗi). Đối soát bằng cùng khóa trước khi làm gì khác.", gateway: "UNKNOWN");
                break;
        }
        return await LoadRunAsync(runId, CancellationToken.None);
    }

    private async Task ToPendingAsync(PayoutRun run, string reason, bool notifyAdmins, CancellationToken ct)
    {
        await CasAsync(run, PayoutStatuses.Pending, reason);
        if (!notifyAdmins) return;
        var admins = await db.Users.AsNoTracking().Where(u => u.Role == "ADMIN" && u.IsActive).Select(u => u.Id).OrderBy(x => x).Take(20).ToListAsync(ct);
        foreach (var a in admins) db.Notifications.Add(Notify(a, run, "Kỳ chi chờ do thiếu số dư", reason));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    // ============================ ĐỐI SOÁT ============================

    /// <summary>
    /// Hỏi nhà cung cấp về kỳ PROCESSING/UNKNOWN (tra bằng khóa hiện hành hoặc provider_ref). Succeeded → PAID; Failed → FAILED;
    /// Processing → giữ (UNKNOWN thành PROCESSING vì nhà cung cấp đã biết lệnh); NotFound sau 10 phút kể từ lần gửi mà chưa có provider_ref →
    /// PENDING (coi là chưa nhận, gửi lại CÙNG khóa bằng RetryAsync). Kỳ PROCESSING chưa từng gửi (sập giữa commit và gửi) thì gửi lần đầu.
    /// </summary>
    public async Task<PayoutRun> ReconcileAsync(Guid runId, Guid? actorId = null, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run.Status is not (PayoutStatuses.Processing or PayoutStatuses.Unknown)) return run;
        if (run.Status == PayoutStatuses.Processing && run.LastAttemptAt == null) return await SubmitAsync(runId, ct);
        var provider = providers.GetByName(run.Provider);
        var status = await provider.GetPayoutAsync(run.ProviderRef ?? PayoutRules.KeyFor(run.Code, run.Attempt), ct);
        switch (status.Status)
        {
            case PayoutStatus.Succeeded:
                await FinalizePaidAsync(run, actorId, status.ProviderRef, "SUCCEEDED", null, ct);
                break;
            case PayoutStatus.Failed:
                if (await CasAsync(run, PayoutStatuses.Failed, Trim($"Nhà cung cấp báo chuyển thất bại: {status.Message}"), providerRef: status.ProviderRef, gateway: "FAILED"))
                    await AuditAsync(actorId, "PAYOUT_FAILED", run.Id, new { run.Code, status.Message }, ct);
                break;
            case PayoutStatus.Processing:
                if (run.Status == PayoutStatuses.Unknown) await CasAsync(run, PayoutStatuses.Processing, null, providerRef: status.ProviderRef, gateway: "PROCESSING");
                else await SetGatewayAsync(run.Id, "PROCESSING", status.ProviderRef);
                break;
            default: // NotFound
                if (run.ProviderRef == null && run.LastAttemptAt != null && Now - PayoutRules.Utc(run.LastAttemptAt.Value) >= PayoutRules.NotFoundGrace)
                    await CasAsync(run, PayoutStatuses.Pending, "Nhà cung cấp không ghi nhận lệnh sau 10 phút: coi là chưa nhận. Có thể gửi lại cùng khóa.", gateway: "NOT_FOUND");
                else await SetGatewayAsync(run.Id, "NOT_FOUND", null);
                break;
        }
        return await LoadRunAsync(runId, ct);
    }

    private async Task<bool> FinalizePaidAsync(PayoutRun run, Guid? actor, string? providerRef, string gateway, string? manualNote, CancellationToken ct)
    {
        var fromStatus = run.Status;
        var oldRef = run.ProviderRef;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await CasAsync(run, PayoutStatuses.Paid, null, providerRef: providerRef, gateway: gateway, paidAt: Now)) return false;
        await db.PayableLines.Where(l => l.PayoutId == run.Id && l.State == PayableStates.InPayout)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.State, PayableStates.Paid), ct);
        db.AuditLogs.Add(Audit(actor, manualNote == null ? "PAYOUT_PAID" : "PAYOUT_MARKED_PAID", run.Id, new { status = fromStatus, providerRef = oldRef }, new { run.Code, run.Net, providerRef, gateway, note = manualNote }));
        db.Notifications.Add(Notify(run.PayeeId, run, "Đã chi tiền", $"Kỳ chi {run.Code}: sàn đã chuyển {MoneyFormat.Vnd(run.Net)} đồng tới tài khoản ngân hàng của bạn."));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }

    // ============================ THAO TÁC CỦA ADMIN ============================

    /// <summary>Duyệt kỳ AWAITING_APPROVAL. Kiểm tra lại tài khoản ngân hàng: chưa đủ điều kiện thì sang HELD (đã được duyệt, không cần duyệt lại khi giải phóng).</summary>
    public async Task<PayoutRun> ApproveAsync(Guid runId, Guid actorId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run.Status != PayoutStatuses.AwaitingApproval) throw new DepotConflictException("Kỳ chi không ở trạng thái chờ duyệt.");
        var (bank, snapshot) = await CurrentBankAsync(run, refresh: run.LastAttemptAt == null, ct);
        var to = bank.Ready ? PayoutStatuses.Processing : PayoutStatuses.Held;
        if (!await CasAsync(run, to, bank.Ready ? null : bank.Reason, approvedBy: actorId, approvedAt: Now, snapshot: snapshot))
            throw new DepotConflictException("Kỳ chi vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        await AuditAsync(actorId, "PAYOUT_APPROVED", run.Id, new { run.Code, run.Net, to }, ct);
        return to == PayoutStatuses.Processing ? await SubmitAsync(runId, ct) : await LoadRunAsync(runId, ct);
    }

    /// <summary>
    /// Gửi lại. FAILED (lỗi rõ ràng): sang lần gửi mới, khóa mới code-N, lấy lại tài khoản ngân hàng hiện tại.
    /// PENDING (chưa gửi được hoặc không được ghi nhận): gửi lại CÙNG khóa và cùng tài khoản đã chốt.
    /// </summary>
    public async Task<PayoutRun> RetryAsync(Guid runId, Guid actorId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run.Status is not (PayoutStatuses.Failed or PayoutStatuses.Pending))
            throw new DepotConflictException("Chỉ gửi lại được kỳ chi đang FAILED hoặc PENDING.");
        var failed = run.Status == PayoutStatuses.Failed;
        var (bank, snapshot) = await CurrentBankAsync(run, refresh: failed || run.LastAttemptAt == null, ct);
        var to = bank.Ready ? PayoutStatuses.Processing : PayoutStatuses.Held;
        if (!await CasAsync(run, to, bank.Ready ? null : bank.Reason, snapshot: snapshot, bumpAttempt: failed, clearProviderRef: failed))
            throw new DepotConflictException("Kỳ chi vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        await AuditAsync(actorId, "PAYOUT_RETRY", run.Id, new { run.Code, from = failed ? "FAILED" : "PENDING", attempt = run.Attempt, to }, ct);
        return to == PayoutStatuses.Processing ? await SubmitAsync(runId, ct) : await LoadRunAsync(runId, ct);
    }

    /// <summary>Giải phóng kỳ HELD khi lý do giữ đã hết (tài khoản đã được Admin duyệt).</summary>
    public async Task<PayoutRun> ReleaseAsync(Guid runId, Guid actorId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run.Status != PayoutStatuses.Held) throw new DepotConflictException("Chỉ giải phóng được kỳ chi đang bị giữ (HELD).");
        var (bank, snapshot) = await CurrentBankAsync(run, refresh: run.LastAttemptAt == null, ct);
        if (!bank.Ready) throw new DepotConflictException(bank.Reason ?? "Lý do giữ chưa hết.");
        var cfg = await configReader.GetAsync(ct);
        var to = run.Net > cfg.PayoutAutoMaxAmount && run.ApprovedBy == null ? PayoutStatuses.AwaitingApproval : PayoutStatuses.Processing;
        var reason = to == PayoutStatuses.AwaitingApproval ? $"Vượt hạn mức tự chi {MoneyFormat.Vnd(cfg.PayoutAutoMaxAmount)} đồng: cần Admin duyệt." : null;
        if (!await CasAsync(run, to, reason, snapshot: snapshot))
            throw new DepotConflictException("Kỳ chi vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        await AuditAsync(actorId, "PAYOUT_RELEASED", run.Id, new { run.Code, to }, ct);
        return to == PayoutStatuses.Processing ? await SubmitAsync(runId, ct) : await LoadRunAsync(runId, ct);
    }

    /// <summary>
    /// Sau khi Admin duyệt tài khoản ngân hàng: giải phóng mọi kỳ HELD của người nhận (kỳ chỉ bị HELD vì tài khoản chưa đủ điều kiện).
    /// Mỗi kỳ qua <see cref="ReleaseAsync"/> (kiểm tra lại tài khoản, vượt hạn mức thì sang AWAITING_APPROVAL, còn lại PROCESSING và gửi nhà cung cấp NGOÀI transaction).
    /// Lỗi ở một kỳ không chặn các kỳ khác. Trả số kỳ đã giải phóng.
    /// </summary>
    public async Task<int> ReleaseHeldForPayeeAsync(Guid payeeId, Guid actorId, CancellationToken ct = default)
    {
        var ids = await db.PayoutRuns.AsNoTracking().Where(r => r.PayeeId == payeeId && r.Status == PayoutStatuses.Held).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Take(100).Select(r => r.Id).ToListAsync(ct);
        var released = 0;
        foreach (var id in ids)
        {
            try { await ReleaseAsync(id, actorId, ct); released++; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                log.LogWarning(ex, "Không giải phóng được kỳ chi {Run} sau khi duyệt tài khoản ngân hàng.", id);
            }
        }
        return released;
    }

    /// <summary>
    /// Đánh dấu đã chi tay (Admin chuyển khoản ngoài hệ thống). reference là mã giao dịch ngân hàng (duy nhất, lưu vào provider_ref).
    /// PROCESSING/UNKNOWN bắt buộc confirmed vì lệnh có thể đang chạy ở nhà cung cấp: chi tay thêm sẽ chi trùng.
    /// </summary>
    public async Task<PayoutRun> MarkPaidManuallyAsync(Guid runId, Guid actorId, string reference, string? note, bool confirmed, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        reference = (reference ?? "").Trim();
        if (reference.Length is 0 or > 100) throw new ArgumentException("Cần nhập mã giao dịch chi tay (tối đa 100 ký tự).");
        if (note is { Length: > 500 }) throw new ArgumentException("Ghi chú tối đa 500 ký tự.");
        if (run.Status is not (PayoutStatuses.Pending or PayoutStatuses.Failed or PayoutStatuses.Processing or PayoutStatuses.Unknown or PayoutStatuses.Held or PayoutStatuses.AwaitingApproval))
            throw new DepotConflictException("Kỳ chi ở trạng thái này không thể đánh dấu đã chi tay.");
        if (run.Status is PayoutStatuses.Processing or PayoutStatuses.Unknown && !confirmed)
            throw new DepotConflictException("Lệnh chi có thể đang chạy ở nhà cung cấp. Hãy đối soát trước, hoặc xác nhận (confirm = true) rằng bạn đã chuyển tay và chấp nhận rủi ro chi trùng.");
        if (await db.PayoutRuns.AsNoTracking().AnyAsync(r => r.ProviderRef == reference && r.Id != run.Id, ct))
            throw new DepotConflictException("Mã giao dịch này đã được dùng cho kỳ chi khác.");
        bool ok;
        try { ok = await FinalizePaidAsync(run, actorId, reference, "MANUAL", note ?? "", ct); }
        catch (DbUpdateException) { throw new DepotConflictException("Mã giao dịch này đã được dùng cho kỳ chi khác."); }
        if (!ok) throw new DepotConflictException("Kỳ chi vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        return await LoadRunAsync(runId, ct);
    }

    // ============================ CHUYỂN THỬ ============================

    /// <summary>
    /// Chuyển thử số nhỏ qua nhà cung cấp ĐANG CHỌN để kiểm tra kênh chi thật. Không tạo kỳ chi và không ghi sổ cái.
    /// Chỉ khi payout.test_enabled; số tiền nguyên 1.000 tới payout.test_max_amount; số dư đủ; BIN 6 số; tài khoản 6-20 số.
    /// ClientKey (hex 8-32 ký tự) do người gọi sinh và giữ lại: gửi lại cùng khóa thì không chuyển trùng.
    /// </summary>
    public async Task<TestTransferResult> TestTransferAsync(Guid actorId, TestTransferRequest request, CancellationToken ct = default)
    {
        var cfg = await configReader.GetAsync(ct);
        if (!cfg.PayoutTestEnabled) throw new InvalidOperationException("Chuyển thử đang tắt. Bật payout.test_enabled trong cấu hình thu/chi trước.");
        if (request.Amount != decimal.Truncate(request.Amount) || request.Amount < 1000 || request.Amount > cfg.PayoutTestMaxAmount)
            throw new ArgumentException($"Số tiền chuyển thử phải là số nguyên từ 1.000 đến {MoneyFormat.Vnd(cfg.PayoutTestMaxAmount)} đồng.");
        if (!PayoutBankRules.IsValidBin(request.ToBin)) throw new ArgumentException("Mã BIN phải gồm đúng 6 chữ số.");
        if (!PayoutBankRules.IsValidAccountNumber(request.ToAccountNumber)) throw new ArgumentException("Số tài khoản phải gồm 6 đến 20 chữ số.");
        var key = TestKey(request.ClientKey);
        var provider = await providers.ResolveCurrentAsync(ct);
        decimal balance;
        try { balance = await provider.GetBalanceAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new InvalidOperationException("Không kiểm tra được số dư tài khoản chi, chưa chuyển thử.", ex); }
        if (balance < request.Amount) throw new InvalidOperationException("Số dư tài khoản chi không đủ để chuyển thử.");

        PayoutResult result;
        try { result = await provider.PayoutAsync(new(key, key, request.Amount, "RETRACK TEST", request.ToBin, request.ToAccountNumber), ct); }
        catch (Exception) { result = new(PayoutOutcome.Unknown, null, "EXCEPTION", "Không nhận được phản hồi từ nhà cung cấp chi."); }
        var outcome = result.Outcome switch { PayoutOutcome.Accepted => "ACCEPTED", PayoutOutcome.DefiniteFailure => "FAILED", _ => "UNKNOWN" };
        // Chỉ ghi 4 số cuối của tài khoản vào audit.
        db.AuditLogs.Add(Audit(actorId, "PAYOUT_TEST_TRANSFER", null, null, new { key, provider = provider.Name, amount = request.Amount, bin = request.ToBin, account = PayoutBankRules.Mask(request.ToAccountNumber), outcome, result.ProviderRef, result.ErrorCode }));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return new(key, provider.Name, outcome, result.ProviderRef, outcome == "FAILED" ? Trim(result.Message ?? result.ErrorCode) : result.Message);
    }

    /// <summary>Tra trạng thái một lần chuyển thử theo ClientKey (kênh chi chỉ có polling).</summary>
    public async Task<TestTransferStatus> GetTestTransferStatusAsync(string clientKey, CancellationToken ct = default)
    {
        var key = TestKey(clientKey);
        var provider = await providers.ResolveCurrentAsync(ct);
        var s = await provider.GetPayoutAsync(key, ct);
        return new(key, provider.Name, s.Status switch { PayoutStatus.Succeeded => "SUCCEEDED", PayoutStatus.Failed => "FAILED", PayoutStatus.Processing => "PROCESSING", _ => "NOT_FOUND" }, s.ProviderRef, s.Message);
    }

    private static string TestKey(string? clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey) || !HexKey().IsMatch(clientKey.Trim()))
            throw new ArgumentException("Khóa chuyển thử (clientKey) phải là chuỗi hex 8 đến 32 ký tự do giao diện sinh ra và giữ lại.");
        return "TEST-" + clientKey.Trim().ToLowerInvariant();
    }

    // ============================ TRUY VẤN DÙNG CHUNG VỚI WORKER ============================

    /// <summary>Kỳ cần đối soát: PROCESSING/UNKNOWN có lần gửi cuối (hoặc lúc tạo) cũ hơn minAge, cũ nhất trước.</summary>
    public async Task<List<Guid>> DueForReconcileAsync(TimeSpan minAge, int take, CancellationToken ct = default)
    {
        var cutoff = Now - minAge;
        return await db.PayoutRuns.AsNoTracking().Where(r => (r.Status == PayoutStatuses.Processing || r.Status == PayoutStatuses.Unknown)
                && (r.LastAttemptAt ?? r.CreatedAt) <= cutoff)
            .OrderBy(r => r.LastAttemptAt ?? r.CreatedAt).ThenBy(r => r.Id).Take(Math.Clamp(take, 1, 200)).Select(r => r.Id).ToListAsync(ct);
    }

    // ============================ HỖ TRỢ NỘI BỘ ============================

    private const string ConflictMessage = "Kỳ này chồng lên hoặc đi trước một kỳ chi đã có của người nhận. Hãy chọn kỳ bắt đầu sau khi kỳ gần nhất kết thúc.";

    private (List<Guid> Ids, DateTime Start, DateTime End) NormalizeRequest(IReadOnlyCollection<Guid> payeeIds, DateTime periodStart, DateTime periodEnd)
    {
        var ids = (payeeIds ?? []).Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("Cần chọn ít nhất một người nhận.");
        if (ids.Count > PayoutRules.MaxPayeesPerRun) throw new ArgumentException($"Mỗi lần chỉ chọn tối đa {PayoutRules.MaxPayeesPerRun} người nhận.");
        var start = PayoutRules.TruncateSeconds(periodStart);
        var end = PayoutRules.TruncateSeconds(periodEnd);
        var invalid = PayoutRules.ValidatePeriod(start, end, Now);
        if (invalid != null) throw new ArgumentException(invalid);
        return (ids, start, end);
    }

    private async Task<Dictionary<Guid, User>> LoadUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

    private Task<PayoutRun?> LatestRunAsync(Guid payeeId, CancellationToken ct) =>
        db.PayoutRuns.AsNoTracking().Where(r => r.PayeeId == payeeId).OrderByDescending(r => r.PeriodEnd).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Nợ mang sang từ kỳ gần nhất: CARRIED mang số âm của nó (dòng đã cấn trừ nên không đếm lại); DEFERRED giữ nguyên nợ nó đã nhận
    /// (dòng của nó trả về AVAILABLE nên sẽ được tính lại ở kỳ sau). Các trạng thái khác đã dùng hết nợ trong chính kỳ đó.
    /// </summary>
    public static decimal CarryOver(PayoutRun? latest) => latest?.Status switch
    {
        PayoutStatuses.Carried => Math.Max(0, -latest.Net),
        PayoutStatuses.Deferred => latest.CarriedDebt,
        _ => 0m,
    };

    private async Task<PayoutRun> LoadRunAsync(Guid id, CancellationToken ct) =>
        await db.PayoutRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException("Không tìm thấy kỳ chi.");

    private async Task<string> NextCodeAsync(Guid payee, DateTime start, DateTime end, CancellationToken ct)
    {
        for (var i = 0; i < 25; i++)
        {
            var code = PayoutRules.Code(payee, start, end, i);
            if (!await db.PayoutRuns.AsNoTracking().AnyAsync(r => r.Code == code, ct)) return code;
        }
        throw new InvalidOperationException("Không sinh được mã kỳ chi duy nhất.");
    }

    /// <summary>Tài khoản ngân hàng hiện tại của người nhận; refresh = true thì chốt lại snapshot (chỉ khi lệnh chưa từng tới nhà cung cấp hoặc sẽ dùng khóa mới).</summary>
    private async Task<(BankCheck Bank, string? Snapshot)> CurrentBankAsync(PayoutRun run, bool refresh, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == run.PayeeId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người nhận.");
        if (!u.IsActive) return (new(false, "Tài khoản người nhận không hoạt động."), null);
        var bank = PayoutRules.CheckBank(u.BankBin, u.BankAccountNumber, u.BankAccountName, u.BankStatus);
        return (bank, refresh && bank.Ready ? SnapshotJson(u) : null);
    }

    private static string SnapshotJson(User u) =>
        JsonSerializer.Serialize(new BankSnapshot(u.BankName ?? "", u.BankBin ?? "", u.BankAccountNumber ?? "", u.BankAccountName ?? ""), Json);

    private static BankSnapshot? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var s = JsonSerializer.Deserialize<BankSnapshot>(json, Json);
            return s != null && PayoutBankRules.IsValidBin(s.Bin) && PayoutBankRules.IsValidAccountNumber(s.Account) ? s : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Compare-and-swap trạng thái trên (status, version). Trả false nếu kỳ đã bị thao tác khác đổi.</summary>
    private async Task<bool> CasAsync(PayoutRun run, string to, string? reason, string? providerRef = null, string? gateway = null, DateTime? paidAt = null,
        Guid? approvedBy = null, DateTime? approvedAt = null, string? snapshot = null, bool bumpAttempt = false, bool clearProviderRef = false)
    {
        var inc = bumpAttempt ? 1 : 0;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE payout_runs SET status = {to}, fail_reason = CAST({reason} AS text),
            provider_ref = CASE WHEN {clearProviderRef} THEN NULL ELSE COALESCE(CAST({providerRef} AS varchar), provider_ref) END,
            gateway_status = COALESCE(CAST({gateway} AS varchar), gateway_status), paid_at = COALESCE(CAST({paidAt} AS timestamptz), paid_at),
            approved_by = COALESCE(CAST({approvedBy} AS uuid), approved_by), approved_at = COALESCE(CAST({approvedAt} AS timestamptz), approved_at),
            bank_snapshot = COALESCE(CAST({snapshot} AS jsonb), bank_snapshot), attempt = attempt + {inc}, version = version + 1
            WHERE id = {run.Id} AND status = {run.Status} AND version = {run.Version}", CancellationToken.None);
        if (rows == 1) { run.Status = to; run.Version++; run.Attempt += inc; }
        return rows == 1;
    }

    /// <summary>Cập nhật thông tin tra cứu (không đổi trạng thái, không tăng version để không phá CAS của luồng gửi).</summary>
    private Task SetGatewayAsync(Guid runId, string gateway, string? providerRef) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payout_runs SET gateway_status = {gateway}, provider_ref = COALESCE(provider_ref, CAST({providerRef} AS varchar)) WHERE id = {runId}");

    private async Task AuditAsync(Guid? actor, string action, Guid? runId, object data, CancellationToken ct)
    {
        db.AuditLogs.Add(Audit(actor, action, runId, null, data));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private AuditLog Audit(Guid? actor, string action, Guid? entityId, object? oldData, object? newData) => new()
    {
        UserId = actor, Action = action, EntityName = entityId == null ? "payout_test" : "payout_runs", EntityId = entityId,
        OldData = oldData == null ? null : JsonSerializer.Serialize(oldData, Json), NewData = newData == null ? null : JsonSerializer.Serialize(newData, Json), CreatedAt = Now,
    };

    private Notification Notify(Guid user, PayoutRun run, string title, string message) => new()
    {
        UserId = user, Category = "PAYOUT", TargetType = "PAYOUT_RUN", TargetId = run.Id, Title = title, Message = message, CreatedAt = Now,
    };

    private static string CreatedTitle(string status) => status switch
    {
        PayoutStatuses.Held => "Kỳ chi đang được giữ",
        PayoutStatuses.AwaitingApproval => "Kỳ chi chờ duyệt",
        PayoutStatuses.Deferred => "Kỳ chi được dồn sang kỳ sau",
        PayoutStatuses.Carried => "Kỳ chi cấn trừ công nợ",
        PayoutStatuses.Settled => "Kỳ chi đã cấn trừ",
        _ => "Sàn đang chi tiền cho bạn",
    };

    private static string CreatedMessage(PayoutRun run, string? reason) =>
        $"Kỳ chi {run.Code}: số tiền sau hoa hồng {MoneyFormat.Vnd(run.Net)} đồng." + (string.IsNullOrWhiteSpace(reason) ? "" : " " + reason);

    private static string Trim(string? s) => s is { Length: > 500 } ? s[..500] : s ?? "";

    private static PayoutPlan Empty(Guid id, string name, string outcome, string message) =>
        new(id, name, outcome, message, null, null, [], 0, 0, 0, 0, 0, null, message, null, null);

    private static PayoutPlan Empty(User u, string outcome, string message) =>
        new(u.Id, u.FullName, outcome, message, null, null, [], 0, 0, 0, 0, 0, null, message, u.BankStatus, PayoutBankRules.Mask(u.BankAccountNumber));

    private static PayoutPlan Plan(User u, string outcome, string? message, Guid? runId, string? code, IReadOnlyList<PayableLine> lines, PayoutTotals t, string status, string? reason) =>
        new(u.Id, u.FullName, outcome, message, runId, code, lines.Select(View).ToList(), t.Gross, t.Commission, t.RefundAmount, t.CarriedDebt, t.Net, status, reason,
            u.BankStatus, PayoutBankRules.Mask(u.BankAccountNumber));

    private async Task<PayoutPlan> ExistingAsync(User u, PayoutRun run, CancellationToken ct)
    {
        var lines = await db.PayableLines.AsNoTracking().Where(l => l.PayoutId == run.Id).OrderBy(l => l.AvailableAt).ThenBy(l => l.Id).ToListAsync(ct);
        return new(u.Id, u.FullName, "EXISTS", "Kỳ chi này đã tồn tại, không tạo thêm.", run.Id, run.Code, lines.Select(View).ToList(), run.Gross, run.Commission,
            run.RefundAmount, run.CarriedDebt, run.Net, run.Status, run.FailReason, u.BankStatus, PayoutBankRules.Mask(u.BankAccountNumber));
    }

    public static PayoutLineView View(PayableLine l) =>
        new(l.Id, l.Flow, l.SourceType, l.SourceId, l.Kind, l.Gross, l.Commission, l.CommissionRate, PayoutRules.LineNet(l.Kind, l.Gross, l.Commission), PayoutRules.Utc(l.AvailableAt), l.State);
}
