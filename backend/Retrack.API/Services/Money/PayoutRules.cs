using System.Globalization;
using Retrack.API.Models;

namespace Retrack.API.Services.Money;

/// <summary>Khóa cấu hình riêng của engine chi (ngoài MoneyConfigKeys để không đụng file dùng chung).</summary>
public static class PayoutConfigKeys
{
    public const string BankChangeHoldHours = "payout.bank_change_hold_hours";
    /// <summary>Mốc lịch tự chi gần nhất đã xử lý (ISO UTC). Bền vững trong system_configs, không dùng cờ trong bộ nhớ.</summary>
    public const string AutoLastBoundary = "payout.auto_last_boundary";
    public const int DefaultBankChangeHoldHours = 24;
}

public sealed record BankCheck(bool Ready, string? Reason);

public sealed record PayoutTotals(decimal Gross, decimal Commission, decimal RefundAmount, decimal CarriedDebt, decimal Net);

/// <summary>Quy tắc thuần của engine chi (không truy cập DB), dễ kiểm thử.</summary>
public static class PayoutRules
{
    /// <summary>Giờ Việt Nam là UTC+7 cố định (không có giờ mùa hè).</summary>
    public static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);
    public const int MaxPayeesPerRun = 200;
    /// <summary>Provider không biết mã tham chiếu quá khoảng này kể từ lần gửi thì coi là chưa nhận lệnh.</summary>
    public static readonly TimeSpan NotFoundGrace = TimeSpan.FromMinutes(10);

    public static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);

    /// <summary>Cắt về giây để so khớp kỳ chi ổn định khi lưu/đọc PostgreSQL.</summary>
    public static DateTime TruncateSeconds(DateTime t)
    {
        var u = Utc(t);
        return new DateTime(u.Ticks - u.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }

    public static DateTime ToVn(DateTime utc) => Utc(utc) + VnOffset;

    /// <summary>0 giờ ngày hiện tại theo giờ Việt Nam, trả về UTC.</summary>
    public static DateTime VnDayStartUtc(DateTime utc) => DateTime.SpecifyKind(ToVn(utc).Date - VnOffset, DateTimeKind.Utc);

    public static string? ValidatePeriod(DateTime start, DateTime end, DateTime nowUtc)
    {
        if (start >= end) return "Ngày bắt đầu phải trước ngày kết thúc.";
        if (end > Utc(nowUtc)) return "Ngày kết thúc không được ở tương lai.";
        return null;
    }

    /// <summary>
    /// Mốc kết thúc kỳ của lịch tự chi gần nhất đã đến hạn (0 giờ giờ Việt Nam, trả về UTC).
    /// DAILY: chạy lúc 02:00 hằng ngày, mốc là 0 giờ hôm nay (hoặc hôm qua nếu chưa tới 02:00).
    /// WEEKLY: chạy 02:00 thứ Hai, mốc là 0 giờ thứ Hai gần nhất đã qua 02:00.
    /// </summary>
    public static DateTime ScheduleBoundaryUtc(DateTime nowUtc, string schedule)
    {
        var vn = ToVn(nowUtc);
        var day = vn.Date;
        if (string.Equals(schedule, "WEEKLY", StringComparison.OrdinalIgnoreCase))
        {
            day = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)); // về thứ Hai
            if (vn < day.AddHours(2)) day = day.AddDays(-7);
        }
        else if (vn < day.AddHours(2)) day = day.AddDays(-1);
        return DateTime.SpecifyKind(day - VnOffset, DateTimeKind.Utc);
    }

    /// <summary>Dòng SALE cộng (gross - hoa hồng); dòng REFUND (độ lớn dương) trừ (gross - hoa hồng hoàn lại).</summary>
    public static decimal LineNet(string kind, decimal gross, decimal commission) =>
        kind == PayableKinds.Refund ? -(gross - commission) : gross - commission;

    /// <summary>net = sum(SALE gross) - sum(SALE hoa hồng) - sum(REFUND gross) + sum(REFUND hoa hồng) - nợ mang sang.</summary>
    public static PayoutTotals Totals(IEnumerable<PayableLine> lines, decimal carriedDebt)
    {
        decimal gross = 0, commission = 0, refund = 0;
        foreach (var l in lines)
        {
            if (l.Kind == PayableKinds.Refund) refund += l.Gross - l.Commission;
            else { gross += l.Gross; commission += l.Commission; }
        }
        return new(gross, commission, refund, carriedDebt, gross - commission - refund - carriedDebt);
    }

    /// <summary>Tài khoản đủ điều kiện nhận chi: đủ thông tin, đã xác minh và không mới đổi trong thời gian giữ.</summary>
    public static BankCheck CheckBank(string? bin, string? account, string? holder, string? status, DateTime? changedAtUtc, DateTime nowUtc, int holdHours)
    {
        if (!PayoutBankRules.IsValidBin(bin) || !PayoutBankRules.IsValidAccountNumber(account) || string.IsNullOrWhiteSpace(holder))
            return new(false, "Người nhận chưa có đủ thông tin tài khoản ngân hàng (mã BIN, số tài khoản, tên chủ tài khoản).");
        if (status != "VERIFIED") return new(false, "Tài khoản ngân hàng của người nhận chưa được xác minh.");
        if (changedAtUtc != null && holdHours > 0 && Utc(nowUtc) < Utc(changedAtUtc.Value).AddHours(holdHours))
            return new(false, $"Người nhận vừa đổi tài khoản ngân hàng, giữ kỳ chi {holdHours} giờ để tránh chi nhầm.");
        return new(true, null);
    }

    /// <summary>
    /// Thứ tự quyết định trạng thái kỳ chi (thiết kế 6.2): net &lt; 0 CARRIED; = 0 SETTLED; &lt; min DEFERRED; ngân hàng chưa đủ điều kiện HELD;
    /// net &gt; auto_max_amount (chưa được duyệt) AWAITING_APPROVAL; còn lại PROCESSING.
    /// Chạy thủ công bởi Admin VẪN phải qua ngưỡng duyệt: hạn mức là chốt chặn an toàn tiền thật, Admin duyệt bằng thao tác riêng.
    /// </summary>
    public static (string Status, string? Reason) Decide(decimal net, MoneyConfig cfg, BankCheck bank, bool alreadyApproved)
    {
        if (net < 0) return (PayoutStatuses.Carried, "Số âm: mang nợ sang kỳ sau, không chuyển tiền.");
        if (net == 0) return (PayoutStatuses.Settled, "Cấn trừ hết, không phải chuyển tiền.");
        if (net < cfg.PayoutMinAmount) return (PayoutStatuses.Deferred, $"Dưới mức chi tối thiểu {cfg.PayoutMinAmount.ToString("N0", CultureInfo.InvariantCulture)} đồng: dồn sang kỳ sau.");
        if (!bank.Ready) return (PayoutStatuses.Held, bank.Reason);
        if (net > cfg.PayoutAutoMaxAmount && !alreadyApproved)
            return (PayoutStatuses.AwaitingApproval, $"Vượt hạn mức tự chi {cfg.PayoutAutoMaxAmount.ToString("N0", CultureInfo.InvariantCulture)} đồng: cần Admin duyệt.");
        return (PayoutStatuses.Processing, null);
    }

    /// <summary>PAY-yymmdd-yymmdd-8 ký tự hex đầu của mã người nhận (ngày theo giờ Việt Nam). Hậu tố -B, -C khi trùng; KHÔNG dùng hậu tố số vì code-N là khóa gửi lại.</summary>
    public static string Code(Guid payeeId, DateTime startUtc, DateTime endUtc, int collisionIndex = 0)
    {
        var baseCode = $"PAY-{ToVn(startUtc):yyMMdd}-{ToVn(endUtc):yyMMdd}-{payeeId:N}"[..(4 + 7 + 7 + 8)];
        return collisionIndex <= 0 ? baseCode : baseCode + "-" + (char)('A' + collisionIndex);
    }

    /// <summary>Khóa idempotency và referenceId: code ở lần gửi đầu, code-N từ lần thứ hai (chỉ sau lỗi rõ ràng).</summary>
    public static string KeyFor(string code, int attempt) => attempt <= 1 ? code : $"{code}-{attempt}";

    /// <summary>Mô tả lệnh chi (tối đa 20 ký tự theo payOS).</summary>
    public static string Description(string code) => "RETRACK " + code[^Math.Min(8, code.Length)..];
}
