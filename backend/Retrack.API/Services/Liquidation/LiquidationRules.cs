namespace Retrack.API.Services.Liquidation;

public static class LiquidationRules
{
    public static void CheckMoney(decimal amount)
    {
        if (amount <= 0 || amount > 1_000_000_000_000 || decimal.Truncate(amount) != amount)
            throw new ArgumentException("Giá phải là số nguyên VND dương, tối đa 1.000 tỷ.");
    }
    public static void CheckBid(decimal starting, decimal? highest, decimal increment, decimal amount)
    {
        CheckMoney(amount);
        if (amount < (highest.HasValue ? highest.Value + increment : starting))
            throw new ArgumentException("Giá đã thay đổi hoặc chưa đạt mức tối thiểu. Hãy tải lại phiên trước khi trả giá.");
    }
    public static void CheckSchedule(DateTime surveyStart, DateTime surveyEnd, DateTime start, DateTime end, DateTime collectionDeadline)
    {
        if (new[] { surveyStart, surveyEnd, start, end, collectionDeadline }.Any(x => x.Kind != DateTimeKind.Utc) ||
            end > DateTime.MaxValue.AddMinutes(-10) || surveyStart >= surveyEnd || surveyEnd > start || start >= end || end.AddMinutes(10) >= collectionDeadline)
            throw new ArgumentException("Cần lịch UTC hợp lệ: khảo sát kết thúc trước trả giá; hạn thu gom sau cả 10 phút gia hạn tối đa.");
    }
    public static DateTime Extend(DateTime now, DateTime end, DateTime hardEnd) =>
        end - now < TimeSpan.FromMinutes(2) ? (now.AddMinutes(2) < hardEnd ? now.AddMinutes(2) : hardEnd) : end;
    public static string Phase(string status, DateTime now, DateTime surveyStart, DateTime surveyEnd, DateTime start, DateTime end) =>
        status != "PUBLISHED" ? status : now >= end ? "CLOSING" : now >= start ? "BIDDING" :
        now >= surveyStart && now < surveyEnd ? "SURVEY" : "UPCOMING";
}
