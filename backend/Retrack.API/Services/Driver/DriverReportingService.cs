using Retrack.API.DTOs.Driver;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.Services.Employee;

namespace Retrack.API.Services.Driver;

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
