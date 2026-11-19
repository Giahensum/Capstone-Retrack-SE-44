using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Money;

public sealed record SaleLineRequest(Guid CollectionId, Guid PayeeId, string Flow, string SourceType, Guid SourceId, decimal Gross, DateTime TriggeredAtUtc);

/// <param name="RefundKey">Khóa của đúng lần hoàn này (ví dụ mã yêu cầu hoàn). Cùng khóa gọi lại là no-op.</param>
public sealed record RefundLineRequest(string SourceType, Guid SourceId, string RefundKey, decimal RefundGross, DateTime TriggeredAtUtc);

/// <param name="Created">true nếu lần gọi này tạo dòng mới; false nếu dòng đã tồn tại (idempotent).</param>
public sealed record LedgerWriteResult(PayableLine Line, bool Created);

/// <summary>
/// Sổ cái phải chi (payable_lines). Mọi hàm ghi PHẢI chạy trong transaction do nơi gọi mở (cùng transaction với việc
/// đổi trạng thái khoản thu/nguồn) để dòng sổ cái và nguồn không bao giờ lệch nhau. Không gọi nhà cung cấp chi ở đây.
/// </summary>
public sealed class PayableLedgerService(AppDbContext db, MoneyConfigReader config, TimeProvider clock)
{
    /// <summary>
    /// Tạo dòng SALE cho một nguồn. Idempotent nhờ ràng buộc duy nhất (source_type, source_id, kind, refund_key):
    /// INSERT ... ON CONFLICT DO NOTHING, nên gọi trùng hoặc đồng thời chỉ cho đúng một dòng; bên thua đợi bên thắng commit rồi đọc lại.
    /// Tỷ lệ hoa hồng được chụp lại tại đây; available_at = thời điểm kích hoạt + payout.hold_minutes.
    /// </summary>
    public async Task<LedgerWriteResult> CreateSaleLineAsync(SaleLineRequest r, CancellationToken ct = default)
    {
        RequireTransaction();
        if (r.Gross < 0 || r.Gross != decimal.Truncate(r.Gross)) throw new ArgumentException("Số tiền phải là số nguyên đồng không âm.", nameof(r));
        if (!MoneyFlows.CommissionFlows.Contains(r.Flow)) throw new ArgumentException("Dòng tiền không hợp lệ.", nameof(r));
        var collection = await db.MoneyCollections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == r.CollectionId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy khoản thu.");
        if (collection.PayeeId != r.PayeeId || collection.SourceId != r.SourceId || collection.SourceType != r.SourceType)
            throw new InvalidOperationException("Khoản thu không khớp người nhận hoặc nguồn của dòng sổ cái.");
        if (collection.Status != "PAID") throw new InvalidOperationException("Chỉ tạo dòng sổ cái cho khoản thu đã thanh toán.");

        var cfg = await config.GetAsync(ct);
        var rate = await ResolveRateAsync(r.PayeeId, r.Flow, cfg, ct);
        var commission = CommissionCalculator.Commission(r.Gross, rate);
        var trigger = Utc(r.TriggeredAtUtc);
        var availableAt = trigger.AddMinutes(cfg.PayoutHoldMinutes);
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow().UtcDateTime;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO payable_lines (id, collection_id, payee_id, flow, source_type, source_id, kind, refund_key, gross, commission_rate, commission, state, available_at, created_at)
            VALUES ({id}, {r.CollectionId}, {r.PayeeId}, {r.Flow}, {r.SourceType}, {r.SourceId}, 'SALE', NULL, {r.Gross}, {rate}, {commission}, 'HELD', {availableAt}, {now})
            ON CONFLICT (source_type, source_id, kind, (COALESCE(refund_key, ''))) DO NOTHING", ct);
        var line = await ReadLineAsync(r.SourceType, r.SourceId, PayableKinds.Sale, null, ct);
        if (inserted == 0 && (line.PayeeId != r.PayeeId || line.Gross != r.Gross))
            throw new InvalidOperationException("Nguồn này đã có dòng bán với người nhận hoặc số tiền khác.");
        return new(line, inserted == 1);
    }

    /// <summary>
    /// Tạo dòng REFUND (độ lớn dương) cho một lần hoàn một phần hoặc toàn phần. Khóa dòng SALE (FOR UPDATE) để tuần tự hóa
    /// các lần hoàn cùng nguồn, tính hoa hồng hoàn theo tỷ lệ tích lũy, dòng có hiệu lực ngay (AVAILABLE) để trừ vào kỳ chi kế tiếp.
    /// </summary>
    public async Task<LedgerWriteResult> CreateRefundLineAsync(RefundLineRequest r, CancellationToken ct = default)
    {
        RequireTransaction();
        if (string.IsNullOrWhiteSpace(r.RefundKey) || r.RefundKey.Length > 100) throw new ArgumentException("Khóa hoàn tiền không hợp lệ.", nameof(r));
        if (r.RefundGross <= 0 || r.RefundGross != decimal.Truncate(r.RefundGross)) throw new ArgumentException("Số tiền hoàn phải là số nguyên đồng lớn hơn 0.", nameof(r));

        var sale = await db.PayableLines.FromSqlInterpolated($@"SELECT * FROM payable_lines WHERE source_type = {r.SourceType} AND source_id = {r.SourceId} AND kind = 'SALE' FOR UPDATE")
            .AsTracking().SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không có dòng bán để hoàn tiền.");
        if (sale.State == PayableStates.Reversed) throw new InvalidOperationException("Dòng bán đã bị đảo, không thể hoàn thêm.");

        var existing = await db.PayableLines.AsNoTracking().SingleOrDefaultAsync(l => l.SourceType == r.SourceType && l.SourceId == r.SourceId
            && l.Kind == PayableKinds.Refund && l.RefundKey == r.RefundKey, ct);
        if (existing != null)
        {
            if (existing.Gross != r.RefundGross) throw new InvalidOperationException("Khóa hoàn tiền đã dùng cho số tiền khác.");
            return new(existing, false);
        }

        var prior = await db.PayableLines.AsNoTracking().Where(l => l.SourceType == r.SourceType && l.SourceId == r.SourceId
            && l.Kind == PayableKinds.Refund && l.State != PayableStates.Reversed)
            .GroupBy(l => 1).Select(g => new { Gross = g.Sum(l => l.Gross), Commission = g.Sum(l => l.Commission) }).SingleOrDefaultAsync(ct);
        var refundCommission = CommissionCalculator.RefundCommission(sale.Gross, sale.Commission, prior?.Gross ?? 0m, prior?.Commission ?? 0m, r.RefundGross);
        var trigger = Utc(r.TriggeredAtUtc);
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow().UtcDateTime;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO payable_lines (id, collection_id, payee_id, flow, source_type, source_id, kind, refund_key, gross, commission_rate, commission, state, available_at, created_at)
            VALUES ({id}, {sale.CollectionId}, {sale.PayeeId}, {sale.Flow}, {sale.SourceType}, {sale.SourceId}, 'REFUND', {r.RefundKey}, {r.RefundGross}, {sale.CommissionRate}, {refundCommission}, 'AVAILABLE', {trigger}, {now})
            ON CONFLICT (source_type, source_id, kind, (COALESCE(refund_key, ''))) DO NOTHING", ct);
        var line = await ReadLineAsync(r.SourceType, r.SourceId, PayableKinds.Refund, r.RefundKey, ct);
        return new(line, inserted == 1);
    }

    /// <summary>Chuyển các dòng SALE đã hết thời gian giữ từ HELD sang AVAILABLE. Chạy lại an toàn; trả số dòng được chuyển.</summary>
    public Task<int> MarkAvailableAsync(DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var now = Utc(nowUtc ?? clock.GetUtcNow().UtcDateTime);
        return db.PayableLines.Where(l => l.State == PayableStates.Held && l.Kind == PayableKinds.Sale && l.AvailableAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.State, PayableStates.Available), ct);
    }

    /// <summary>Riêng của người nhận (nếu có) thắng mặc định chung của dòng tiền.</summary>
    public async Task<decimal> ResolveRateAsync(Guid payeeId, string flow, MoneyConfig? cfg = null, CancellationToken ct = default)
    {
        cfg ??= await config.GetAsync(ct);
        var own = await db.PayeeCommissions.AsNoTracking().Where(c => c.UserId == payeeId && c.Flow == flow).Select(c => (decimal?)c.Rate).SingleOrDefaultAsync(ct);
        return CommissionCalculator.ResolveRate(own, cfg.DefaultRate(flow));
    }

    private Task<PayableLine> ReadLineAsync(string sourceType, Guid sourceId, string kind, string? refundKey, CancellationToken ct) =>
        db.PayableLines.AsNoTracking().SingleAsync(l => l.SourceType == sourceType && l.SourceId == sourceId && l.Kind == kind && l.RefundKey == refundKey, ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Ghi sổ cái phải chạy trong transaction do nơi gọi mở.");
    }

    private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);
}
