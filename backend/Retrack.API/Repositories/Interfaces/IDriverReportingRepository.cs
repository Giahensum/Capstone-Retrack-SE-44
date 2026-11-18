using Retrack.API.DTOs;
using Retrack.API.Services.Driver;
namespace Retrack.API.Repositories.Interfaces;
public interface IDriverReportingRepository
{
    Task<DriverDashboardDto> DashboardAsync(Guid userId, DateTime today, CancellationToken ct);
    Task<PagedResult<DriverHistoryRow>> HistoryAsync(Guid userId, int page, int pageSize, string period, string receipt, DateTime now, CancellationToken ct);
    Task<DriverStatsDto> StatsAsync(Guid userId, DateTime now, CancellationToken ct);
}
