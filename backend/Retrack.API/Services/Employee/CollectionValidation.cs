using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Employee;

public static class CollectionValidation
{
    public const int RadiusMeters = 200;
    public const int MaxAccuracyMeters = 50;
    public const int MaxLocationAgeSeconds = 120;

    public static double ValidateCheckIn(PickupRequest pickup, EmployeeCheckInRequest input, DateTimeOffset now)
    {
        if (input.IsMocked) throw new ArgumentException("Không chấp nhận vị trí GPS giả lập để check-in.");
        if (!Coordinate(input.Latitude, 90) || !Coordinate(input.Longitude, 180)
            || !Coordinate((double?)pickup.Latitude, 90) || !Coordinate((double?)pickup.Longitude, 180))
            throw new ArgumentException("Địa điểm đơn hoặc vị trí hiện tại chưa có tọa độ hợp lệ.");
        if (input.AccuracyMeters is not double accuracy || !double.IsFinite(accuracy) || accuracy < 0 || accuracy > MaxAccuracyMeters)
            throw new ArgumentException($"GPS chưa đủ chính xác. Cần sai số không quá {MaxAccuracyMeters} m; hãy ra nơi thoáng và lấy lại vị trí.");
        if (input.LocationRecordedAt == null || now - input.LocationRecordedAt > TimeSpan.FromSeconds(MaxLocationAgeSeconds)
            || input.LocationRecordedAt - now > TimeSpan.FromSeconds(30))
            throw new ArgumentException("Vị trí đã cũ hoặc thời gian thiết bị không đúng. Hãy cập nhật GPS.");
        if (input.PhotoTakenAt == null || now - input.PhotoTakenAt > TimeSpan.FromMinutes(10)
            || input.PhotoTakenAt - now > TimeSpan.FromSeconds(30))
            throw new ArgumentException("Ảnh phải được chụp trong vòng 10 phút. Hãy chụp lại tại địa điểm.");
        var distance = Distance(input.Latitude!.Value, input.Longitude!.Value, (double)pickup.Latitude!.Value, (double)pickup.Longitude!.Value);
        if (distance > RadiusMeters)
            throw new ArgumentException($"Bạn đang cách địa điểm khoảng {Math.Round(distance)} m. Cần ở trong bán kính {RadiusMeters} m để check-in.");
        return distance;
    }

    private static bool Coordinate(double? value, int limit) => value.HasValue && double.IsFinite(value.Value) && Math.Abs(value.Value) <= limit;
    public static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * radians / 2), 2)
            + Math.Cos(lat1 * radians) * Math.Cos(lat2 * radians) * Math.Pow(Math.Sin((lng2 - lng1) * radians / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    public static List<ClassificationItemInput> Items(List<ClassificationItemInput>? items)
    {
        if (items == null || items.Count > Enum.GetValues<MaterialType>().Length)
            throw new ArgumentException("Danh sách phế liệu không hợp lệ hoặc vượt quá số loại cho phép.");
        var result = new List<ClassificationItemInput>();
        var seen = new HashSet<string>();
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.MaterialType)) throw new ArgumentException("Hãy chọn loại phế liệu.");
            var code = MaterialCatalog.RequireCode(item.MaterialType);
            if (!seen.Add(code)) throw new ArgumentException("Mỗi loại phế liệu chỉ được có một dòng; hãy cộng gộp khối lượng cùng loại.");
            if (item.WeightKg is < 0.01m or > 10000m || decimal.Round(item.WeightKg, 2) != item.WeightKg)
                throw new ArgumentException("Khối lượng phải từ 0,01 đến 10.000 kg và tối đa 2 chữ số thập phân.");
            if (item.PricePerKg is <= 0 or > 10000000m || decimal.Truncate(item.PricePerKg) != item.PricePerKg)
                throw new ArgumentException("Đơn giá phải là số nguyên từ 1 đến 10.000.000 đồng/kg.");
            result.Add(item with { MaterialType = code });
        }
        if (result.Sum(i => i.WeightKg) > 10000m) throw new ArgumentException("Tổng khối lượng một đơn không được vượt 10.000 kg.");
        return result.OrderBy(i => i.MaterialType, StringComparer.Ordinal).ToList();
    }
    public static decimal SubTotal(decimal weight, decimal price) => decimal.Round(weight * price, 0, MidpointRounding.AwayFromZero);
}
