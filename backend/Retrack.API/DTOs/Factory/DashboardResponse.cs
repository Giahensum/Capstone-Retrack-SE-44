namespace Retrack.API.DTOs.Factory;

public sealed record DashboardResponse
{
    public required int OrderCount { get; init; }
    public required int ActiveDemandCount { get; init; }
    public required int PartnerCount { get; init; }
    public required int PendingQcCount { get; init; }
    public required decimal MonthlyPurchasedKg { get; init; }
    public required decimal MonthlyNetPayment { get; init; }
    public required decimal PeriodPurchasedKg { get; init; }
    public required decimal PeriodGrossAmount { get; init; }
    public required decimal PeriodNetPayment { get; init; }
    public required decimal PeriodFeeAmount { get; init; }
    public required int PendingSettlementCount { get; init; }
    public required IReadOnlyList<MaterialVolumeResponse> MaterialVolumes { get; init; }
    public required IReadOnlyList<MonthlyPaymentResponse> SixMonthPayments { get; init; }
    public required IReadOnlyList<OrderResponse> PriorityOrders { get; init; }
    public required IReadOnlyList<OrderResponse> RecentOrders { get; init; }
}

public sealed record MaterialVolumeResponse(string MaterialType, decimal WeightKg);
public sealed record MonthlyPaymentResponse(int Year, int Month, decimal NetPayment);
