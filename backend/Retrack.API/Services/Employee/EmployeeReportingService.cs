using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Employee;

public sealed class EmployeeReportingService(IEmployeeReportingRepository repository)
{
    public static readonly string[] HistoryStates = ["SCHEDULED", "IN_PROGRESS", "WEIGHED", "SELLER_CONFIRMED", "AWAITING_PAYMENT", "PAYMENT_SENT", "DONE", "CANCELLED"];
    public async Task RequireAsync(Guid id, CancellationToken ct)
    {
        if (!await repository.HasActiveStaffAsync(id, ct)) throw new DepotForbiddenException();
    }
    public static void Page(int page, int size)
    {
        if (page is < 1 or > 100000 || size is < 1 or > 50) throw new ArgumentException("Trang từ 1 đến 100.000; kích thước trang từ 1 đến 50.");
    }
    public static DateTime VietnamDayStart(DateTime utc) => Retrack.API.Helpers.VietnamTime.DayStart(utc);
    public static DateTime VietnamMonthStart(DateTime utc) => Retrack.API.Helpers.VietnamTime.MonthStart(utc);
    public async Task<EmployeeDashboardDto> DashboardAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        return await repository.DashboardAsync(id, VietnamDayStart(DateTime.UtcNow), ct);
    }
    public async Task<PagedResult<EmployeeHistoryRow>> HistoryAsync(Guid id, int page, int size, string? status, CancellationToken ct)
    {
        await RequireAsync(id, ct); Page(page, size);
        if (status != null && !HistoryStates.Contains(status)) throw new ArgumentException("Trạng thái lọc không hợp lệ.");
        return await repository.HistoryAsync(id, page, size, status, ct);
    }
    public async Task<EmployeeStatsDto> StatsAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        return await repository.StatsAsync(id, VietnamMonthStart(DateTime.UtcNow), ct);
    }
    public async Task<EmployeeNotificationPage> NotificationsAsync(Guid id, int page, int size, CancellationToken ct)
    {
        await RequireAsync(id, ct); Page(page, size);
        return await repository.NotificationsAsync(id, page, size, ct);
    }
    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        await RequireAsync(userId, ct);
        if (await repository.MarkReadAsync(userId, notificationId, ct) == 0) throw new KeyNotFoundException("Không tìm thấy thông báo của bạn.");
    }
}
