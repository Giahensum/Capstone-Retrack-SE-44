using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Employee;

public sealed class EmployeeReportingService(AppDbContext db)
{
    public static readonly string[] HistoryStates = ["SCHEDULED", "IN_PROGRESS", "WEIGHED", "SELLER_CONFIRMED", "AWAITING_PAYMENT", "PAYMENT_SENT", "DONE", "CANCELLED"];
    private IQueryable<DepotStaff> Staff(Guid id) => db.DepotStaffs.AsNoTracking().Where(s => s.UserId == id
        && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive);
    public async Task RequireAsync(Guid id, CancellationToken ct)
    {
        if (!await Staff(id).AnyAsync(ct)) throw new DepotForbiddenException();
    }
    private IQueryable<PickupRequest> Own(Guid id) => db.PickupRequests.AsNoTracking().Where(p => p.AcceptedCollectorId == id
        && Staff(id).Any(s => s.DepotId == p.TargetDepotId));
    public static void Page(int page, int size)
    {
        if (page is < 1 or > 100000 || size is < 1 or > 50) throw new ArgumentException("Trang từ 1 đến 100.000; kích thước trang từ 1 đến 50.");
    }
    public static DateTime VietnamDayStart(DateTime utc) => DateTime.SpecifyKind(utc.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
    public static DateTime VietnamMonthStart(DateTime utc)
    {
        var local = utc.AddHours(7);
        return new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);
    }
    public async Task<EmployeeDashboardDto> DashboardAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        var own = Own(id);
        // Pool hiện dùng kho chính (liên kết đầu tiên); bộ đếm đơn chờ phải cùng phạm vi.
        var poolDepotId = await Staff(id).OrderBy(s => s.Id).Select(s => s.DepotId).FirstAsync(ct);
        var active = await own.Where(p => p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS" || p.Status == "SELLER_CONFIRMED")
            .OrderBy(p => p.Status == "SELLER_CONFIRMED" ? 0 : 1).ThenBy(p => p.PreferredDatetime == null)
            .ThenBy(p => p.PreferredDatetime).ThenBy(p => p.Id)
            .Select(p => new ActivePickupSummaryDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address, p.Status, p.PreferredDatetime)).FirstOrDefaultAsync(ct);
        var today = VietnamDayStart(DateTime.UtcNow);
        return new(await db.PickupRequests.CountAsync(p => p.Status == "PENDING" && p.AcceptedCollectorId == null
                && p.TargetDepotId == poolDepotId, ct), active,
            await own.CountAsync(p => p.Status == "DONE" && p.UpdatedAt >= today && p.UpdatedAt < today.AddDays(1), ct),
            await own.CountAsync(p => p.Status == "DONE", ct),
            await own.CountAsync(p => p.Status == "IN_PROGRESS", ct),
            await own.CountAsync(p => p.Status == "WEIGHED", ct),
            await own.CountAsync(p => p.Status == "SELLER_CONFIRMED", ct),
            await own.CountAsync(p => p.Status == "AWAITING_PAYMENT" || p.Status == "PAYMENT_SENT", ct),
            await db.Notifications.CountAsync(n => n.UserId == id && !n.IsRead, ct));
    }
    public async Task<PagedResult<EmployeeHistoryRow>> HistoryAsync(Guid id, int page, int size, string? status, CancellationToken ct)
    {
        await RequireAsync(id, ct); Page(page, size);
        if (status != null && !HistoryStates.Contains(status)) throw new ArgumentException("Trạng thái lọc không hợp lệ.");
        var query = Own(id).Where(p => HistoryStates.Contains(p.Status));
        if (status != null) query = query.Where(p => p.Status == status);
        return new() { Page = page, PageSize = size, TotalCount = await query.CountAsync(ct),
            Items = await query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id).Skip((page - 1) * size).Take(size)
                .Select(p => new EmployeeHistoryRow(p.Id, p.Seller.FullName, p.Address, p.Status, p.UpdatedAt, p.GrossAmount)).ToListAsync(ct) };
    }
    public async Task<EmployeeStatsDto> StatsAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        var all = Own(id).Where(p => p.Status == "DONE");
        var start = VietnamMonthStart(DateTime.UtcNow);
        var month = all.Where(p => p.UpdatedAt >= start && p.UpdatedAt < start.AddHours(7).AddMonths(1).AddHours(-7));
        async Task<EmployeeTotals> Total(IQueryable<PickupRequest> query) => new(await query.CountAsync(ct),
            await db.PickupRequestItems.Where(i => query.Any(p => p.Id == i.PickupRequestId)).SumAsync(i => (decimal?)i.WeightKg, ct) ?? 0,
            await query.SumAsync(p => (decimal?)p.GrossAmount, ct) ?? 0);
        return new(await Total(all), await Total(month), "Asia/Ho_Chi_Minh", "updated_at của đơn DONE");
    }
    public async Task<EmployeeNotificationPage> NotificationsAsync(Guid id, int page, int size, CancellationToken ct)
    {
        await RequireAsync(id, ct); Page(page, size);
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == id);
        return new(await query.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip((page - 1) * size).Take(size)
            .Select(n => new EmployeeNotificationDto(n.Id, n.Title, n.Message, n.IsRead, n.CreatedAt)).ToListAsync(ct),
            await query.CountAsync(ct), await query.CountAsync(n => !n.IsRead, ct), page, size);
    }
    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        await RequireAsync(userId, ct);
        var changed = await db.Notifications.Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true), ct);
        if (changed == 0) throw new KeyNotFoundException("Không tìm thấy thông báo của bạn.");
    }
}

public sealed record EmployeeHistoryRow(Guid Id, string SellerName, string Address, string Status, DateTime UpdatedAt, decimal GrossAmount);
public sealed record EmployeeTotals(int Pickups, decimal WeightKg, decimal GrossAmount);
public sealed record EmployeeStatsDto(EmployeeTotals AllTime, EmployeeTotals ThisMonth, string Timezone, string DateBasis);
public sealed record EmployeeNotificationDto(Guid Id, string Title, string? Message, bool IsRead, DateTime CreatedAt);
public sealed record EmployeeNotificationPage(List<EmployeeNotificationDto> Items, int TotalCount, int UnreadCount, int Page, int PageSize);
