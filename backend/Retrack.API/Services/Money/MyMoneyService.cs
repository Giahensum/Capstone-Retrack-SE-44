using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Money;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Money;

/// <summary>
/// Thu nhập và tài khoản ngân hàng của CHÍNH người nhận (Seller, Chủ kho, Factory). Mọi truy vấn lọc theo userId lấy từ token,
/// không nhận mã người dùng từ request. Tài khoản bị khóa hoặc sai vai trò bị từ chối ngay cả khi token còn hạn.
/// </summary>
public sealed class MyMoneyService(AppDbContext db, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<User> RequirePayeeAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u == null || !u.IsActive || !PayeeRoles.All.Contains(u.Role))
            throw new DepotForbiddenException("Tài khoản không hoạt động hoặc không có quyền xem thu nhập.");
        return u;
    }

    public async Task<MyEarningsDto> GetEarningsAsync(Guid userId, CancellationToken ct = default)
    {
        await RequirePayeeAsync(userId, ct);
        var sums = await db.PayableLines.AsNoTracking().Where(l => l.PayeeId == userId && (l.State == PayableStates.Held || l.State == PayableStates.Available || l.State == PayableStates.InPayout))
            .GroupBy(l => l.State)
            .Select(g => new { State = g.Key, Net = g.Sum(l => l.Kind == PayableKinds.Refund ? -(l.Gross - l.Commission) : l.Gross - l.Commission) }).ToListAsync(ct);
        decimal Of(string state) => sums.Where(s => s.State == state).Sum(s => s.Net);
        var paid = await db.PayoutRuns.AsNoTracking().Where(r => r.PayeeId == userId && r.Status == PayoutStatuses.Paid).SumAsync(r => (decimal?)r.Net, ct) ?? 0m;
        var next = await db.PayableLines.AsNoTracking().Where(l => l.PayeeId == userId && l.State == PayableStates.Held).MinAsync(l => (DateTime?)l.AvailableAt, ct);
        var latest = await db.PayoutRuns.AsNoTracking().Where(r => r.PayeeId == userId).OrderByDescending(r => r.PeriodEnd).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        return new(Of(PayableStates.Held), Of(PayableStates.Available), Of(PayableStates.InPayout), paid, PayoutEngine.CarryOver(latest), next == null ? null : PayoutRules.Utc(next.Value), "VND");
    }

    public async Task<PagedResult<MyPayoutDto>> ListPayoutsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        await RequirePayeeAsync(userId, ct);
        (page, pageSize) = MoneyAdminService.Paging(page, pageSize);
        var q = db.PayoutRuns.AsNoTracking().Where(r => r.PayeeId == userId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        // Không trả fail_reason/provider_ref/phản hồi nhà cung cấp cho người nhận: chỉ ghi chú thân thiện theo trạng thái.
        return new() { Items = rows.Select(r => new MyPayoutDto(r.Id, r.Code, PayoutRules.Utc(r.PeriodStart), PayoutRules.Utc(r.PeriodEnd), r.Gross, r.Commission, r.RefundAmount,
            r.CarriedDebt, r.Net, r.Status, StatusNote(r.Status), PayoutRules.Utc(r.CreatedAt), r.PaidAt == null ? null : PayoutRules.Utc(r.PaidAt.Value))).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public static string StatusNote(string status) => status switch
    {
        PayoutStatuses.Paid => "Đã chuyển vào tài khoản ngân hàng của bạn.",
        PayoutStatuses.Processing or PayoutStatuses.Unknown or PayoutStatuses.Pending => "Đang chuyển tiền.",
        PayoutStatuses.AwaitingApproval => "Đang chờ sàn duyệt.",
        PayoutStatuses.Held => "Đang được giữ: hãy kiểm tra và xác minh tài khoản ngân hàng của bạn.",
        PayoutStatuses.Deferred => "Chưa đạt mức chi tối thiểu, được dồn sang kỳ sau.",
        PayoutStatuses.Carried => "Cấn trừ công nợ, chuyển sang kỳ sau.",
        PayoutStatuses.Settled => "Đã cấn trừ, không phải chuyển tiền.",
        PayoutStatuses.Failed => "Chuyển chưa thành công, sàn đang xử lý lại.",
        _ => "",
    };

    public async Task<PayeeBankDto> GetBankAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await RequirePayeeAsync(userId, ct);
        return MoneyAdminService.BankDto(u, await BankHoldHoursAsync(ct));
    }

    /// <summary>
    /// Lưu tài khoản ngân hàng. Tên chủ tài khoản khớp họ tên (không phân biệt dấu/hoa thường) thì VERIFIED, ngược lại PENDING_VERIFICATION chờ Admin.
    /// Mọi thay đổi ghi bank_changed_at (kỳ chi bị giữ theo payout.bank_change_hold_hours) và xóa trạng thái xác minh cũ; lưu lại y nguyên thì không đổi gì.
    /// </summary>
    public async Task<PayeeBankDto> UpdateBankAsync(Guid userId, UpdateBankRequest req, CancellationToken ct = default)
    {
        var u = await RequirePayeeAsync(userId, ct);
        var bin = (req.BankBin ?? "").Trim();
        var account = (req.AccountNumber ?? "").Trim();
        var holder = string.Join(' ', (req.AccountName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var name = (req.BankName ?? "").Trim();
        if (!PayoutBankRules.IsValidBin(bin)) throw new ArgumentException("Mã BIN ngân hàng phải gồm đúng 6 chữ số.");
        if (!PayoutBankRules.IsValidAccountNumber(account)) throw new ArgumentException("Số tài khoản phải gồm 6 đến 20 chữ số.");
        if (holder.Length is < 2 or > 100) throw new ArgumentException("Tên chủ tài khoản phải từ 2 đến 100 ký tự.");
        if (name.Length == 0) name = VietnameseBanks.All.FirstOrDefault(b => b.Bin == bin)?.Name ?? "";
        if (name.Length is < 2 or > 100) throw new ArgumentException("Cần nhập tên ngân hàng (tối đa 100 ký tự).");

        var hold = await BankHoldHoursAsync(ct);
        var changed = u.BankBin != bin || u.BankAccountNumber != account || u.BankAccountName != holder || u.BankName != name;
        if (!changed) return MoneyAdminService.BankDto(u, hold);
        var now = Now;
        var old = new { bin = u.BankBin, account = PayoutBankRules.Mask(u.BankAccountNumber), status = u.BankStatus };
        var verified = PayoutBankRules.NamesMatch(holder, u.FullName);
        u.BankName = name; u.BankBin = bin; u.BankAccountNumber = account; u.BankAccountName = holder;
        u.BankChangedAt = now;
        u.BankStatus = verified ? "VERIFIED" : "PENDING_VERIFICATION";
        u.BankVerifiedAt = verified ? now : null;
        db.AuditLogs.Add(new() { UserId = userId, Action = "PAYEE_BANK_UPDATED", EntityName = "users", EntityId = userId, OldData = JsonSerializer.Serialize(old, Json),
            NewData = JsonSerializer.Serialize(new { bin, account = PayoutBankRules.Mask(account), status = u.BankStatus }, Json), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        var dto = MoneyAdminService.BankDto(u, hold);
        db.ChangeTracker.Clear();
        return dto;
    }

    private async Task<int> BankHoldHoursAsync(CancellationToken ct)
    {
        var v = await db.SystemConfigs.AsNoTracking().Where(c => c.ConfigKey == PayoutConfigKeys.BankChangeHoldHours).Select(c => c.ConfigValue).FirstOrDefaultAsync(ct);
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h is >= 0 and <= 720 ? h : PayoutConfigKeys.DefaultBankChangeHoldHours;
    }
}
