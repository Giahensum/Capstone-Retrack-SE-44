using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.Services.Employee;

namespace Retrack.API.Services.Driver;

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

public sealed class DriverReportingService(IDriverReportingRepository repository, DriverJobService jobs)
{
    public async Task<DriverDashboardDto> DashboardAsync(Guid userId, CancellationToken ct)
    {
        await jobs.RequireAsync(userId, ct);
        return await repository.DashboardAsync(userId, EmployeeReportingService.VietnamDayStart(DateTime.UtcNow), ct);
    }
    public async Task<PagedResult<DriverHistoryRow>> HistoryAsync(Guid userId, int page, int pageSize, string period, string receipt, CancellationToken ct)
    {
        await jobs.RequireAsync(userId, ct);
        EmployeeReportingService.Page(page, pageSize);
        if (period is not ("all" or "month") || receipt is not ("all" or "waiting" or "received"))
            throw new ArgumentException("Bộ lọc lịch sử không hợp lệ.");
        return await repository.HistoryAsync(userId, page, pageSize, period, receipt, DateTime.UtcNow, ct);
    }
    public async Task<DriverStatsDto> StatsAsync(Guid userId, CancellationToken ct)
    {
        await jobs.RequireAsync(userId, ct);
        return await repository.StatsAsync(userId, DateTime.UtcNow, ct);
    }
}
