namespace Retrack.API.DTOs.Factory;

public sealed record DashboardResponse
{
    public required int OrderCount { get; init; }
    public required int ActiveDemandCount { get; init; }
    public required int PartnerCount { get; init; }
    public required int PendingQcCount { get; init; }
    public required decimal MonthlyPurchasedKg { get; init; }
    public required decimal MonthlyNetPayment { get; init; }
    public required IReadOnlyList<OrderResponse> RecentOrders { get; init; }
}
