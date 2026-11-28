namespace Retrack.API.Helpers;

// Ranh giới ngày/tháng Việt Nam trả về UTC để lọc thời gian trong database.
public static class VietnamTime
{
    public static DateTime DayStart(DateTime utc) => DateTime.SpecifyKind(utc.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
    public static DateTime MonthStart(DateTime utc)
    {
        var local = utc.AddHours(7);
        return new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);
    }
}
