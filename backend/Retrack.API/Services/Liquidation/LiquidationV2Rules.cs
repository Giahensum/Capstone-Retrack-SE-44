using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Liquidation;

/// <summary>Luật phiên thanh lý nhiều hạng mục (V2). Một cấu hình dùng chung cho backend, thông báo và giao diện.</summary>
public static class LiquidationV2Rules
{
    public const int MaxItems = 20;
    public const int ExtensionMinutes = 2;
    public const int MaxExtensions = 5;
    public const int MaxExtensionMinutes = ExtensionMinutes * MaxExtensions; // 10 phút, khớp CHECK của liquidation_lots
    public const int MinCollectionWindowHours = 24, MaxCollectionWindowHours = 1440;
    public const decimal MaxLineAmount = 1_000_000_000_000;
    public const decimal MaxKg = 1_000_000, MaxPieces = 100_000;

    public static readonly string[] Units = ["kg", "cai", "bo"];

    /// <summary>Hạng mục: kg là vật liệu (tối đa 3 số lẻ); cái/bộ là đồ cũ (số nguyên). Mô tả bắt buộc cho loại/tình trạng hoặc thành phần bộ.</summary>
    public static string KindOf(string unit) => unit == "kg" ? "MATERIAL" : "USED_GOODS";

    public static void CheckItem(string name, string description, string unit, decimal quantity, decimal startingUnitPrice, decimal bidIncrement)
    {
        if (name.Trim().Length is < 2 or > 255) throw new ArgumentException("Tên hạng mục cần từ 2 đến 255 ký tự.");
        if (!Units.Contains(unit)) throw new ArgumentException("Đơn vị hạng mục chỉ nhận kg, cái hoặc bộ.");
        var minDescription = unit == "kg" ? 5 : 10;
        if (description.Trim().Length < minDescription || description.Trim().Length > 1000)
            throw new ArgumentException(unit switch
            {
                "bo" => "Đơn vị bộ cần mô tả thành phần của mỗi bộ (10–1000 ký tự).",
                "cai" => "Đơn vị cái cần ghi rõ loại và tình trạng (10–1000 ký tự).",
                _ => "Cần mô tả hiện trạng vật liệu (5–1000 ký tự)."
            });
        if (unit == "kg")
        {
            if (quantity <= 0 || quantity > MaxKg || decimal.Round(quantity, 3) != quantity)
                throw new ArgumentException("Khối lượng kg phải lớn hơn 0, tối đa 1.000.000 kg và tối đa 3 chữ số thập phân.");
        }
        else if (quantity < 1 || quantity > MaxPieces || decimal.Truncate(quantity) != quantity)
            throw new ArgumentException("Số lượng cái/bộ phải là số nguyên từ 1 đến 100.000.");
        LiquidationRules.CheckMoney(startingUnitPrice);
        LiquidationRules.CheckMoney(bidIncrement);
        if (Amount(quantity, startingUnitPrice) > MaxLineAmount) throw new ArgumentException("Giá trị khởi điểm của hạng mục vượt giới hạn 1.000 tỷ.");
    }

    /// <summary>Tiền một dòng = đơn giá × số lượng đã đăng, làm tròn một lần đến đồng.</summary>
    public static decimal Amount(decimal quantity, decimal unitPrice) => Math.Round(quantity * unitPrice, 0, MidpointRounding.AwayFromZero);

    public static decimal NextMin(LiquidationItem item) => item.HighestUnitPrice.HasValue ? item.HighestUnitPrice.Value + item.BidIncrement : item.StartingUnitPrice;

    /// <summary>Giá hợp lệ: ≥ mức tối thiểu kế tiếp và nằm đúng lưới khởi điểm + n × bước giá.</summary>
    public static void CheckBid(LiquidationItem item, decimal unitPrice)
    {
        LiquidationRules.CheckMoney(unitPrice);
        var min = NextMin(item);
        if (unitPrice < min)
            throw new DepotConflictException($"Giá đã thay đổi hoặc chưa đạt mức tối thiểu {min:N0} đ/{UnitLabel(item.Unit)}. Hãy xem giá mới rồi trả lại.");
        if ((unitPrice - item.StartingUnitPrice) % item.BidIncrement != 0)
            throw new ArgumentException($"Đơn giá phải bằng giá khởi điểm cộng bội số bước giá {item.BidIncrement:N0} đ.");
        if (Amount(item.Quantity, unitPrice) > MaxLineAmount) throw new ArgumentException("Thành tiền hạng mục vượt giới hạn 1.000 tỷ.");
    }

    /// <summary>Có giá hợp lệ trong 2 phút cuối thì cộng 2 phút vào giờ đóng chung của phiên, tối đa 5 lần.</summary>
    public static (DateTime End, short Count, bool Extended) Extend(DateTime now, DateTime end, short count) =>
        end - now < TimeSpan.FromMinutes(ExtensionMinutes) && count < MaxExtensions
            ? (end.AddMinutes(ExtensionMinutes), (short)(count + 1), true)
            : (end, count, false);

    public static string UnitLabel(string unit) => unit switch { "cai" => "cái", "bo" => "bộ", _ => "kg" };
}

