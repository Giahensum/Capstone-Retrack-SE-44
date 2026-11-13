namespace Retrack.API.Services.Liquidation;

public static class LiquidationRules
{
    public static void CheckMoney(decimal amount)
    {
        if (amount <= 0 || amount > 1_000_000_000_000 || decimal.Truncate(amount) != amount)
            throw new ArgumentException("Giá phải là số nguyên VND dương, tối đa 1.000 tỷ.");
    }
    public const decimal MaxDepositPercent = 50;
    public static void CheckDepositPercent(decimal percent)
    {
        if (percent < 1 || percent > MaxDepositPercent || decimal.Round(percent, 2) != percent)
            throw new ArgumentException($"Tỷ lệ đặt cọc phải từ 1% đến {MaxDepositPercent}% (tối đa 2 chữ số thập phân).");
    }
    // Cọc làm tròn đến đồng; lô cũ (0%) không có cọc nên luồng cũ không đổi.
    public static decimal Deposit(decimal amount, decimal percent) =>
        percent <= 0 ? 0 : Math.Max(1, Math.Round(amount * percent / 100, 0, MidpointRounding.AwayFromZero));
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
