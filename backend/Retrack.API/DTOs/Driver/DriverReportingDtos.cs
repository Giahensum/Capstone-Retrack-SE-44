namespace Retrack.API.DTOs.Driver;

public sealed class DriverHistoryRow
{
    public Guid Id { get; init; }
    public string? BatchCode { get; init; }
    public string MaterialType { get; init; } = "";
    public decimal WeightKg { get; init; }
    public string DepotName { get; init; } = "";
    public string FactoryName { get; init; } = "";
    public DateTime DeliveredAt { get; init; }
    public DateTime? FactoryReceivedAt { get; init; }
    public bool LegacyDate { get; init; }
}
public sealed record DriverTotals(int Trips, double DeclaredWeightKg, int FactoryReceived);
public sealed record DriverStatsDto(DriverTotals AllTime, DriverTotals ThisMonth, string Timezone);
public sealed record DriverActiveJob(Guid Id, string? BatchCode, string Status, string DepotName, string FactoryName);
public sealed record DriverDashboardDto(int AvailableJobs, int ActiveJobs, int DeliveredToday, int AwaitingFactory,
    int UnreadNotifications, List<DriverActiveJob> ActivePreview);
