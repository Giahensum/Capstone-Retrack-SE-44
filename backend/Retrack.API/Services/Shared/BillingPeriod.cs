namespace Retrack.API.Services.Shared;

public static class BillingPeriod
{
    public static (DateTime Start, DateTime End) ClosedMonth(int year, int month, DateTimeOffset now)
    {
        if (year is < 1900 or > 9998 || month is < 1 or > 12)
            throw new ArgumentException("Kỳ hóa đơn không hợp lệ.");
        var localMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = localMonth.AddMonths(1).AddHours(-7);
        if (end > now.UtcDateTime)
            throw new ArgumentException("Chỉ phát hành hóa đơn cho tháng đã kết thúc theo giờ Việt Nam. Phí tháng hiện tại là tạm tính.");
        return (localMonth.AddHours(-7), end);
    }
}
