using System.Globalization;
using System.Text;

namespace Retrack.API.Services.Money;

public sealed record VietnameseBank(string Bin, string Name);

/// <summary>
/// Danh sách ngân hàng phổ biến kèm mã BIN Napas 6 số để giao diện cho chọn. Chỉ để gợi ý: BIN lạ (6 chữ số) vẫn được nhận,
/// vì danh sách có thể thiếu hoặc lỗi thời. Sao chép dữ liệu (không sao chép mã) từ danh mục BIN do NAPAS/VietQR công bố.
/// </summary>
public static class VietnameseBanks
{
    public static readonly IReadOnlyList<VietnameseBank> All =
    [
        new("970436", "Vietcombank"), new("970415", "VietinBank"), new("970418", "BIDV"), new("970405", "Agribank"),
        new("970407", "Techcombank"), new("970422", "MB Bank"), new("970416", "ACB"), new("970432", "VPBank"),
        new("970423", "TPBank"), new("970403", "Sacombank"), new("970437", "HDBank"), new("970441", "VIB"),
        new("970443", "SHB"), new("970448", "OCB"), new("970426", "MSB"), new("970431", "Eximbank"),
        new("970429", "SCB"), new("970440", "SeABank"), new("970454", "BVBank (Bản Việt)"), new("970449", "LPBank"),
        new("970425", "ABBANK"), new("970428", "Nam A Bank"), new("970412", "PVcomBank"), new("970419", "NCB"),
        new("970438", "BaoViet Bank"), new("970433", "VietBank"), new("970452", "KienLongBank"), new("970409", "BacABank"),
        new("970400", "SaigonBank"), new("970406", "DongA Bank"), new("970446", "Co-opBank"), new("971025", "MoMo"),
    ];

    public static bool IsKnown(string bin) => All.Any(b => b.Bin == bin);
}

/// <summary>Quy tắc ngân hàng dùng chung của engine chi và API người nhận.</summary>
public static class PayoutBankRules
{
    public static bool IsValidBin(string? bin) => bin is { Length: 6 } && bin.All(char.IsAsciiDigit);
    public static bool IsValidAccountNumber(string? account) => account is { Length: >= 6 and <= 20 } && account.All(char.IsAsciiDigit);

    /// <summary>****1234. Không bao giờ trả số tài khoản đầy đủ cho người nhận hoặc danh sách.</summary>
    public static string? Mask(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return null;
        var a = account.Trim();
        return a.Length <= 4 ? "****" : "****" + a[^4..];
    }

    /// <summary>Bỏ dấu tiếng Việt, đổi đ thành d, chữ hoa, gộp khoảng trắng: so khớp tên chủ tài khoản với họ tên.</summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var d = name.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool NamesMatch(string? holder, string? fullName)
    {
        var h = NormalizeName(holder);
        return h.Length > 0 && h == NormalizeName(fullName);
    }
}
