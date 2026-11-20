using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Money;

/// <summary>Quy tắc thuần của hoàn tiền (không truy cập DB).</summary>
public static class RefundRules
{
    /// <summary>RF-yymmdd-8 ký tự hex (ngày theo giờ Việt Nam). Hậu tố chữ -B, -C khi trùng; KHÔNG dùng hậu tố số vì code-N là khóa gửi lại.</summary>
    public static string Code(DateTime nowUtc, string hex8, int collisionIndex = 0)
    {
        var baseCode = $"RF-{PayoutRules.ToVn(nowUtc):yyMMdd}-{hex8.ToUpperInvariant()}";
        return collisionIndex <= 0 ? baseCode : baseCode + "-" + (char)('A' + collisionIndex);
    }

    /// <summary>Mô tả lệnh chi tối đa 20 ký tự: "RETRACK HOAN " + 6 ký tự hex của mã (19 ký tự).</summary>
    public static string Description(string code)
    {
        var parts = code.Split('-');
        var hex = parts.Length >= 3 ? parts[2] : code;
        return "RETRACK HOAN " + hex[..Math.Min(6, hex.Length)];
    }

    /// <summary>Loại hoàn tương ứng với bất thường khi thu tiền.</summary>
    public static string KindFor(string anomalyKind) => anomalyKind switch
    {
        CollectionAnomalyKinds.Overpaid => RefundKinds.Excess,
        CollectionAnomalyKinds.DuplicatePayment => RefundKinds.Duplicate,
        CollectionAnomalyKinds.LatePayment => RefundKinds.Late,
        _ => RefundKinds.Full, // UNDERPAID, SOURCE_MISMATCH: trả lại toàn bộ số tiền đã nhận nhưng chưa được ghi nhận là đã thanh toán
    };

    public static string StatusNote(string status) => status switch
    {
        RefundStatuses.Paid or RefundStatuses.ManualPaid => "Đã hoàn về tài khoản ngân hàng của bạn.",
        RefundStatuses.Processing or RefundStatuses.Unknown or RefundStatuses.Pending => "Đang hoàn tiền về tài khoản ngân hàng của bạn.",
        RefundStatuses.AwaitingApproval => "Đang chờ sàn duyệt.",
        RefundStatuses.Failed => "Hoàn tiền chưa thành công, sàn đang xử lý lại.",
        RefundStatuses.Cancelled => "Lệnh hoàn tiền đã bị hủy.",
        _ => "",
    };

    public const string BankRequiredMessage = "Người trả chưa có tài khoản ngân hàng được duyệt. Hãy yêu cầu người trả nhập tài khoản ngân hàng ở trang Tài khoản ngân hàng, rồi Admin duyệt tài khoản đó trước khi hoàn tiền.";
}

/// <summary>
/// Engine hoàn tiền cho người trả vào sàn. Cùng giao thức an toàn với <see cref="PayoutEngine"/>: kiểm tra số dư TRƯỚC khi gọi nhà cung cấp,
/// gọi nhà cung cấp NGOÀI transaction, khóa idempotency = mã hoàn (code) hoặc code-N chỉ sau lỗi RÕ RÀNG, CAS trên (status, version).
/// Khóa dòng khi tạo: nguồn → khoản thu → bất thường/sổ cái → lệnh hoàn; mỗi thao tác một transaction.
/// </summary>
public sealed class RefundEngine(AppDbContext db, MoneyConfigReader configReader, IPayoutProviderResolver providers, TimeProvider clock, ILogger<RefundEngine> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private const int MaxReasonLength = 500;

    // ============================ TẠO LỆNH HOÀN ============================

    /// <summary>
    /// Hoàn tiền cho một bất thường thu tiền (trả dư/trùng/muộn/thiếu/nguồn đổi). Số tiền mặc định và tối đa là refund_amount của bất thường.
    /// Mỗi bất thường chỉ có một lệnh hoàn còn hiệu lực (đã hủy thì được tạo lại; đang FAILED thì phải gửi lại hoặc hủy trước).
    /// Không đụng sổ cái: tiền dư/trùng/muộn chưa bao giờ nằm trong dòng SALE.
    /// </summary>
    public async Task<MoneyRefund> CreateForAnomalyAsync(Guid anomalyId, decimal? amount, string? reason, Guid actorId, CancellationToken ct = default)
    {
        reason = ValidateReason(reason);
        db.ChangeTracker.Clear();
        var head = await db.MoneyCollectionAnomalies.AsNoTracking().Where(a => a.Id == anomalyId).Select(a => new { a.CollectionId }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy bất thường thu tiền.");
        var provider = await providers.ResolveCurrentAsync(ct);
        var cfg = await configReader.GetAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await LockCollectionChainAsync(head.CollectionId, ct);
        var anomaly = await db.MoneyCollectionAnomalies.FromSqlInterpolated($"SELECT * FROM money_collection_anomalies WHERE id = {anomalyId} FOR UPDATE").AsNoTracking().SingleAsync(ct);
        if (anomaly.Status != "OPEN") throw new DepotConflictException("Bất thường này đã được xử lý.");
        if (anomaly.RefundAmount <= 0) throw new DepotConflictException("Bất thường này không có số tiền cần hoàn.");
        var existing = await db.MoneyRefunds.AsNoTracking().Where(r => r.AnomalyId == anomalyId && r.Status != RefundStatuses.Cancelled)
            .OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (existing != null)
            throw new DepotConflictException($"Bất thường này đã có lệnh hoàn {existing.Code} ({existing.Status})." +
                (existing.Status == RefundStatuses.Failed ? " Hãy gửi lại hoặc hủy lệnh đó trước." : ""));
        var value = ValidateAmount(amount ?? anomaly.RefundAmount, anomaly.RefundAmount, "Số tiền hoàn");
        var payer = await RequirePayerAsync(c.PayerId, ct);

        var kind = RefundRules.KindFor(anomaly.Kind);
        var refund = await InsertAsync(c, anomaly.Id, payer, kind, value, reason, actorId, provider.Name, cfg, ct);
        db.AuditLogs.Add(Audit(actorId, "REFUND_CREATED", refund.Id, null, new { refund.Code, refund.Kind, refund.Amount, refund.Status, anomalyId, anomalyKind = anomaly.Kind, collectionId = c.Id }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await DispatchAsync(refund, ct);
    }

    /// <summary>
    /// Admin hoàn toàn phần (FULL) hoặc một phần (PARTIAL) khoản thu đã thanh toán (ví dụ đơn bị hủy/trả hàng).
    /// Chỉ khi dòng SALE của nguồn CHƯA IN_PAYOUT/PAID (HELD/AVAILABLE hoặc chưa có dòng): tạo dòng REFUND tương ứng trong cùng transaction
    /// (hoa hồng hoàn theo tỷ lệ tích lũy, khóa = mã hoàn). Tổng các lần hoàn không vượt số tiền khoản thu (khóa khoản thu FOR UPDATE rồi cộng các lệnh chưa hủy).
    /// Khoản thu sang REFUNDING khi tổng các lệnh hoàn bằng số tiền khoản thu, REFUNDED khi tổng đã chi xong bằng số tiền đó.
    /// </summary>
    public async Task<MoneyRefund> CreateForCollectionAsync(Guid collectionId, string? kindText, decimal? amount, string? reason, Guid actorId, CancellationToken ct = default)
    {
        reason = ValidateReason(reason);
        var kind = (kindText ?? "").Trim().ToUpperInvariant();
        if (kind is not (RefundKinds.Full or RefundKinds.Partial)) throw new ArgumentException("Loại hoàn phải là FULL hoặc PARTIAL.");
        if (kind == RefundKinds.Partial && amount == null) throw new ArgumentException("Hoàn một phần cần nhập số tiền.");
        db.ChangeTracker.Clear();
        if (!await db.MoneyCollections.AsNoTracking().AnyAsync(x => x.Id == collectionId, ct)) throw new KeyNotFoundException("Không tìm thấy khoản thu.");
        var provider = await providers.ResolveCurrentAsync(ct);
        var cfg = await configReader.GetAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await LockCollectionChainAsync(collectionId, ct);
        if (c.Status is not (CollectionStates.Paid or CollectionStates.Refunding))
            throw new DepotConflictException("Chỉ hoàn tiền được khoản thu đã thanh toán (PAID) hoặc đang chờ hoàn (REFUNDING).");
        if (await db.MoneyCollectionAnomalies.AsNoTracking().AnyAsync(a => a.CollectionId == c.Id && a.Kind == CollectionAnomalyKinds.SourceMismatch && a.Status == "OPEN", ct))
            throw new DepotConflictException("Khoản thu này có bất thường 'nguồn đã đổi trạng thái' đang mở: hãy hoàn tiền từ màn hình Bất thường để tránh hoàn trùng.");
        var committed = await db.MoneyRefunds.AsNoTracking().Where(r => r.CollectionId == c.Id && r.Status != RefundStatuses.Cancelled
            && (r.Kind == RefundKinds.Full || r.Kind == RefundKinds.Partial)).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var remaining = c.Amount - committed;
        if (remaining <= 0) throw new DepotConflictException("Khoản thu này đã được hoàn đủ hoặc đã có lệnh hoàn bao trùm toàn bộ số tiền.");
        decimal value;
        if (kind == RefundKinds.Full)
        {
            if (amount != null && amount != remaining) throw new ArgumentException($"Hoàn toàn phần phải hoàn đúng số còn lại {MoneyFormat.Vnd(remaining)} đồng (bỏ trống số tiền).");
            value = remaining;
        }
        else value = ValidateAmount(amount!.Value, remaining, "Số tiền hoàn một phần", $"Tổng số tiền hoàn không được vượt số tiền khoản thu: còn tối đa {MoneyFormat.Vnd(remaining)} đồng.");
        var payer = await RequirePayerAsync(c.PayerId, ct);

        // Sổ cái: dòng SALE đã vào kỳ chi hoặc đã chi thì không hoàn ở đây (thu hồi từ người nhận nằm ngoài phạm vi).
        PayableLine? sale = null;
        if (c.Status == CollectionStates.Paid)
        {
            sale = (await db.PayableLines.FromSqlInterpolated($"SELECT * FROM payable_lines WHERE source_type = {c.SourceType} AND source_id = {c.SourceId} AND kind = 'SALE' FOR UPDATE")
                .AsNoTracking().ToListAsync(ct)).FirstOrDefault();
            if (sale != null && sale.State is PayableStates.InPayout or PayableStates.Paid)
                throw new DepotConflictException("Tiền của khoản thu này đã nằm trong kỳ chi hoặc đã được chi cho người nhận nên không thể hoàn trực tiếp. Việc thu hồi từ người nhận cần xử lý ngoài hệ thống.");
            if (sale != null && sale.State == PayableStates.Reversed)
                throw new DepotConflictException("Dòng sổ cái của khoản thu này đã bị đảo nên không thể hoàn thêm.");
        }

        var refund = await InsertAsync(c, null, payer, kind, value, reason, actorId, provider.Name, cfg, ct);
        string? ledger = null;
        if (sale != null)
        {
            var line = await new PayableLedgerService(db, configReader, clock).CreateRefundLineAsync(new(c.SourceType, c.SourceId, refund.Code, value, Now), ct);
            ledger = line.Line.Id.ToString();
        }
        // Hoàn tới hết số tiền thì khoản thu sang REFUNDING (nhả ràng buộc nguồn); hoàn một phần dở dang giữ PAID để nguồn còn hiệu lực.
        var before = c.Status;
        if (c.Status == CollectionStates.Paid && committed + value >= c.Amount)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE money_collections SET status = 'REFUNDING', updated_at = {Now} WHERE id = {c.Id} AND status = 'PAID'", ct);
        }
        db.AuditLogs.Add(Audit(actorId, "REFUND_CREATED", refund.Id, null, new { refund.Code, refund.Kind, refund.Amount, refund.Status, collectionId = c.Id, collectionStatusBefore = before, ledgerLineId = ledger }));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await DispatchAsync(refund, ct);
    }

    private async Task<MoneyRefund> InsertAsync(MoneyCollection c, Guid? anomalyId, User payer, string kind, decimal amount, string reason, Guid actorId, string providerName, MoneyConfig cfg, CancellationToken ct)
    {
        var bank = PayoutRules.CheckBank(payer.BankBin, payer.BankAccountNumber, payer.BankAccountName, payer.BankStatus);
        if (!bank.Ready) throw new DepotConflictException(RefundRules.BankRequiredMessage);
        var now = Now;
        var status = amount > cfg.PayoutAutoMaxAmount ? RefundStatuses.AwaitingApproval : RefundStatuses.Processing;
        var refund = new MoneyRefund
        {
            Code = await NextCodeAsync(ct), CollectionId = c.Id, AnomalyId = anomalyId, PayerId = c.PayerId, Kind = kind, Amount = amount, Reason = reason, Status = status,
            Provider = providerName, FailReason = status == RefundStatuses.AwaitingApproval
                ? $"Vượt hạn mức tự chi {MoneyFormat.Vnd(cfg.PayoutAutoMaxAmount)} đồng: cần Admin duyệt." : null,
            Attempt = 1, BankSnapshot = SnapshotJson(payer), CreatedBy = actorId, CreatedAt = now, UpdatedAt = now,
        };
        db.MoneyRefunds.Add(refund);
        db.Notifications.Add(Notify(c.PayerId, refund, "Sàn sẽ hoàn tiền cho bạn",
            $"Lệnh hoàn {refund.Code}: sàn sẽ hoàn {MoneyFormat.Vnd(amount)} đồng về tài khoản ngân hàng của bạn. Lý do: {reason}"));
        await db.SaveChangesAsync(ct);
        return refund;
    }

    /// <summary>Khóa theo thứ tự: (depot) → nguồn → khoản thu. Trả khoản thu (không theo dõi) sau khi đã khóa.</summary>
    private async Task<MoneyCollection> LockCollectionChainAsync(Guid collectionId, CancellationToken ct)
    {
        var head = await db.MoneyCollections.AsNoTracking().Where(x => x.Id == collectionId).Select(x => new { x.SourceType, x.SourceId }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy khoản thu.");
        switch (head.SourceType)
        {
            case MoneySources.PickupRequest:
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM pickup_requests WHERE id = {head.SourceId} FOR UPDATE", ct);
                break;
            case MoneySources.InventoryBatch:
                var depot = await db.InventoryBatches.AsNoTracking().Where(b => b.Id == head.SourceId).Select(b => (Guid?)b.DepotId).SingleOrDefaultAsync(ct);
                if (depot != null) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM depots WHERE id = {depot.Value} FOR UPDATE", ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM inventory_batches WHERE id = {head.SourceId} FOR UPDATE", ct);
                break;
            case MoneySources.LiquidationAgreement:
                var ag = await db.LiquidationAgreements.AsNoTracking().Where(a => a.Id == head.SourceId).Select(a => new { a.DepotId, a.LotId }).SingleOrDefaultAsync(ct);
                if (ag != null)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM depots WHERE id = {ag.DepotId} FOR UPDATE", ct);
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM liquidation_lots WHERE id = {ag.LotId} FOR UPDATE", ct);
                }
                break;
        }
        return await db.MoneyCollections.FromSqlInterpolated($"SELECT * FROM money_collections WHERE id = {collectionId} FOR UPDATE").AsNoTracking().SingleAsync(ct);
    }

    private async Task<User> RequirePayerAsync(Guid payerId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payerId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người trả.");
        if (!u.IsActive) throw new DepotConflictException("Tài khoản người trả không còn hoạt động nên chưa thể hoàn tiền.");
        if (!PayoutRules.CheckBank(u.BankBin, u.BankAccountNumber, u.BankAccountName, u.BankStatus).Ready) throw new DepotConflictException(RefundRules.BankRequiredMessage);
        return u;
    }

    private static string ValidateReason(string? reason)
    {
        var r = (reason ?? "").Trim();
        if (r.Length is < 5 or > MaxReasonLength) throw new ArgumentException($"Lý do hoàn tiền phải từ 5 đến {MaxReasonLength} ký tự.");
        return r;
    }

    private static decimal ValidateAmount(decimal v, decimal max, string label, string? overMessage = null)
    {
        if (v != decimal.Truncate(v) || v <= 0) throw new ArgumentException($"{label} phải là số nguyên đồng lớn hơn 0.");
        if (v > max) throw new ArgumentException(overMessage ?? $"{label} không được vượt {MoneyFormat.Vnd(max)} đồng.");
        return v;
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var hex = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 25; i++)
        {
            var code = RefundRules.Code(Now, hex, i);
            if (!await db.MoneyRefunds.AsNoTracking().AnyAsync(r => r.Code == code, ct)) return code;
        }
        throw new InvalidOperationException("Không sinh được mã hoàn tiền duy nhất.");
    }

    // ============================ GỬI LỆNH HOÀN (NGOÀI TRANSACTION) ============================

    private async Task<MoneyRefund> DispatchAsync(MoneyRefund created, CancellationToken ct)
    {
        if (created.Status != RefundStatuses.Processing) return await LoadAsync(created.Id, ct);
        try { return await SubmitAsync(created.Id, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            log.LogWarning(ex, "Gửi lệnh hoàn {Refund} thất bại, worker đối soát sẽ xử lý.", created.Code);
            return await LoadAsync(created.Id, CancellationToken.None);
        }
    }

    /// <summary>
    /// Gửi lệnh hoàn cho lệnh PROCESSING. Không mở transaction: chiếm lượt gửi (CAS) → kiểm tra số dư → PayoutAsync → ghi kết quả (CAS).
    /// Accepted: giữ PROCESSING kèm provider_ref. DefiniteFailure: FAILED. Unknown: UNKNOWN (không gửi lại bằng khóa mới). Thiếu số dư: PENDING, chưa gọi nhà cung cấp.
    /// </summary>
    public async Task<MoneyRefund> SubmitAsync(Guid refundId, CancellationToken ct = default)
    {
        var r = await LoadAsync(refundId, ct);
        if (r.Status != RefundStatuses.Processing) return r;
        var provider = providers.GetByName(r.Provider);
        var snap = ParseSnapshot(r.BankSnapshot);
        if (snap == null)
        {
            await CasAsync(r, RefundStatuses.Failed, "Thiếu thông tin tài khoản ngân hàng của người trả.");
            await NotifyPayerAsync(r, "Hoàn tiền chưa thành công", $"Lệnh hoàn {r.Code} chưa thực hiện được: thiếu thông tin tài khoản ngân hàng. Sàn đang xử lý.");
            return await LoadAsync(refundId, ct);
        }
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE money_refunds SET last_attempt_at = {Now}, version = version + 1
            WHERE id = {r.Id} AND status = 'PROCESSING' AND version = {r.Version}", ct);
        if (claimed == 0) return await LoadAsync(refundId, ct);
        r.Version++;

        decimal balance;
        try { balance = await provider.GetBalanceAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Không kiểm tra được số dư trước khi hoàn {Refund}.", r.Code);
            await CasAsync(r, RefundStatuses.Pending, "Không kiểm tra được số dư tài khoản chi, chưa gửi lệnh. Hãy thử lại sau.");
            return await LoadAsync(refundId, ct);
        }
        if (balance < r.Amount)
        {
            await CasAsync(r, RefundStatuses.Pending, $"Số dư tài khoản chi ({MoneyFormat.Vnd(balance)}) không đủ cho lệnh hoàn {r.Code} ({MoneyFormat.Vnd(r.Amount)}). Chưa gửi lệnh.");
            await NotifyAdminsAsync(r, "Lệnh hoàn chờ do thiếu số dư", $"Lệnh hoàn {r.Code} ({MoneyFormat.Vnd(r.Amount)} đồng) chưa gửi được vì số dư tài khoản chi không đủ.", ct);
            return await LoadAsync(refundId, ct);
        }

        var key = PayoutRules.KeyFor(r.Code, r.Attempt);
        PayoutResult result;
        try { result = await provider.PayoutAsync(new(key, key, r.Amount, RefundRules.Description(r.Code), snap.Bin, snap.Account), ct); }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Gọi nhà cung cấp hoàn {Refund} bị gián đoạn, coi là chưa rõ.", r.Code);
            result = new(PayoutOutcome.Unknown, null, "EXCEPTION", "Không nhận được phản hồi từ nhà cung cấp chi.");
        }

        switch (result.Outcome)
        {
            case PayoutOutcome.Accepted:
                await CasAsync(r, RefundStatuses.Processing, null, providerRef: result.ProviderRef, gateway: "ACCEPTED");
                break;
            case PayoutOutcome.DefiniteFailure:
                if (await CasAsync(r, RefundStatuses.Failed, Trim($"Nhà cung cấp từ chối lệnh chi: {result.Message ?? result.ErrorCode}"), gateway: "REJECTED"))
                    await OnFailedAsync(r, CancellationToken.None);
                break;
            default:
                await CasAsync(r, RefundStatuses.Unknown, "Chưa rõ lệnh hoàn đã được nhận chưa (timeout/lỗi). Đối soát bằng cùng khóa trước khi làm gì khác.", gateway: "UNKNOWN");
                break;
        }
        return await LoadAsync(refundId, CancellationToken.None);
    }

    // ============================ ĐỐI SOÁT ============================

    /// <summary>Hỏi nhà cung cấp về lệnh PROCESSING/UNKNOWN. Succeeded → PAID; Failed → FAILED; NotFound ≥ 10 phút kể từ lần gửi và chưa có provider_ref → PENDING (gửi lại CÙNG khóa).</summary>
    public async Task<MoneyRefund> ReconcileAsync(Guid refundId, Guid? actorId = null, CancellationToken ct = default)
    {
        var r = await LoadAsync(refundId, ct);
        if (r.Status is not (RefundStatuses.Processing or RefundStatuses.Unknown)) return r;
        if (r.Status == RefundStatuses.Processing && r.LastAttemptAt == null) return await SubmitAsync(refundId, ct);
        var provider = providers.GetByName(r.Provider);
        var status = await provider.GetPayoutAsync(r.ProviderRef ?? PayoutRules.KeyFor(r.Code, r.Attempt), ct);
        switch (status.Status)
        {
            case PayoutStatus.Succeeded:
                await FinalizePaidAsync(r, actorId, status.ProviderRef, "SUCCEEDED", null, ct);
                break;
            case PayoutStatus.Failed:
                if (await CasAsync(r, RefundStatuses.Failed, Trim($"Nhà cung cấp báo chuyển thất bại: {status.Message}"), providerRef: status.ProviderRef, gateway: "FAILED"))
                {
                    await AuditAsync(actorId, "REFUND_FAILED", r.Id, new { r.Code, status.Message }, ct);
                    await OnFailedAsync(r, ct);
                }
                break;
            case PayoutStatus.Processing:
                if (r.Status == RefundStatuses.Unknown) await CasAsync(r, RefundStatuses.Processing, null, providerRef: status.ProviderRef, gateway: "PROCESSING");
                else await SetGatewayAsync(r.Id, "PROCESSING", status.ProviderRef);
                break;
            default:
                if (r.ProviderRef == null && r.LastAttemptAt != null && Now - PayoutRules.Utc(r.LastAttemptAt.Value) >= PayoutRules.NotFoundGrace)
                    await CasAsync(r, RefundStatuses.Pending, "Nhà cung cấp không ghi nhận lệnh sau 10 phút: coi là chưa nhận. Có thể gửi lại cùng khóa.", gateway: "NOT_FOUND");
                else await SetGatewayAsync(r.Id, "NOT_FOUND", null);
                break;
        }
        return await LoadAsync(refundId, ct);
    }

    /// <summary>Lệnh hoàn xong (tự động hoặc chi tay): bất thường gốc → RESOLVED; khoản thu REFUNDING → REFUNDED khi tổng đã chi bằng số tiền khoản thu; thông báo người trả.</summary>
    private async Task<bool> FinalizePaidAsync(MoneyRefund r, Guid? actor, string? providerRef, string gateway, string? manualNote, CancellationToken ct)
    {
        var fromStatus = r.Status;
        var oldRef = r.ProviderRef;
        var to = manualNote == null ? RefundStatuses.Paid : RefundStatuses.ManualPaid;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM money_collections WHERE id = {r.CollectionId} FOR UPDATE", ct);
        if (!await CasAsync(r, to, null, providerRef: providerRef, gateway: gateway, paidAt: Now)) return false;
        var now = Now;
        if (r.Kind is RefundKinds.Full or RefundKinds.Partial)
        {
            var paid = await db.MoneyRefunds.AsNoTracking().Where(x => x.CollectionId == r.CollectionId && (x.Status == RefundStatuses.Paid || x.Status == RefundStatuses.ManualPaid)
                && (x.Kind == RefundKinds.Full || x.Kind == RefundKinds.Partial)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE money_collections SET status = 'REFUNDED', updated_at = {now} WHERE id = {r.CollectionId} AND status = 'REFUNDING' AND amount <= {paid}", ct);
        }
        if (r.AnomalyId != null)
        {
            var note = $"Đã hoàn tiền {r.Code}";
            await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE money_collection_anomalies SET status = 'RESOLVED', resolved_at = {now}, resolved_by = CAST({actor} AS uuid),
                note = CASE WHEN note IS NULL OR note = '' THEN CAST({note} AS text) ELSE note || ' | ' || CAST({note} AS text) END WHERE id = {r.AnomalyId} AND status = 'OPEN'", ct);
        }
        db.AuditLogs.Add(Audit(actor, manualNote == null ? "REFUND_PAID" : "REFUND_MARKED_PAID", r.Id, new { status = fromStatus, providerRef = oldRef },
            new { r.Code, r.Amount, providerRef, gateway, note = manualNote, r.CollectionId, r.AnomalyId }));
        db.Notifications.Add(Notify(r.PayerId, r, "Đã hoàn tiền", $"Lệnh hoàn {r.Code}: sàn đã hoàn {MoneyFormat.Vnd(r.Amount)} đồng về tài khoản ngân hàng của bạn."));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return true;
    }

    private async Task OnFailedAsync(MoneyRefund r, CancellationToken ct)
    {
        await NotifyPayerAsync(r, "Hoàn tiền chưa thành công", $"Lệnh hoàn {r.Code} chưa chuyển được. Sàn sẽ kiểm tra và xử lý lại, bạn không cần làm gì thêm.");
        await NotifyAdminsAsync(r, "Lệnh hoàn thất bại", $"Lệnh hoàn {r.Code} ({MoneyFormat.Vnd(r.Amount)} đồng) bị từ chối: hãy kiểm tra tài khoản người trả rồi gửi lại.", ct);
    }

    // ============================ THAO TÁC CỦA ADMIN ============================

    /// <summary>Duyệt lệnh AWAITING_APPROVAL. Tài khoản người trả phải còn VERIFIED (nếu không: 409, chưa gửi).</summary>
    public async Task<MoneyRefund> ApproveAsync(Guid refundId, Guid actorId, CancellationToken ct = default)
    {
        var r = await LoadAsync(refundId, ct);
        if (r.Status != RefundStatuses.AwaitingApproval) throw new DepotConflictException("Lệnh hoàn không ở trạng thái chờ duyệt.");
        var snapshot = await CurrentSnapshotAsync(r, refresh: r.LastAttemptAt == null, ct);
        if (!await CasAsync(r, RefundStatuses.Processing, null, approvedBy: actorId, approvedAt: Now, snapshot: snapshot))
            throw new DepotConflictException("Lệnh hoàn vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        await AuditAsync(actorId, "REFUND_APPROVED", r.Id, new { r.Code, r.Amount }, ct);
        return await SubmitAsync(refundId, ct);
    }

    /// <summary>FAILED: lần gửi mới với khóa mới code-N, lấy lại tài khoản hiện tại. PENDING: gửi lại CÙNG khóa và cùng tài khoản đã chốt.</summary>
    public async Task<MoneyRefund> RetryAsync(Guid refundId, Guid actorId, CancellationToken ct = default)
    {
        var r = await LoadAsync(refundId, ct);
        if (r.Status is not (RefundStatuses.Failed or RefundStatuses.Pending)) throw new DepotConflictException("Chỉ gửi lại được lệnh hoàn đang FAILED hoặc PENDING.");
        var failed = r.Status == RefundStatuses.Failed;
        var snapshot = await CurrentSnapshotAsync(r, refresh: failed || r.LastAttemptAt == null, ct);
        if (!await CasAsync(r, RefundStatuses.Processing, null, snapshot: snapshot, bumpAttempt: failed, clearProviderRef: failed))
            throw new DepotConflictException("Lệnh hoàn vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        await AuditAsync(actorId, "REFUND_RETRY", r.Id, new { r.Code, from = failed ? "FAILED" : "PENDING", attempt = r.Attempt }, ct);
        return await SubmitAsync(refundId, ct);
    }

    /// <summary>
    /// Hủy lệnh chưa gửi hoặc đã thất bại rõ ràng (PENDING/AWAITING_APPROVAL/FAILED). Đảo dòng REFUND trong sổ cái nếu chưa vào kỳ chi;
    /// đã vào kỳ chi thì 409. Khoản thu REFUNDING do chính lệnh này đặt được trả về PAID khi không còn lệnh hoàn nào hiệu lực.
    /// </summary>
    public async Task<MoneyRefund> CancelAsync(Guid refundId, Guid actorId, string? note, CancellationToken ct = default)
    {
        var r = await LoadAsync(refundId, ct);
        if (r.Status is not (RefundStatuses.Pending or RefundStatuses.AwaitingApproval or RefundStatuses.Failed))
            throw new DepotConflictException("Chỉ hủy được lệnh hoàn đang PENDING, AWAITING_APPROVAL hoặc FAILED. Lệnh đang chạy cần đối soát hoặc đánh dấu đã hoàn tay.");
        if (note is { Length: > 500 }) throw new ArgumentException("Ghi chú tối đa 500 ký tự.");
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await LockCollectionChainAsync(r.CollectionId, ct);
        var line = (await db.PayableLines.FromSqlInterpolated($"SELECT * FROM payable_lines WHERE source_type = {c.SourceType} AND source_id = {c.SourceId} AND kind = 'REFUND' AND refund_key = {r.Code} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct)).FirstOrDefault();
        if (line != null && (line.PayoutId != null || line.State is PayableStates.InPayout or PayableStates.Paid))
            throw new DepotConflictException("Dòng trừ trong sổ cái của lệnh hoàn này đã nằm trong kỳ chi nên không thể hủy.");
        if (!await CasAsync(r, RefundStatuses.Cancelled, Trim(note ?? "Admin hủy lệnh hoàn.")))
            throw new DepotConflictException("Lệnh hoàn vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        if (line != null)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payable_lines SET state = 'REVERSED' WHERE id = {line.Id} AND payout_id IS NULL AND state IN ('AVAILABLE', 'HELD')", ct);
        if (c.Status == CollectionStates.Refunding && r.Kind is RefundKinds.Full or RefundKinds.Partial)
        {
            var stillActive = await db.MoneyRefunds.AsNoTracking().AnyAsync(x => x.CollectionId == c.Id && x.Id != r.Id && x.Status != RefundStatuses.Cancelled
                && (x.Kind == RefundKinds.Full || x.Kind == RefundKinds.Partial), ct);
            var hookKey = $"FULL-{c.Id:N}"; // dòng hoàn do MarkRefundingAsync (luồng cũ hủy đơn) tạo: khoản thu phải ở lại REFUNDING
            var hooked = await db.PayableLines.AsNoTracking().AnyAsync(l => l.SourceType == c.SourceType && l.SourceId == c.SourceId && l.Kind == PayableKinds.Refund && l.RefundKey == hookKey, ct);
            if (!stillActive && !hooked)
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE money_collections SET status = 'PAID', updated_at = {Now} WHERE id = {c.Id} AND status = 'REFUNDING'", ct);
        }
        db.AuditLogs.Add(Audit(actorId, "REFUND_CANCELLED", r.Id, new { status = r.Status }, new { r.Code, r.Amount, note, ledgerReversed = line != null }));
        db.Notifications.Add(Notify(r.PayerId, r, "Lệnh hoàn tiền đã hủy", $"Lệnh hoàn {r.Code} đã bị hủy. Sàn sẽ liên hệ nếu cần hoàn lại."));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await LoadAsync(refundId, ct);
    }

    /// <summary>
    /// Đánh dấu đã hoàn tay (Admin chuyển khoản ngoài hệ thống). reference là mã giao dịch ngân hàng (duy nhất, lưu vào provider_ref).
    /// PROCESSING/UNKNOWN bắt buộc confirmed vì lệnh có thể đang chạy ở nhà cung cấp: hoàn tay thêm sẽ hoàn trùng.
    /// </summary>
    public async Task<MoneyRefund> MarkPaidManuallyAsync(Guid refundId, Guid actorId, string reference, string? note, bool confirmed, CancellationToken ct = default)
    {
        var r = await LoadAsync(refundId, ct);
        reference = (reference ?? "").Trim();
        if (reference.Length is 0 or > 100) throw new ArgumentException("Cần nhập mã giao dịch hoàn tay (tối đa 100 ký tự).");
        if (note is { Length: > 500 }) throw new ArgumentException("Ghi chú tối đa 500 ký tự.");
        if (r.Status is not (RefundStatuses.Pending or RefundStatuses.Failed or RefundStatuses.Processing or RefundStatuses.Unknown or RefundStatuses.AwaitingApproval))
            throw new DepotConflictException("Lệnh hoàn ở trạng thái này không thể đánh dấu đã hoàn tay.");
        if (r.Status is RefundStatuses.Processing or RefundStatuses.Unknown && !confirmed)
            throw new DepotConflictException("Lệnh hoàn có thể đang chạy ở nhà cung cấp. Hãy đối soát trước, hoặc xác nhận (confirm = true) rằng bạn đã chuyển tay và chấp nhận rủi ro hoàn trùng.");
        if (await db.MoneyRefunds.AsNoTracking().AnyAsync(x => x.ProviderRef == reference && x.Id != r.Id, ct))
            throw new DepotConflictException("Mã giao dịch này đã được dùng cho lệnh hoàn khác.");
        bool ok;
        try { ok = await FinalizePaidAsync(r, actorId, reference, "MANUAL", note ?? "", ct); }
        catch (DbUpdateException) { throw new DepotConflictException("Mã giao dịch này đã được dùng cho lệnh hoàn khác."); }
        if (!ok) throw new DepotConflictException("Lệnh hoàn vừa được xử lý bởi thao tác khác. Hãy tải lại.");
        return await LoadAsync(refundId, ct);
    }

    // ============================ TRUY VẤN DÙNG CHUNG VỚI WORKER ============================

    /// <summary>Lệnh cần đối soát: PROCESSING/UNKNOWN có lần gửi cuối (hoặc lúc tạo) cũ hơn minAge, cũ nhất trước.</summary>
    public async Task<List<Guid>> DueForReconcileAsync(TimeSpan minAge, int take, CancellationToken ct = default)
    {
        var cutoff = Now - minAge;
        return await db.MoneyRefunds.AsNoTracking().Where(r => (r.Status == RefundStatuses.Processing || r.Status == RefundStatuses.Unknown) && (r.LastAttemptAt ?? r.CreatedAt) <= cutoff)
            .OrderBy(r => r.LastAttemptAt ?? r.CreatedAt).ThenBy(r => r.Id).Take(Math.Clamp(take, 1, 200)).Select(r => r.Id).ToListAsync(ct);
    }

    // ============================ HỖ TRỢ NỘI BỘ ============================

    private async Task<MoneyRefund> LoadAsync(Guid id, CancellationToken ct) =>
        await db.MoneyRefunds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException("Không tìm thấy lệnh hoàn.");

    /// <summary>Tài khoản hiện tại của người trả phải còn đủ điều kiện; refresh = true thì chốt lại snapshot (chỉ khi lệnh chưa từng tới nhà cung cấp hoặc sẽ dùng khóa mới).</summary>
    private async Task<string?> CurrentSnapshotAsync(MoneyRefund r, bool refresh, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.PayerId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người trả.");
        if (!u.IsActive) throw new DepotConflictException("Tài khoản người trả không còn hoạt động nên chưa thể hoàn tiền.");
        if (!PayoutRules.CheckBank(u.BankBin, u.BankAccountNumber, u.BankAccountName, u.BankStatus).Ready) throw new DepotConflictException(RefundRules.BankRequiredMessage);
        return refresh ? SnapshotJson(u) : null;
    }

    private static string SnapshotJson(User u) =>
        JsonSerializer.Serialize(new PayoutEngine.BankSnapshot(u.BankName ?? "", u.BankBin ?? "", u.BankAccountNumber ?? "", u.BankAccountName ?? ""), Json);

    private static PayoutEngine.BankSnapshot? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var s = JsonSerializer.Deserialize<PayoutEngine.BankSnapshot>(json, Json);
            return s != null && PayoutBankRules.IsValidBin(s.Bin) && PayoutBankRules.IsValidAccountNumber(s.Account) ? s : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Compare-and-swap trạng thái trên (status, version). Trả false nếu lệnh đã bị thao tác khác đổi.</summary>
    private async Task<bool> CasAsync(MoneyRefund r, string to, string? reason, string? providerRef = null, string? gateway = null, DateTime? paidAt = null,
        Guid? approvedBy = null, DateTime? approvedAt = null, string? snapshot = null, bool bumpAttempt = false, bool clearProviderRef = false)
    {
        var inc = bumpAttempt ? 1 : 0;
        var now = Now;
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE money_refunds SET status = {to}, fail_reason = CAST({reason} AS text),
            provider_ref = CASE WHEN {clearProviderRef} THEN NULL ELSE COALESCE(CAST({providerRef} AS varchar), provider_ref) END,
            gateway_status = COALESCE(CAST({gateway} AS varchar), gateway_status), paid_at = COALESCE(CAST({paidAt} AS timestamptz), paid_at),
            approved_by = COALESCE(CAST({approvedBy} AS uuid), approved_by), approved_at = COALESCE(CAST({approvedAt} AS timestamptz), approved_at),
            bank_snapshot = COALESCE(CAST({snapshot} AS jsonb), bank_snapshot), attempt = attempt + {inc}, version = version + 1, updated_at = {now}
            WHERE id = {r.Id} AND status = {r.Status} AND version = {r.Version}", CancellationToken.None);
        if (rows == 1) { r.Status = to; r.Version++; r.Attempt += inc; }
        return rows == 1;
    }

    private Task SetGatewayAsync(Guid id, string gateway, string? providerRef) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE money_refunds SET gateway_status = {gateway}, provider_ref = COALESCE(provider_ref, CAST({providerRef} AS varchar)) WHERE id = {id}");

    private async Task AuditAsync(Guid? actor, string action, Guid id, object data, CancellationToken ct)
    {
        db.AuditLogs.Add(Audit(actor, action, id, null, data));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private AuditLog Audit(Guid? actor, string action, Guid id, object? oldData, object? newData) => new()
    {
        UserId = actor, Action = action, EntityName = "money_refunds", EntityId = id,
        OldData = oldData == null ? null : JsonSerializer.Serialize(oldData, Json), NewData = newData == null ? null : JsonSerializer.Serialize(newData, Json), CreatedAt = Now,
    };

    private Notification Notify(Guid user, MoneyRefund r, string title, string message) => new()
    {
        UserId = user, Category = "REFUND", TargetType = "MONEY_REFUND", TargetId = r.Id, Title = title, Message = message, CreatedAt = Now,
    };

    private async Task NotifyPayerAsync(MoneyRefund r, string title, string message)
    {
        db.Notifications.Add(Notify(r.PayerId, r, title, message));
        await db.SaveChangesAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
    }

    private async Task NotifyAdminsAsync(MoneyRefund r, string title, string message, CancellationToken ct)
    {
        var admins = await db.Users.AsNoTracking().Where(u => u.Role == "ADMIN" && u.IsActive).Select(u => u.Id).OrderBy(x => x).Take(20).ToListAsync(ct);
        foreach (var a in admins) db.Notifications.Add(Notify(a, r, title, message));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static string Trim(string? s) => s is { Length: > 500 } ? s[..500] : s ?? "";
}
