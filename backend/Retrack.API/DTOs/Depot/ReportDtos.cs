namespace Retrack.API.DTOs.Depot;

public sealed class PeriodQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string GroupBy { get; set; } = "day";
    public (DateTime Start, DateTime End) Bounds()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var from = From ?? new DateOnly(today.Year, today.Month, 1);
        var to = To ?? today;
        if (to < from || from.Year < 1900 || to.Year > 9998 || to.DayNumber - from.DayNumber > 366)
            throw new ArgumentException("Khoảng thời gian phải hợp lệ và không quá 367 ngày.");
        if (GroupBy is not ("day" or "week" or "month")) throw new ArgumentException("Nhóm theo ngày, tuần hoặc tháng.");
        return (DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc),
            DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc));
    }
}
public record RevenuePointDto(DateOnly Date, decimal Revenue, decimal PurchaseCost);
public record RevenueReportDto(decimal Revenue, decimal PurchaseCost, List<RevenuePointDto> Points);
public record DashboardDto(int NewRequestsToday, decimal AvailableKg, int ActiveBatches, int PendingPayments, decimal Revenue, decimal PurchaseCost, int RejectedQualityBatches = 0);
public record StaffPerformanceDto(Guid Id, string FullName, string Role, int CompletedCount, decimal WeightKg, decimal PurchaseAmount);
public record StaffHistoryDto(Guid Id, string Type, string Status, decimal WeightKg, decimal Amount, DateTime Date);
public record FeeEntryDto(Guid Id, Guid SourceId, decimal FeeAmount, DateTime Date);
public record FeeSummaryDto(decimal AccruedAmount, decimal UnpaidInvoiceAmount, decimal SubmittedInvoiceAmount);
public record FeeInvoiceDto(Guid Id, DateOnly PeriodStart, decimal Amount, string Status, string? PaymentProofUrl, DateTime? SubmittedAt);
