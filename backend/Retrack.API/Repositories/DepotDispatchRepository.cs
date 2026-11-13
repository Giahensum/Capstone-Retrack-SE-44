using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

public sealed class DepotDispatchRepository(AppDbContext db) : IDepotDispatchRepository
{
    // Cùng khóa với EmployeeDispatchService TV3; Owner không tạo nguồn phân công song song.
    public Task LockAsync() => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(62051001)");
    public Task<PagedResult<StaffDto>> EmployeesAsync(Guid depot,DepotQuery query)=>DepotRepositoryPage.ReadAsync(
        db.DepotStaffs.AsNoTracking().Where(s=>s.DepotId==depot&&s.IsActive&&s.StaffType=="DEPOT_EMPLOYEE"&&s.User.IsActive&&s.User.Role=="DEPOT_EMPLOYEE")
            .OrderBy(s=>s.User.FullName).ThenBy(s=>s.Id).Select(s=>new StaffDto(s.Id,s.UserId,s.User.FullName,s.User.Email,s.User.Phone,s.StaffType,true)),query);
    public Task<PagedResult<DispatchAreaDto>> AreasAsync(Guid depot, DepotQuery query)
    {
        var rows = db.EmployeeCollectionAreas.AsNoTracking().Where(x => x.DepotId == depot);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.Name.Contains(query.Search));
        return DepotRepositoryPage.ReadAsync(rows.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new DispatchAreaDto(x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusMeters, x.IsActive, x.DepotId)), query);
    }
    public Task<PagedResult<DispatchShiftDto>> ShiftsAsync(Guid depot, DepotQuery query, DateTime now, bool availableOnly = false, Guid? excludeEmployeeId = null)
    {
        var rows = from shift in db.EmployeeDispatchShifts.AsNoTracking()
                   join area in db.EmployeeCollectionAreas on shift.AreaId equals area.Id
                   join employee in db.Users on shift.EmployeeId equals employee.Id
                   where area.DepotId == depot
                   select new { shift, area, employee };
        if (excludeEmployeeId.HasValue) rows = rows.Where(x => x.employee.Id != excludeEmployeeId.Value);
        if (query.Status == "CURRENT" || availableOnly) rows = rows.Where(x => x.shift.StartsAt <= now && x.shift.EndsAt > now);
        if (query.Status == "UPCOMING") rows = rows.Where(x => x.shift.StartsAt > now);
        if (query.Status == "ENDED") rows = rows.Where(x => x.shift.EndsAt <= now);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.employee.FullName.Contains(query.Search) || x.area.Name.Contains(query.Search));
        if (availableOnly) rows = rows.Where(x => x.shift.Availability == "AVAILABLE" && x.area.IsActive && x.employee.IsActive && x.employee.Role == "DEPOT_EMPLOYEE" &&
            db.DepotStaffs.Any(s => s.DepotId == depot && s.UserId == x.employee.Id && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE") &&
            db.PickupRequests.Count(p => p.TargetDepotId == depot && p.AcceptedCollectorId == x.employee.Id && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS")) < x.shift.MaxPendingOrders);
        return DepotRepositoryPage.ReadAsync(rows.OrderByDescending(x => x.shift.StartsAt).ThenBy(x => x.shift.Id).Select(x => new DispatchShiftDto(
            x.shift.Id, x.area.Id, x.area.Name, x.employee.Id, x.employee.FullName, x.shift.StartsAt, x.shift.EndsAt, x.shift.Availability, x.shift.Reason, x.shift.Vehicle, x.shift.CapacityKg, x.shift.MaxPendingOrders,
            db.PickupRequests.Count(p => p.TargetDepotId == depot && p.AcceptedCollectorId == x.employee.Id && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS")))), query);
    }
    private IQueryable<DispatchPickupDto> MapPickups(IQueryable<PickupRequest> rows) => rows.Select(x => new DispatchPickupDto(
        x.Id, x.Seller.FullName, x.Seller.Phone, x.Address, x.Description, x.Latitude, x.Longitude, x.PreferredDatetime, x.Status, x.AcceptedCollectorId, x.AcceptedCollector == null ? null : x.AcceptedCollector.FullName,
        db.EmployeeDispatchQueues.Where(q => q.PickupId == x.Id).Select(q => q.Reason).FirstOrDefault(),
        x.CheckinImageUrl != null && x.CheckinImageUrl != "" || db.PickupCheckIns.Any(c => c.PickupRequestId == x.Id), x.CreatedAt));
    public Task<PagedResult<DispatchPickupDto>> PickupsAsync(Guid depot, DepotQuery query)
    {
        // Filter entity trước projection để truy vấn có thể dịch SQL ổn định.
        var ids = db.PickupRequests.Where(x => x.TargetDepotId == depot);
        ids = query.Status == "SCHEDULED" ? ids.Where(x => x.Status == "SCHEDULED") : ids.Where(x => x.Status == "PENDING");
        if (!string.IsNullOrWhiteSpace(query.Search)) ids = ids.Where(x => x.Address.Contains(query.Search) || x.Seller.FullName.Contains(query.Search));
        return DepotRepositoryPage.ReadAsync(MapPickups(ids.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)), query);
    }
    public Task<DispatchPickupDto?> DetailAsync(Guid depot, Guid id) => MapPickups(db.PickupRequests.AsNoTracking().Where(x => x.TargetDepotId == depot && x.Id == id)).SingleOrDefaultAsync();
    public async Task<DispatchSummaryDto> SummaryAsync(Guid depot, DateTime now) => new(
        await db.EmployeeCollectionAreas.CountAsync(x => x.DepotId == depot && x.IsActive),
        await db.EmployeeDispatchShifts.CountAsync(x => x.StartsAt <= now && x.EndsAt > now && x.Availability != "STOPPED" && db.EmployeeCollectionAreas.Any(a => a.Id == x.AreaId && a.DepotId == depot)),
        await db.PickupRequests.CountAsync(x => x.TargetDepotId == depot && x.Status == "PENDING"));
    public Task<PagedResult<DispatchHistoryDto>> HistoryAsync(Guid depot, Guid id, DepotQuery query) => DepotRepositoryPage.ReadAsync(
        db.EmployeeDispatchEvents.AsNoTracking().Where(x => x.PickupId == id && db.PickupRequests.Any(p => p.Id == id && p.TargetDepotId == depot))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new DispatchHistoryDto(x.Id, x.FromEmployeeId,
                db.Users.Where(u => u.Id == x.FromEmployeeId).Select(u => u.FullName).FirstOrDefault(), x.ToEmployeeId,
                db.Users.Where(u => u.Id == x.ToEmployeeId).Select(u => u.FullName).FirstOrDefault(), x.Reason, x.CreatedAt)), query);
    public async Task<EmployeeCollectionArea?> AreaAsync(Guid id)
    { var row = await db.EmployeeCollectionAreas.SingleOrDefaultAsync(x => x.Id == id); if (row != null) await db.Entry(row).ReloadAsync(); return row; }
    public async Task<EmployeeDispatchShift?> ShiftAsync(Guid id)
    { var row = await db.EmployeeDispatchShifts.SingleOrDefaultAsync(x => x.Id == id); if (row != null) await db.Entry(row).ReloadAsync(); return row; }
    public Task<bool> ActiveEmployeeAsync(Guid depot, Guid employee) => db.DepotStaffs.AnyAsync(x => x.DepotId == depot && x.UserId == employee && x.IsActive && x.StaffType == "DEPOT_EMPLOYEE" && x.User.Role == "DEPOT_EMPLOYEE" && x.User.IsActive);
    public Task<bool> OverlapAsync(Guid employee, DateTime start, DateTime end) => db.EmployeeDispatchShifts.AnyAsync(x => x.EmployeeId == employee && x.Availability != "STOPPED" && x.StartsAt < end && x.EndsAt > start);
    public async Task<PickupRequest?> LockPickupAsync(Guid depot, Guid id)
    {
        var row = await db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id={id} AND target_depot_id={depot} FOR UPDATE").SingleOrDefaultAsync();
        if (row != null) await db.Entry(row).ReloadAsync(); return row;
    }
    public Task<bool> HasCheckInAsync(Guid id) => db.PickupCheckIns.AnyAsync(x => x.PickupRequestId == id);
    public Task<int> AssignedCountAsync(Guid depot, Guid employee) => db.PickupRequests.CountAsync(x => x.TargetDepotId == depot && x.AcceptedCollectorId == employee && (x.Status == "SCHEDULED" || x.Status == "IN_PROGRESS"));
    public Task<EmployeeDispatchEvent?> EventAsync(Guid operation) => db.EmployeeDispatchEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operation);
    public Task<EmployeeDispatchQueue?> QueueAsync(Guid pickup) => db.EmployeeDispatchQueues.SingleOrDefaultAsync(x => x.PickupId == pickup);
    public void AddArea(EmployeeCollectionArea area) => db.EmployeeCollectionAreas.Add(area);
    public void AddShift(EmployeeDispatchShift shift) => db.EmployeeDispatchShifts.Add(shift);
    public void AddEvent(EmployeeDispatchEvent entry) => db.EmployeeDispatchEvents.Add(entry);
    public void AddQueue(EmployeeDispatchQueue queue) => db.EmployeeDispatchQueues.Add(queue);
    public void AddNotice(Notification notice) => db.Notifications.Add(notice);
    // Giữ dữ liệu trả về đã công bố cho client TV3; chỉ chiếu trường đơn giản, không serialize entity graph.
    public async Task<object> ConfigurationAsync(Guid depot, int page)
    {
        var areas = db.EmployeeCollectionAreas.AsNoTracking().Where(a => a.DepotId == depot);
        var waiting = db.EmployeeDispatchQueues.AsNoTracking().Where(q => q.Reason != "ASSIGNED" && q.Reason != "ASSIGNED_MANUAL" &&
            db.PickupRequests.Any(p => p.Id == q.PickupId && p.TargetDepotId == depot && p.Status == "PENDING"));
        return new
        {
            areas = await areas.OrderBy(a => a.Name).ThenBy(a => a.Id).Take(100).Select(a => new DispatchAreaDto(a.Id, a.Name, a.Latitude, a.Longitude, a.RadiusMeters, a.IsActive, a.DepotId)).ToListAsync(),
            shifts = await db.EmployeeDispatchShifts.AsNoTracking().Where(s => areas.Any(a => a.Id == s.AreaId)).OrderByDescending(s => s.StartsAt).ThenBy(s => s.Id).Skip((page - 1) * 20).Take(20)
                .Select(s => new { s.Id, s.AreaId, s.EmployeeId, s.StartsAt, s.EndsAt, s.Availability, s.Reason, s.Vehicle, s.CapacityKg, s.MaxPendingOrders, s.UpdatedAt }).ToListAsync(),
            waiting = await waiting.OrderBy(q => q.UpdatedAt).ThenBy(q => q.PickupId).Skip((page - 1) * 20).Take(20).Select(q => new { q.PickupId, q.Reason, q.UpdatedAt }).ToListAsync(),
            waitingCount = await waiting.CountAsync(),
            page,
            pageSize = 20
        };
    }
    public Task<List<DispatchEventCompatDto>> LegacyHistoryAsync(Guid pickup, int page) => db.EmployeeDispatchEvents.AsNoTracking().Where(e => e.PickupId == pickup)
        .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Skip((page - 1) * 20).Take(20)
        .Select(e => new DispatchEventCompatDto(e.Id, e.PickupId, e.FromEmployeeId, e.ToEmployeeId, e.ShiftId, e.Reason, e.CreatedAt)).ToListAsync();
}
