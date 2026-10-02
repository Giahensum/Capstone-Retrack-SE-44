using Retrack.API.Models.Enums;

namespace Retrack.API.Services.Shared;

public static class MaterialCatalog
{
    // Chỉ ánh xạ tên xác định rõ; không đoán loại của vật liệu cũ chưa nhận diện.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nhựa PET"] = "PET", ["Nhựa HDPE"] = "HDPE", ["Nhựa PVC"] = "PVC",
        ["Giấy"] = "PAPER", ["Giấy carton"] = "CARDBOARD", ["Bìa carton"] = "CARDBOARD",
        ["Nhôm"] = "ALUMINUM", ["Sắt"] = "IRON", ["Thép"] = "STEEL", ["Đồng"] = "COPPER",
        ["Rác thải điện tử"] = "ELECTRONIC_WASTE", ["Khác"] = "OTHER"
    };
    public static string Normalize(string value)
    {
        var text = value.Trim();
        if (Aliases.TryGetValue(text, out var code)) return code;
        return Enum.TryParse<MaterialType>(text, true, out var type) && Enum.IsDefined(type) ? type.ToString() : text;
    }
    public static string RequireCode(string value)
    {
        var code = Normalize(value);
        if (!Enum.TryParse<MaterialType>(code, out var type) || !Enum.IsDefined(type))
            throw new ArgumentException("Vật liệu chưa có mã chuẩn; cần đối chiếu danh mục trước khi tạo lô.");
        return code;
    }
    public static string[] Values(string code) => Aliases.Where(x => x.Value == code).Select(x => x.Key).Append(code).ToArray();
}
