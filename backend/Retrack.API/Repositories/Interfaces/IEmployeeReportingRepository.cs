using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Services.Employee;
namespace Retrack.API.Repositories.Interfaces;
public interface IEmployeeReportingRepository
{
    Task<bool> HasActiveStaffAsync(Guid id, CancellationToken ct);
    Task<EmployeeDashboardDto> DashboardAsync(Guid id, DateTime today, CancellationToken ct);
    Task<PagedResult<EmployeeHistoryRow>> HistoryAsync(Guid id, int page, int size, string? status, CancellationToken ct);
    Task<EmployeeStatsDto> StatsAsync(Guid id, DateTime start, CancellationToken ct);
    Task<EmployeeNotificationPage> NotificationsAsync(Guid id, int page, int size, CancellationToken ct);
    Task<int> MarkReadAsync(Guid id, Guid notificationId, CancellationToken ct);
}
