using Retrack.API.Models;
using Retrack.API.Services.Depot;
namespace Retrack.API.Services.Employee;

// Ngày và giờ nghiệp vụ Việt Nam; không phụ thuộc múi giờ của máy chủ.
public static class PickupSchedule
{
    public static DateOnly Today(DateTime now) => DateOnly.FromDateTime(now.AddHours(7));
    public static DateTime At(DateOnly day,int hour) => DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour,0)).AddHours(-7),DateTimeKind.Utc);
    public static DateOnly Day(PickupRequest p) => p.CollectionDate ?? Today(p.PreferredDatetime ?? p.CreatedAt);
    public static DateTime Start(PickupRequest p) => p.PreferredDatetime ?? At(Day(p),7);
    public static DateTime End(PickupRequest p) => p.PreferredEndDatetime ?? (p.PreferredDatetime.HasValue ? p.PreferredDatetime.Value.AddHours(2) : At(Day(p),19));
    public static void Validate(DateOnly? day,DateTime? start,DateTime? end,DateTime now)
    {
        day ??= start.HasValue ? Today(start.Value) : null;
        if(start.HasValue != end.HasValue) throw new ArgumentException("Hãy cung cấp đầy đủ giờ bắt đầu và kết thúc khung thu gom.");
        if(day.HasValue && (day<Today(now) || day>Today(now).AddDays(60))) throw new ArgumentException("Ngày thu gom phải từ hôm nay đến 60 ngày tới.");
        if(day.HasValue && !start.HasValue && now>=At(day.Value,19)) throw new ArgumentException("Ngày thu gom đã hết giờ hoạt động. Hãy chọn ngày tiếp theo.");
        if(start.HasValue) {
            var local=start.Value.AddHours(7);var d=Today(start.Value);
            if(day.HasValue && d!=day) throw new ArgumentException("Khung giờ không thuộc ngày thu gom đã chọn.");
            if(local.Minute!=0 || local.Second!=0 || local.Hour is not (7 or 9 or 11 or 13 or 15 or 17) || end!=start.Value.AddHours(2))
                throw new ArgumentException("Chọn khung hai giờ từ 07:00 đến 19:00 giờ Việt Nam.");
            if(end<=now) throw new ArgumentException("Khung thu gom đã kết thúc. Hãy chọn khung còn hiệu lực.");
        }
    }
    public static void RequireCheckIn(PickupRequest p,DateTime now)
    {
        if(!p.RequiresOwnerReview && p.CollectionDate==null && p.PreferredDatetime==null && Day(p)==Today(now)) return; // Đơn linh hoạt cũ chưa có lịch rõ ràng.
        if(p.RequiresOwnerReview || now>=End(p)) throw new DepotConflictException("Đơn đã quá lịch hoặc đang chờ chủ kho xử lý. Vui lòng cập nhật danh sách.");
        if(now<Start(p)) throw new DepotConflictException("Chưa đến khung giờ thu gom đã hẹn với người bán.");
    }
}
