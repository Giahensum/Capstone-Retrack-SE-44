using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;
public sealed class EmployeeDispatchRepository(AppDbContext db) : IEmployeeDispatchRepository
{
    private IQueryable<DepotStaff> Staff(Guid id) => db.DepotStaffs.Where(s => s.UserId == id
        && s.StaffType == "DEPOT_EMPLOYEE" && s.IsActive && s.User.IsActive
        && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive);


    public async Task LockAsync(CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(62051001)", ct);
    }

    public Task<bool> HasStaffAsync(Guid userId, Guid? depotId, CancellationToken ct) =>
        Staff(userId).AnyAsync(s => depotId == null || s.DepotId == depotId, ct);

    public async Task<object> MineAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        return await (from shift in db.EmployeeDispatchShifts.AsNoTracking()
            join area in db.EmployeeCollectionAreas on shift.AreaId equals area.Id
            where shift.EmployeeId == userId && shift.EndsAt > now && shift.StartsAt <= now
                && Staff(userId).Any(s => s.DepotId == area.DepotId)
            orderby shift.StartsAt, shift.Id
            select new { shift.Id, area.Name, area.DepotId, shift.StartsAt, shift.EndsAt,
                shift.Availability, shift.Reason, shift.Vehicle, shift.CapacityKg, shift.MaxPendingOrders, shift.IsAutomatic })
            .Take(20).ToListAsync(ct);
    }

    public Task<EmployeeDispatchShift?> FindShiftAsync(Guid shiftId, Guid userId, CancellationToken ct) =>
        db.EmployeeDispatchShifts.SingleOrDefaultAsync(s => s.Id == shiftId && s.EmployeeId == userId, ct);

    public Task<EmployeeCollectionArea> AreaAsync(Guid areaId, CancellationToken ct) =>
        db.EmployeeCollectionAreas.SingleAsync(a => a.Id == areaId, ct);

    public Task<List<Guid>> UnstartedAsync(Guid depotId, Guid userId, CancellationToken ct) =>
        db.PickupRequests.Where(p => p.TargetDepotId == depotId
            && p.AcceptedCollectorId == userId && p.Status == "SCHEDULED"
            && !db.PickupCheckIns.Any(c => c.PickupRequestId == p.Id))
            .OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);

    public Task<Guid> OwnerIdAsync(Guid? depotId, CancellationToken ct) =>
        db.Depots.Where(d => d.Id == depotId).Select(d => d.OwnerId).SingleAsync(ct);

    public Task<List<Guid>> DueAsync(DateTime endDay, CancellationToken ct) =>
        db.PickupRequests.AsNoTracking()
            .Where(p => !p.RequiresOwnerReview && (p.Status == "PENDING" || p.Status == "SCHEDULED")
                && (p.TargetDepotId == null || db.EmployeeCollectionAreas.Any(a => a.DepotId == p.TargetDepotId))
                && (p.PreferredDatetime == null || p.PreferredDatetime < endDay)
                && (p.CollectionDate == null || p.CollectionDate < DateOnly.FromDateTime(endDay.AddHours(7)))
                && !db.PickupCheckIns.Any(c => c.PickupRequestId == p.Id))
            .OrderBy(p => db.EmployeeDispatchQueues.Where(q => q.PickupId == p.Id)
                .Select(q => (DateTime?)q.UpdatedAt).FirstOrDefault())
            .ThenBy(p => p.PreferredDatetime ?? p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => p.Id).Take(300).ToListAsync(ct);

    public async Task<PickupRequest?> LockPickupAsync(Guid id, CancellationToken ct)
    {
        var query = db.Database.IsNpgsql()
            ? db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {id} FOR UPDATE")
            : db.PickupRequests.Where(p => p.Id == id);
        var pickup = await query.SingleOrDefaultAsync(ct);
        if (pickup != null) await db.Entry(pickup).ReloadAsync(ct);
        return pickup;
    }

    public Task<bool> HasCheckInAsync(Guid id, CancellationToken ct) =>
        db.PickupCheckIns.AnyAsync(c => c.PickupRequestId == id, ct);

    public async Task<bool> KeepManualAsync(PickupRequest pickup, DateTime now, CancellationToken ct) =>
        pickup.AcceptedCollectorId.HasValue && pickup.TargetDepotId.HasValue &&
            await db.EmployeeDispatchQueues.AnyAsync(q => q.PickupId == pickup.Id && q.Reason == "ASSIGNED_MANUAL", ct) &&
            await db.EmployeeDispatchShifts.AnyAsync(s => s.EmployeeId == pickup.AcceptedCollectorId && s.Availability == "AVAILABLE" && s.StartsAt < (pickup.PreferredEndDatetime ?? now.AddHours(7).Date.AddDays(1).AddHours(-7)) && s.EndsAt > now &&
                (pickup.PreferredDatetime == null || pickup.PreferredDatetime < s.EndsAt) &&
                db.EmployeeCollectionAreas.Any(a => a.Id == s.AreaId && a.DepotId == pickup.TargetDepotId && a.IsActive), ct) &&
            await db.DepotStaffs.AnyAsync(s => s.DepotId == pickup.TargetDepotId && s.UserId == pickup.AcceptedCollectorId && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive && s.Depot.VerificationStatus == "APPROVED", ct);

    public Task<List<EmployeeCollectionArea>> AreasAsync(Guid? depotId, CancellationToken ct) =>
        db.EmployeeCollectionAreas.Where(a => a.IsActive
            && (depotId == null || a.DepotId == depotId)
            && db.Depots.Any(d => d.Id == a.DepotId && d.Owner.IsActive && d.VerificationStatus == "APPROVED")
            && (depotId != null || !db.SystemConfigs.Any(c => c.ConfigKey == "BUSINESS_MODEL" && c.ConfigValue == "DEPOT_SUBSCRIPTION") ||
                db.DepotSubscriptions.Any(s => s.DepotId == a.DepotId && (s.PaidUntil > DateTime.UtcNow || s.TrialEndsAt > DateTime.UtcNow))))
            .OrderBy(a => a.Id).ToListAsync(ct);

    public Task<List<DispatchDepotLocation>> DepotLocationsAsync(Guid[] depotIds, CancellationToken ct) =>
        db.Depots.Where(d => depotIds.Contains(d.Id))
                .Select(d => new DispatchDepotLocation(d.Id, d.Latitude, d.Longitude)).ToListAsync(ct);

    public Task<List<EmployeeDispatchShift>> AvailableShiftsAsync(Guid[] areaIds, Guid depotId, DateTime now, CancellationToken ct) =>
        db.EmployeeDispatchShifts.Where(s => areaIds.Contains(s.AreaId)
                && s.StartsAt <= now && s.EndsAt > now && s.Availability == "AVAILABLE"
                && db.DepotStaffs.Any(st => st.DepotId == depotId && st.UserId == s.EmployeeId && st.IsActive
                    && st.StaffType == "DEPOT_EMPLOYEE" && st.User.IsActive && st.User.Role == "DEPOT_EMPLOYEE"))
                .OrderBy(s => s.Id).ToListAsync(ct);

    public Task<Dictionary<Guid, int>> WorkloadAsync(Guid depotId, CancellationToken ct) =>
        db.PickupRequests.Where(p => p.TargetDepotId == depotId && p.AcceptedCollectorId != null
                && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS"))
                .GroupBy(p => p.AcceptedCollectorId!.Value).Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

    public Task<List<Guid>> ExcludedAsync(Guid id, DateTime day, CancellationToken ct) =>
        db.EmployeeDispatchEvents.Where(e => e.PickupId == id
                && e.FromEmployeeId != null && e.CreatedAt >= day).Select(e => e.FromEmployeeId!.Value).ToListAsync(ct);

    public async Task<EmployeeDispatchQueue?> QueueAsync(Guid id, CancellationToken ct) =>
        await db.EmployeeDispatchQueues.FindAsync([id], ct);

    public void AddEvent(EmployeeDispatchEvent entry) => db.EmployeeDispatchEvents.Add(entry);

    public void AddQueue(EmployeeDispatchQueue entry) => db.EmployeeDispatchQueues.Add(entry);

    public void AddNotification(Notification entry) => db.Notifications.Add(entry);
}
