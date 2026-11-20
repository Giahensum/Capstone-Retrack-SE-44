using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Money;

/// <summary>Trạng thái khoản thu cho người gọi. Status = NONE khi chưa tạo khoản thu (chưa bấm thanh toán lần nào).</summary>
/// <param name="Role">PAYER (người trả) hoặc PAYEE (người nhận).</param>
/// <param name="MoneyMode">LEGACY hoặc PLATFORM (khóa money.mode).</param>
/// <param name="CanPay">true khi người gọi là người trả, chế độ PLATFORM, nguồn đang ở bước cho phép thanh toán và khoản thu chưa PAID.</param>
/// <param name="CheckoutUrl">Chỉ trả cho người trả khi khoản thu còn PENDING.</param>
/// <param name="RefundStatus">Trạng thái lệnh hoàn mới nhất chưa hủy của khoản thu (chỉ người trả thấy); null nếu chưa có.</param>
/// <param name="HasOpenAnomaly">Có tiền về bất thường (thiếu/dư/muộn/trùng) đang chờ Admin xử lý.</param>
public sealed record MoneyCollectionDto(Guid? Id, string Flow, Guid SourceId, decimal Amount, string Status, string Role, string MoneyMode, bool CanPay,
    string? CheckoutUrl, long? OrderCode, DateTime? PaidAt, decimal? ReceivedAmount, bool HasOpenAnomaly, string? Message, string? RefundStatus = null);

public enum MoneyWebhookResult { Invalid, NotMine, Handled }

public interface IMoneyCollectionService
{
    Task<MoneyCollectionDto> GetAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct = default);
    Task<PayOsLinkDto> CreateLinkAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct = default);
    Task<MoneyCollectionDto> SyncAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct = default);
    /// <summary>Webhook PayOS đã ký: Invalid = sai chữ ký; NotMine = mã đơn không thuộc khoản thu nào (để luồng hóa đơn xử lý); Handled = đã xử lý (kể cả no-op).</summary>
    Task<MoneyWebhookResult> HandleWebhookAsync(JsonElement body, CancellationToken ct = default);
    Task<List<Guid>> PendingCollectionIdsAsync(int take, CancellationToken ct = default);
    Task<bool> ReconcileAsync(Guid collectionId, CancellationToken ct = default);
    /// <summary>Khoản thu PAID sang REFUNDING (Admin hoàn tay); nếu đã có dòng SALE thì tạo dòng REFUND tương ứng. Dùng transaction hiện hành nếu có.</summary>
    Task<bool> MarkRefundingAsync(string flow, Guid sourceId, string reason, Guid? actorId, CancellationToken ct = default);
    /// <summary>Khoản thu PENDING sang CANCELLED khi nguồn bị hủy trước khi thanh toán.</summary>
    Task<bool> CancelUnpaidAsync(string flow, Guid sourceId, string reason, Guid? actorId, CancellationToken ct = default);
}

/// <summary>
/// Thu tiền vào sàn qua PayOS cho ba dòng tiền (docs/thiet-ke-thu-chi-qua-san.md mục 5). Chỉ có hiệu lực khi money.mode = PLATFORM.
/// Khóa dòng theo thứ tự cố định: (depot) → nguồn (pickup / lô / lô thanh lý) → khoản thu. Không gọi PayOS khi đang giữ khóa.
/// Mã đơn PayOS: cùng bộ sinh với hóa đơn phí (unix ms * 1000 + ngẫu nhiên), kiểm tra không trùng platform_invoices và money_collection_orders.
/// </summary>
public sealed class MoneyCollectionService(AppDbContext db, Retrack.API.Services.Depot.IPayOsClient payos, MoneyConfigReader config,
    PayableLedgerService ledger, TimeProvider clock, ILogger<MoneyCollectionService> logger) : IMoneyCollectionService
{
    private const string Platform = MoneyConfigKeys.ModePlatform;

    private sealed record SourceInfo(string SourceType, Guid PayerId, Guid PayeeId, decimal Amount, string Description, string? BlockReason);
    private sealed record SourceLock(PickupRequest? Pickup, InventoryBatch? Batch, LiquidationAgreement? Agreement);
    private sealed record LinkPlan(Guid CollectionId, decimal Amount, long? OldCode, string? OldUrl, bool ForceNew, string Description);

    public static bool IsFlow(string flow) => flow is MoneyFlows.Pickup or MoneyFlows.Batch or MoneyFlows.LiquidationDeposit or MoneyFlows.LiquidationBalance;
    public static string SourceTypeOf(string flow) => flow switch
    {
        MoneyFlows.Pickup => MoneySources.PickupRequest,
        MoneyFlows.Batch => MoneySources.InventoryBatch,
        MoneyFlows.LiquidationDeposit or MoneyFlows.LiquidationBalance => MoneySources.LiquidationAgreement,
        _ => throw new ArgumentException("Dòng tiền không hợp lệ.")
    };
    private static void RequireFlow(string flow) { if (!IsFlow(flow)) throw new ArgumentException("Dòng tiền không hợp lệ."); }
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------- đọc ----------

    public async Task<MoneyCollectionDto> GetAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct = default)
    {
        RequireFlow(flow);
        await RequireActiveUserAsync(userId, ct);
        var info = await ResolveAsync(flow, sourceId, ct);
        if (info.PayerId != userId && info.PayeeId != userId) throw new DepotForbiddenException();
        var cfg = await config.GetAsync(ct);
        var c = await LatestAsync(flow, sourceId, ct);
        return await ToDtoAsync(c, flow, sourceId, info, userId, cfg, ct);
    }

    private async Task<MoneyCollection?> LatestAsync(string flow, Guid sourceId, CancellationToken ct) =>
        await db.MoneyCollections.AsNoTracking().Where(x => x.Flow == flow && x.SourceId == sourceId)
            .OrderByDescending(x => x.Status == CollectionStates.Pending || x.Status == CollectionStates.Paid).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);

    private async Task<MoneyCollectionDto> ToDtoAsync(MoneyCollection? c, string flow, Guid sourceId, SourceInfo info, Guid userId, MoneyConfig cfg, CancellationToken ct)
    {
        var payer = info.PayerId == userId;
        var open = c != null && await db.MoneyCollectionAnomalies.AsNoTracking().AnyAsync(a => a.CollectionId == c.Id && a.Status == "OPEN", ct);
        var refundStatus = payer && c != null
            ? await db.MoneyRefunds.AsNoTracking().Where(r => r.CollectionId == c.Id && r.PayerId == userId && r.Status != RefundStatuses.Cancelled).OrderByDescending(r => r.CreatedAt).Select(r => r.Status).FirstOrDefaultAsync(ct)
            : null;
        var status = c?.Status ?? "NONE";
        var canPay = payer && cfg.IsPlatformMode && info.BlockReason == null && (c == null || c.Status == CollectionStates.Pending);
        var message = cfg.IsPlatformMode ? (c?.Status == CollectionStates.Paid ? null : info.BlockReason) : "Chế độ thu chi qua sàn chưa được bật.";
        return new(c?.Id, flow, sourceId, c?.Amount ?? info.Amount, status, payer ? "PAYER" : "PAYEE", cfg.Mode, canPay,
            payer && c?.Status == CollectionStates.Pending ? c.PayosCheckoutUrl : null, payer ? c?.PayosOrderCode : null, c?.PaidAt, c?.ReceivedAmount, open, message, refundStatus);
    }

    private async Task RequireActiveUserAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, ct))
            throw new DepotForbiddenException("Tài khoản không còn hoạt động.");
    }

    // ---------- nguồn ----------

    /// <summary>Đọc nguồn (không khóa): người trả/nhận suy ra từ dòng dữ liệu, số tiền chuẩn và lý do chưa cho thanh toán (nếu có).</summary>
    private async Task<SourceInfo> ResolveAsync(string flow, Guid sourceId, CancellationToken ct)
    {
        var tag = sourceId.ToString("N")[..8].ToUpperInvariant();
        switch (flow)
        {
            case MoneyFlows.Pickup:
            {
                var r = await (from p in db.PickupRequests.AsNoTracking() where p.Id == sourceId
                               join d in db.Depots.AsNoTracking() on p.TargetDepotId equals d.Id
                               select new { p.Status, p.SellerId, p.GrossAmount, Owner = d.OwnerId }).SingleOrDefaultAsync(ct)
                    ?? throw new KeyNotFoundException("Không tìm thấy đơn thu gom.");
                var block = r.Status != "AWAITING_PAYMENT" ? "Chỉ thanh toán đơn thu gom đang chờ thanh toán." : null;
                return new(MoneySources.PickupRequest, r.Owner, r.SellerId, r.GrossAmount, $"RT THU MUA {tag}", block);
            }
            case MoneyFlows.Batch:
            {
                var r = await (from b in db.InventoryBatches.AsNoTracking() where b.Id == sourceId && b.TargetFactoryId != null
                               join f in db.Factories.AsNoTracking() on b.TargetFactoryId equals f.Id
                               join d in db.Depots.AsNoTracking() on b.DepotId equals d.Id
                               select new { b.Status, b.SettlementWorkflowVersion, b.SettlementApprovedAt, b.InvoiceTotalAmount, b.Code, Payer = f.OwnerId, Payee = d.OwnerId })
                    .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không tìm thấy lô giao nhà máy.");
                var block = r.Status != "SETTLEMENT_ACCEPTED" || r.SettlementWorkflowVersion < 1 || r.SettlementApprovedAt == null
                    ? "Chỉ thanh toán lô khi chủ kho đã đồng ý giá quyết toán." : null;
                var code = string.IsNullOrWhiteSpace(r.Code) ? tag : r.Code!;
                return new(MoneySources.InventoryBatch, r.Payer, r.Payee, r.InvoiceTotalAmount ?? 0m, Short($"RT LO {code}"), block);
            }
            default:
            {
                var r = await (from a in db.LiquidationAgreements.AsNoTracking() where a.Id == sourceId
                               join l in db.LiquidationLots.AsNoTracking() on a.LotId equals l.Id
                               select new { a.BuyerId, l.SellerId, a.Amount, a.DepositAmount, a.SellerSignedAt, a.BuyerSignedAt, a.DepositConfirmedAt, a.PaymentConfirmedAt })
                    .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không tìm thấy thỏa thuận thanh lý.");
                if (flow == MoneyFlows.LiquidationDeposit)
                {
                    var block = r.DepositAmount <= 0 ? "Thỏa thuận này không có tiền cọc."
                        : r.SellerSignedAt == null || r.BuyerSignedAt == null ? "Cần cả hai bên ký hợp đồng đặt cọc trước khi thanh toán cọc."
                        : r.DepositConfirmedAt != null ? "Tiền cọc đã được ghi nhận." : null;
                    return new(MoneySources.LiquidationAgreement, r.BuyerId, r.SellerId, r.DepositAmount, $"RT COC {tag}", block);
                }
                var balBlock = r.DepositAmount > 0 && r.DepositConfirmedAt == null ? "Cần thanh toán và xác nhận tiền cọc trước khi thanh toán phần còn lại."
                    : r.PaymentConfirmedAt != null ? "Thỏa thuận đã ghi nhận thanh toán phần còn lại." : null;
                return new(MoneySources.LiquidationAgreement, r.BuyerId, r.SellerId, r.Amount - r.DepositAmount, $"RT TL {tag}", balBlock);
            }
        }
    }

    // payOS giới hạn mô tả (25 ký tự với tài khoản thường).
    private static string Short(string s) => s.Length <= 25 ? s : s[..25];

    /// <summary>Khóa dòng nguồn theo giao thức hiện có: lô → khóa depot trước; thanh lý → depot rồi lô thanh lý. Trả null nếu nguồn không tồn tại.</summary>
    private async Task<SourceLock?> LockSourceAsync(string flow, Guid sourceId, CancellationToken ct)
    {
        if (flow == MoneyFlows.Pickup)
        {
            var p = await db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {sourceId} FOR UPDATE").AsTracking().SingleOrDefaultAsync(ct);
            if (p == null) return null;
            await db.Entry(p).ReloadAsync(ct);
            return new(p, null, null);
        }
        if (flow == MoneyFlows.Batch)
        {
            var depotId = await db.InventoryBatches.AsNoTracking().Where(b => b.Id == sourceId).Select(b => (Guid?)b.DepotId).SingleOrDefaultAsync(ct);
            if (depotId == null) return null;
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").AsTracking().LoadAsync(ct);
            var b = await db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {sourceId} FOR UPDATE").AsTracking().SingleOrDefaultAsync(ct);
            if (b == null) return null;
            await db.Entry(b).ReloadAsync(ct);
            await db.Entry(b).Reference(x => x.QualityCheck).LoadAsync(ct);
            return new(null, b, null);
        }
        var head = await db.LiquidationAgreements.AsNoTracking().Where(a => a.Id == sourceId).Select(a => new { a.DepotId, a.LotId }).SingleOrDefaultAsync(ct);
        if (head == null) return null;
        await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {head.DepotId} FOR UPDATE").AsTracking().LoadAsync(ct);
        await db.LiquidationLots.FromSqlInterpolated($"SELECT * FROM liquidation_lots WHERE id = {head.LotId} FOR UPDATE").AsTracking().LoadAsync(ct);
        var ag = await db.LiquidationAgreements.AsTracking().Include(a => a.Lot).SingleOrDefaultAsync(a => a.Id == sourceId, ct);
        if (ag == null) return null;
        await db.Entry(ag).ReloadAsync(ct);
        return new(null, null, ag);
    }

    private async Task<MoneyCollection> LockCollectionAsync(Guid id, CancellationToken ct)
    {
        var c = await db.MoneyCollections.FromSqlInterpolated($"SELECT * FROM money_collections WHERE id = {id} FOR UPDATE").AsTracking().SingleAsync(ct);
        await db.Entry(c).ReloadAsync(ct);
        return c;
    }

    // ---------- tạo link ----------

    public async Task<PayOsLinkDto> CreateLinkAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct = default)
    {
        RequireFlow(flow);
        if (!payos.IsConfigured) throw new DepotConflictException("PayOS chưa sẵn sàng. Vui lòng liên hệ Admin.");
        if (!(await config.GetAsync(ct)).IsPlatformMode)
            throw new DepotConflictException("Chế độ thu chi qua sàn chưa được bật. Vui lòng dùng luồng chuyển khoản hiện tại.");
        await RequireActiveUserAsync(userId, ct);

        var plan = await PlanLinkAsync(userId, flow, sourceId, ct);
        if (plan.OldCode is long old && !plan.ForceNew)
        {
            var current = await QueryPayOsAsync(old, ct);
            if (current.Status is "PAID" or "UNDERPAID" && current.Amount > 0)
            {
                // Tiền đã về trên link cũ: ghi nhận trước (đủ tiền thì PAID), đừng tạo đơn mới.
                await SettleCoreAsync(old, current.Amount, current.Reference, current.PaidAt, userId, ct);
                if ((await db.MoneyCollections.AsNoTracking().SingleAsync(x => x.Id == plan.CollectionId, ct)).Status == CollectionStates.Paid)
                    throw new DepotConflictException("Khoản này đã được thanh toán.");
            }
            else if (current.Status is "PENDING" or "PROCESSING" && !string.IsNullOrEmpty(plan.OldUrl)) return new(plan.OldUrl!, old);
        }

        var code = await ReserveOrderCodeAsync(plan.CollectionId, ct);
        PayOsLink link;
        try { link = await payos.CreatePaymentLinkAsync(code, (long)plan.Amount, plan.Description, PayOsRedirect.Money, $"flow={flow}&source={sourceId}", ct); }
        catch (PayOsException ex) { throw new DepotConflictException(ex.Message); }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await LockCollectionAsync(plan.CollectionId, ct);
        if (c.Status != CollectionStates.Pending) throw new DepotConflictException("Khoản thu không còn ở trạng thái chờ thanh toán.");
        // Có người vừa tạo link khác song song: dùng link đã lưu, link của ta vẫn truy được qua money_collection_orders.
        if (c.PayosOrderCode != plan.OldCode && !string.IsNullOrEmpty(c.PayosCheckoutUrl) && c.PayosOrderCode is long other)
        { await tx.CommitAsync(ct); return new(c.PayosCheckoutUrl!, other); }
        c.PayosOrderCode = code; c.PayosLinkId = link.PaymentLinkId; c.PayosCheckoutUrl = link.CheckoutUrl; c.UpdatedAt = Now;
        Audit(userId, "COLLECTION_LINK_CREATED", c, new { c.Status }, new { c.Status, orderCode = code, c.Amount });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(link.CheckoutUrl, code);
    }

    /// <summary>Pha 1 (transaction ngắn): khóa nguồn, kiểm quyền/trạng thái, tạo-hoặc-lấy khoản thu PENDING rồi nhả khóa trước khi gọi PayOS.</summary>
    private async Task<LinkPlan> PlanLinkAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await LockSourceAsync(flow, sourceId, ct) == null) throw new KeyNotFoundException("Không tìm thấy nguồn thanh toán.");
        var info = await ResolveAsync(flow, sourceId, ct);
        if (info.PayerId != userId) throw new DepotForbiddenException();
        if (info.BlockReason != null) throw new DepotConflictException(info.BlockReason);
        if (info.Amount <= 0 || info.Amount != decimal.Truncate(info.Amount))
            throw new DepotConflictException("Khoản thanh toán chưa có số tiền nguyên VND hợp lệ để thanh toán PayOS. Vui lòng liên hệ Admin.");

        var sourceType = SourceTypeOf(flow);
        var now = Now;
        // Race an toàn: chỉ số duy nhất từng phần ux_money_collections_active_source loại bản thứ hai; nguồn đã bị khóa nên các lần tạo song song tuần tự.
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO money_collections (id, flow, source_type, source_id, payer_id, payee_id, amount, status, created_at, updated_at)
            VALUES ({Guid.NewGuid()}, {flow}, {sourceType}, {sourceId}, {info.PayerId}, {info.PayeeId}, {info.Amount}, 'PENDING', {now}, {now})
            ON CONFLICT (flow, source_id) WHERE status IN ('PENDING', 'PAID') DO NOTHING", ct);
        var c = await db.MoneyCollections.FromSqlInterpolated($"SELECT * FROM money_collections WHERE flow = {flow} AND source_id = {sourceId} AND status IN ('PENDING', 'PAID') FOR UPDATE")
            .AsTracking().SingleAsync(ct);
        await db.Entry(c).ReloadAsync(ct);
        if (c.Status == CollectionStates.Paid) throw new DepotConflictException("Khoản này đã được thanh toán.");
        var forceNew = false;
        if (c.Amount != info.Amount || c.PayerId != info.PayerId || c.PayeeId != info.PayeeId)
        {
            // Số tiền/bên liên quan đổi sau khi tạo link: link cũ không còn đúng, cấp đơn mới.
            c.Amount = info.Amount; c.PayerId = info.PayerId; c.PayeeId = info.PayeeId; c.UpdatedAt = now; forceNew = true;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new(c.Id, c.Amount, c.PayosOrderCode, c.PayosCheckoutUrl, forceNew, info.Description);
    }

    private async Task<PayOsPayment> QueryPayOsAsync(long code, CancellationToken ct)
    {
        try { return await payos.GetPaymentAsync(code, ct); }
        catch (PayOsException ex) { throw new DepotConflictException(ex.Message); }
    }

    /// <summary>Cấp mã đơn mới và ghi vào money_collection_orders TRƯỚC khi gọi PayOS, để mọi link (kể cả link bị bỏ rơi) đều truy được về khoản thu.</summary>
    private async Task<long> ReserveOrderCodeAsync(Guid collectionId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(0, 1000);
            if (await db.PlatformInvoices.AsNoTracking().AnyAsync(i => i.PayosOrderCode == code, ct)) continue;
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO money_collection_orders (order_code, collection_id, created_at) VALUES ({code}, {collectionId}, {Now})
                ON CONFLICT (order_code) DO NOTHING", ct);
            // order_code của money_collections là UNIQUE riêng; mã mới sinh theo thời gian nên chỉ va chạm khi trùng ngẫu nhiên, khi đó thử lại.
            if (inserted == 1 && !await db.MoneyCollections.AsNoTracking().AnyAsync(x => x.PayosOrderCode == code && x.Id != collectionId, ct)) return code;
        }
        throw new DepotConflictException("Không cấp được mã đơn PayOS. Vui lòng thử lại.");
    }

    // ---------- đồng bộ / đối soát ----------

    public async Task<MoneyCollectionDto> SyncAsync(Guid userId, string flow, Guid sourceId, CancellationToken ct = default)
    {
        RequireFlow(flow);
        await RequireActiveUserAsync(userId, ct);
        var info = await ResolveAsync(flow, sourceId, ct);
        if (info.PayerId != userId) throw new DepotForbiddenException();
        var c = await LatestAsync(flow, sourceId, ct) ?? throw new DepotConflictException("Chưa tạo link thanh toán PayOS.");
        if (c.Status == CollectionStates.Pending && c.PayosOrderCode is long code)
        {
            var payment = await QueryPayOsAsync(code, ct);
            if (payment.Status is "PAID" or "UNDERPAID" && payment.Amount > 0)
                await SettleCoreAsync(code, payment.Amount, payment.Reference, payment.PaidAt, userId, ct);
        }
        var cfg = await config.GetAsync(ct);
        // Đọc lại sau khi ghi nhận; nguồn có thể đã chuyển bước nên dựng lại thông tin.
        info = await ResolveAsync(flow, sourceId, ct);
        return await ToDtoAsync(await LatestAsync(flow, sourceId, ct), flow, sourceId, info, userId, cfg, ct);
    }

    public Task<List<Guid>> PendingCollectionIdsAsync(int take, CancellationToken ct = default) =>
        db.MoneyCollections.AsNoTracking().Where(c => c.Status == CollectionStates.Pending && c.PayosOrderCode != null)
            .OrderBy(c => c.UpdatedAt).ThenBy(c => c.Id).Take(Math.Clamp(take, 1, 200)).Select(c => c.Id).ToListAsync(ct);

    public async Task<bool> ReconcileAsync(Guid collectionId, CancellationToken ct = default)
    {
        var c = await db.MoneyCollections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == collectionId, ct);
        if (c == null || c.Status != CollectionStates.Pending || c.PayosOrderCode is not long code) return false;
        var payment = await payos.GetPaymentAsync(code, ct);
        if (payment.Status is not ("PAID" or "UNDERPAID") || payment.Amount <= 0) return false;
        var result = await SettleCoreAsync(code, payment.Amount, payment.Reference, payment.PaidAt, null, ct);
        return result == SettleKind.Paid;
    }

    // ---------- webhook ----------

    public async Task<MoneyWebhookResult> HandleWebhookAsync(JsonElement body, CancellationToken ct = default)
    {
        if (!body.TryGetProperty("data", out var data) || !body.TryGetProperty("signature", out var sig) || sig.ValueKind != JsonValueKind.String) return MoneyWebhookResult.Invalid;
        if (!payos.VerifyWebhookData(data, sig.GetString()!)) return MoneyWebhookResult.Invalid;
        if (!data.TryGetProperty("orderCode", out var oc) || !oc.TryGetInt64(out var orderCode)) return MoneyWebhookResult.NotMine;
        var mine = await db.MoneyCollectionOrders.AsNoTracking().AnyAsync(o => o.OrderCode == orderCode, ct);
        if (!mine) return MoneyWebhookResult.NotMine;
        var ok = body.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True && data.TryGetProperty("code", out var code) && code.GetString() == "00";
        if (!ok) return MoneyWebhookResult.Handled; // thanh toán không thành công: không đổi gì
        var amount = data.TryGetProperty("amount", out var a) && a.TryGetInt64(out var v) ? v : -1;
        var reference = data.TryGetProperty("reference", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        await SettleCoreAsync(orderCode, amount, reference, null, null, ct);
        return MoneyWebhookResult.Handled;
    }

    // ---------- ghi nhận tiền về ----------

    private enum SettleKind { NotMine, Paid, AlreadyPaid, Underpaid, Late, Duplicate, Refunding, Ignored }

    /// <summary>
    /// Ghi nhận tiền về trên một mã đơn (webhook, đồng bộ, đối soát dùng chung). Idempotent: lặp lại không tạo thêm hiệu ứng.
    /// Khóa: nguồn → khoản thu FOR UPDATE. Số tiền do PayOS báo, không bao giờ lấy từ trình duyệt.
    /// </summary>
    private async Task<SettleKind> SettleCoreAsync(long orderCode, long paidAmount, string? reference, DateTime? paidAt, Guid? actor, CancellationToken ct)
    {
        var link = await db.MoneyCollectionOrders.AsNoTracking().SingleOrDefaultAsync(o => o.OrderCode == orderCode, ct);
        if (link == null) return SettleKind.NotMine;
        var head = await db.MoneyCollections.AsNoTracking().Where(x => x.Id == link.CollectionId).Select(x => new { x.Flow, x.SourceId }).SingleAsync(ct);
        var refKey = Trim100(string.IsNullOrWhiteSpace(reference) ? $"{orderCode}:{paidAmount}" : reference!.Trim());
        var now = Now;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await LockSourceAsync(head.Flow, head.SourceId, ct);
        var c = await LockCollectionAsync(link.CollectionId, ct);

        if (paidAmount <= 0) { await tx.CommitAsync(ct); return SettleKind.Ignored; }
        SettleKind result;
        switch (c.Status)
        {
            case CollectionStates.Paid or CollectionStates.Refunding or CollectionStates.Refunded:
                if (c.PaidOrderCode == orderCode) { result = SettleKind.AlreadyPaid; break; } // webhook/đồng bộ gửi lại: không làm gì
                // Một đơn khác của cùng khoản thu cũng nhận tiền: tiền trùng, hoàn toàn bộ phần này.
                await RecordAnomalyAsync(c, CollectionAnomalyKinds.DuplicatePayment, orderCode, paidAmount, paidAmount, refKey, "Khoản thu đã thanh toán, nhận thêm tiền trên link khác.", actor, ct);
                result = SettleKind.Duplicate; break;
            case CollectionStates.Expired or CollectionStates.Cancelled:
                await RecordAnomalyAsync(c, CollectionAnomalyKinds.LatePayment, orderCode, paidAmount, paidAmount, refKey, $"Tiền về sau khi khoản thu {c.Status}.", actor, ct);
                result = SettleKind.Late; break;
            default: // PENDING
                if (paidAmount < c.Amount)
                {
                    // Trả thiếu: không coi là đã trả; toàn bộ số đã nhận cần hoàn tay.
                    await RecordAnomalyAsync(c, CollectionAnomalyKinds.Underpaid, orderCode, paidAmount, paidAmount, refKey, "Số tiền nhận được ít hơn số cần thanh toán.", actor, ct);
                    result = SettleKind.Underpaid; break;
                }
                result = await MarkPaidAsync(c, source, orderCode, paidAmount, paidAt ?? now, reference, refKey, actor, ct);
                break;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<SettleKind> MarkPaidAsync(MoneyCollection c, SourceLock? source, long orderCode, long paidAmount, DateTime paidAt, string? reference, string refKey, Guid? actor, CancellationToken ct)
    {
        var now = Now;
        var before = new { c.Status, c.ReceivedAmount };
        c.Status = CollectionStates.Paid; c.PaidAt = paidAt; c.ReceivedAmount = paidAmount; c.PaidOrderCode = orderCode; c.UpdatedAt = now;
        var payRef = Trim100(string.IsNullOrWhiteSpace(reference) ? $"PAYOS-{orderCode}" : $"PAYOS-{reference!.Trim()}");

        // Hiệu ứng lên nguồn; nếu nguồn đã đổi trạng thái (hủy, đã xử lý tay) thì tiền chờ Admin hoàn (REFUNDING).
        var applied = source != null && c.Flow switch
        {
            MoneyFlows.Pickup => ApplyPickup(c, source.Pickup, now),
            MoneyFlows.Batch => ApplyBatch(c, source.Batch, orderCode, payRef, now),
            MoneyFlows.LiquidationDeposit => ApplyDeposit(c, source.Agreement, payRef, now),
            MoneyFlows.LiquidationBalance => ApplyBalance(c, source.Agreement, payRef, now),
            _ => false
        };
        if (!applied)
        {
            c.Status = CollectionStates.Refunding;
            await RecordAnomalyAsync(c, CollectionAnomalyKinds.SourceMismatch, orderCode, paidAmount, paidAmount, refKey, "Nguồn đã đổi trạng thái nên không thể áp dụng khoản thanh toán; chờ Admin hoàn tiền.", actor, ct);
            Audit(actor, "COLLECTION_REFUNDING", c, before, new { c.Status, c.ReceivedAmount, reason = "SOURCE_MISMATCH" });
            return SettleKind.Refunding;
        }

        Audit(actor, "COLLECTION_PAID", c, before, new { c.Status, c.ReceivedAmount, orderCode, c.Flow });
        db.Notifications.Add(new Notification { UserId = c.PayerId, Category = "PAYMENT", TargetType = "MONEY_COLLECTION", TargetId = c.Id,
            Title = "Sàn đã nhận thanh toán PayOS", Message = $"Sàn đã nhận {paidAmount:N0}đ cho khoản thanh toán {FlowLabel(c.Flow)}." });
        if (paidAmount > c.Amount)
        {
            // Trả dư: vẫn PAID, phần dư ghi nhận để Admin hoàn tay.
            await RecordAnomalyAsync(c, CollectionAnomalyKinds.Overpaid, orderCode, paidAmount, paidAmount - c.Amount, refKey, "Nhận nhiều hơn số cần thanh toán; hoàn phần dư.", actor, ct);
        }
        await db.SaveChangesAsync(ct); // dòng sổ cái (BATCH) đọc khoản thu từ DB nên phải là PAID trước đó
        if (c.Flow == MoneyFlows.Batch) await CreateBatchSaleLineAsync(c, source!.Batch!, now, ct);
        return SettleKind.Paid;
    }

    private static string FlowLabel(string flow) => flow switch
    {
        MoneyFlows.Pickup => "thu mua phế liệu", MoneyFlows.Batch => "lô hàng giao nhà máy",
        MoneyFlows.LiquidationDeposit => "tiền cọc thanh lý", _ => "phần còn lại của lô thanh lý"
    };

    // PICKUP: AWAITING_PAYMENT -> PAYMENT_SENT (cùng bước như MarkSentAsync: không chứng từ, không PlatformTransaction vì hoa hồng thay phí).
    private bool ApplyPickup(MoneyCollection c, PickupRequest? p, DateTime now)
    {
        if (p == null || p.Status != "AWAITING_PAYMENT") return false;
        p.Status = "PAYMENT_SENT"; p.UpdatedAt = now;
        db.Notifications.Add(Notification.ForPickup(p.SellerId, p.Id, "Kho đã thanh toán qua sàn",
            $"Kho đã thanh toán đơn #{p.Id.ToString()[..8].ToUpper()} vào sàn qua PayOS. Vui lòng xác nhận khi đã hoàn tất để nhận tiền theo kỳ chi.", "PAYMENT"));
        return true;
    }

    // BATCH: SETTLEMENT_ACCEPTED -> PAID (trạng thái luồng cũ đạt được khi kho xác nhận nhận tiền); sàn xác nhận thay chủ kho.
    private bool ApplyBatch(MoneyCollection c, InventoryBatch? b, long orderCode, string payRef, DateTime now)
    {
        if (b == null || b.Status != "SETTLEMENT_ACCEPTED" || b.SettlementWorkflowVersion < 1 || b.SettlementApprovedAt == null) return false;
        var from = b.Status;
        b.Status = "PAID"; b.SettledAt = now; b.DepotPaymentConfirmedAt = now; b.DepotPaymentConfirmedBy = null;
        b.PaymentReference = payRef; b.UpdatedAt = now;
        db.BatchWorkflowEvents.Add(BatchSettlementService.Event(b, c.PayerId, "PAYMENT_CONFIRMED", from, "Sàn đã nhận tiền của nhà máy qua PayOS."));
        db.Notifications.Add(Notification.ForDepot(c.PayeeId, b.DepotId, "FACTORY", "Nhà máy đã thanh toán lô qua sàn",
            $"Lô {b.Code}: sàn đã nhận {c.Amount:N0}đ từ nhà máy. Tiền sẽ được chi cho kho theo kỳ chi sau thời gian giữ.", b.Id));
        db.Notifications.Add(new Notification { UserId = c.PayerId, BatchId = b.Id, Category = "PAYMENT", TargetType = "MONEY_COLLECTION", TargetId = c.Id,
            Title = "Đã thanh toán lô qua sàn", Message = $"Lô {b.Code}: sàn đã nhận {c.Amount:N0}đ. Lô chuyển sang đã thanh toán." });
        return true;
    }

    // LIQUIDATION_DEPOSIT: đặt cả báo chuyển và xác nhận nhận cọc (thỏa ck_liquidation_deposit_flow: cần cả hai bên đã ký).
    private bool ApplyDeposit(MoneyCollection c, LiquidationAgreement? a, string payRef, DateTime now)
    {
        if (a == null || a.DepositAmount <= 0 || a.SellerSignedAt == null || a.BuyerSignedAt == null || a.DepositConfirmedAt != null) return false;
        a.DepositReference = payRef; a.DepositReportedAt ??= now; a.DepositConfirmedAt = now;
        db.Notifications.Add(Notification.ForLiquidation(c.PayeeId, a.LotId, "Tiền cọc đã vào sàn", $"Chủ kho đã thanh toán tiền cọc lô {a.Lot.Title} qua PayOS. Chủ kho có thể thanh toán phần còn lại."));
        db.Notifications.Add(Notification.ForDepot(c.PayerId, a.DepotId, "LIQUIDATION", "Sàn đã nhận tiền cọc", $"Sàn đã nhận tiền cọc lô {a.Lot.Title}. Bạn có thể thanh toán phần còn lại.", targetType: "LIQUIDATION", targetId: a.LotId));
        return true;
    }

    // LIQUIDATION_BALANCE: đặt payment_reported_at/payment_confirmed_at như hai xác nhận của luồng cũ; bàn giao và thu gom vẫn là xác nhận thủ công.
    private bool ApplyBalance(MoneyCollection c, LiquidationAgreement? a, string payRef, DateTime now)
    {
        if (a == null || a.PaymentConfirmedAt != null || (a.DepositAmount > 0 && a.DepositConfirmedAt == null)) return false;
        a.PaymentReference = payRef; a.PaymentReportedAt ??= now; a.PaymentConfirmedAt = now;
        db.Notifications.Add(Notification.ForLiquidation(c.PayeeId, a.LotId, "Tiền thanh lý đã vào sàn", $"Chủ kho đã thanh toán đủ lô {a.Lot.Title} qua PayOS. Hai bên có thể tiến hành bàn giao."));
        db.Notifications.Add(Notification.ForDepot(c.PayerId, a.DepotId, "LIQUIDATION", "Sàn đã nhận đủ tiền", $"Sàn đã nhận phần thanh toán lô {a.Lot.Title}. Hai bên có thể tiến hành giao nhận.", targetType: "LIQUIDATION", targetId: a.LotId));
        return true;
    }

    /// <summary>
    /// Dòng SALE của lô giao nhà máy, tạo ngay khi sàn nhận tiền (lúc đó quyết toán hoàn tất).
    /// gross = giá trị hóa đơn gồm VAT (VAT chi hộ cho kho, kho tự nộp thuế); commission = tỷ lệ × tiền hàng chưa VAT (batch.GrossAmount).
    /// </summary>
    private async Task CreateBatchSaleLineAsync(MoneyCollection c, InventoryBatch b, DateTime now, CancellationToken ct)
    {
        var goods = b.GrossAmount ?? c.Amount;
        await ledger.CreateSaleLineAsync(new(c.Id, c.PayeeId, MoneyFlows.Batch, MoneySources.InventoryBatch, b.Id, c.Amount, now, Math.Min(goods, c.Amount)), ct);
    }

    // ---------- bất thường, hoàn, hủy ----------

    private async Task RecordAnomalyAsync(MoneyCollection c, string kind, long orderCode, decimal received, decimal refund, string reference, string note, Guid? actor, CancellationToken ct)
    {
        var now = Now;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO money_collection_anomalies (id, collection_id, order_code, kind, received_amount, expected_amount, refund_amount, reference, status, note, created_at)
            VALUES ({Guid.NewGuid()}, {c.Id}, {orderCode}, {kind}, {received}, {c.Amount}, {refund}, {reference}, 'OPEN', {note}, {now})
            ON CONFLICT (collection_id, kind, reference) DO NOTHING", ct);
        if (inserted == 0) return; // đã ghi nhận lần trước (webhook gửi lại)
        logger.LogWarning("Thu tiền bất thường {Kind} cho khoản thu {CollectionId}: nhận {Received}, cần {Expected}.", kind, c.Id, received, c.Amount);
        Audit(actor, "COLLECTION_" + kind, c, new { c.Status, c.Amount }, new { kind, orderCode, received, refund, c.Status });
        await NotifyAdminsAsync("Tiền thanh toán PayOS bất thường", $"{kind}: khoản thu {c.Id} ({FlowLabel(c.Flow)}) nhận {received:N0}đ, cần {c.Amount:N0}đ, cần hoàn {refund:N0}đ. Vui lòng xử lý hoàn tiền thủ công.", c.Id, ct);
        db.Notifications.Add(new Notification { UserId = c.PayerId, Category = "PAYMENT", TargetType = "MONEY_COLLECTION", TargetId = c.Id,
            Title = "Thanh toán PayOS cần đối soát", Message = $"Sàn ghi nhận {received:N0}đ khác số cần thanh toán {c.Amount:N0}đ. Admin sẽ hoàn lại phần chênh lệch cho bạn." });
    }

    private async Task NotifyAdminsAsync(string title, string message, Guid collectionId, CancellationToken ct)
    {
        var admins = await db.Users.AsNoTracking().Where(u => u.Role == "ADMIN" && u.IsActive).Select(u => u.Id).ToListAsync(ct);
        foreach (var admin in admins)
            db.Notifications.Add(new Notification { UserId = admin, Category = "PAYMENT", TargetType = "MONEY_COLLECTION", TargetId = collectionId, Title = title, Message = message });
    }

    public async Task<bool> MarkRefundingAsync(string flow, Guid sourceId, string reason, Guid? actorId, CancellationToken ct = default)
    {
        RequireFlow(flow);
        var own = db.Database.CurrentTransaction == null;
        await using var tx = own ? await db.Database.BeginTransactionAsync(ct) : null;
        var id = await db.MoneyCollections.AsNoTracking().Where(x => x.Flow == flow && x.SourceId == sourceId && x.Status == CollectionStates.Paid).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (id == null) { if (tx != null) await tx.CommitAsync(ct); return false; }
        var c = await LockCollectionAsync(id.Value, ct);
        if (c.Status != CollectionStates.Paid) { if (tx != null) await tx.CommitAsync(ct); return false; }
        c.Status = CollectionStates.Refunding; c.UpdatedAt = Now;
        Audit(actorId, "COLLECTION_REFUNDING", c, new { Status = CollectionStates.Paid }, new { c.Status, reason });
        await NotifyAdminsAsync("Cần hoàn tiền cho người trả", $"Khoản thu {c.Id} ({FlowLabel(c.Flow)}) chuyển sang hoàn tiền: {reason}. Số tiền {c.ReceivedAmount ?? c.Amount:N0}đ. Vui lòng hoàn thủ công.", c.Id, ct);
        db.Notifications.Add(new Notification { UserId = c.PayerId, Category = "PAYMENT", TargetType = "MONEY_COLLECTION", TargetId = c.Id,
            Title = "Khoản thanh toán sẽ được hoàn", Message = $"Khoản thanh toán {FlowLabel(c.Flow)} sẽ được Admin hoàn lại. Lý do: {reason}." });
        await db.SaveChangesAsync(ct);
        // Đã có dòng SALE thì tạo dòng REFUND toàn phần (trừ vào kỳ chi kế tiếp, hoa hồng hoàn theo tỷ lệ).
        var sale = await db.PayableLines.AsNoTracking().SingleOrDefaultAsync(l => l.SourceType == c.SourceType && l.SourceId == c.SourceId && l.Kind == PayableKinds.Sale, ct);
        if (sale != null && sale.State != PayableStates.Reversed && sale.Gross > 0)
            await ledger.CreateRefundLineAsync(new(c.SourceType, c.SourceId, $"FULL-{c.Id:N}", sale.Gross, Now), ct);
        if (tx != null) await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> CancelUnpaidAsync(string flow, Guid sourceId, string reason, Guid? actorId, CancellationToken ct = default)
    {
        RequireFlow(flow);
        var own = db.Database.CurrentTransaction == null;
        await using var tx = own ? await db.Database.BeginTransactionAsync(ct) : null;
        var id = await db.MoneyCollections.AsNoTracking().Where(x => x.Flow == flow && x.SourceId == sourceId && x.Status == CollectionStates.Pending).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (id == null) { if (tx != null) await tx.CommitAsync(ct); return false; }
        var c = await LockCollectionAsync(id.Value, ct);
        if (c.Status != CollectionStates.Pending) { if (tx != null) await tx.CommitAsync(ct); return false; }
        c.Status = CollectionStates.Cancelled; c.UpdatedAt = Now;
        Audit(actorId, "COLLECTION_CANCELLED", c, new { Status = CollectionStates.Pending }, new { c.Status, reason });
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return true;
    }

    // ---------- tiện ích ----------

    private void Audit(Guid? userId, string action, MoneyCollection c, object? oldData, object? newData) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityName = nameof(MoneyCollection), EntityId = c.Id,
            OldData = JsonSerializer.Serialize(oldData), NewData = JsonSerializer.Serialize(newData), CreatedAt = Now });

    private static string Trim100(string s) => s.Length <= 100 ? s : s[..100];
}
