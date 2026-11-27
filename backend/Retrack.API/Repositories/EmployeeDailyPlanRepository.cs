using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Repositories;

public sealed record DailyPoint(decimal Latitude, decimal Longitude, int Count);

public sealed class EmployeeDailyPlanRepository(AppDbContext db)
{
    public Task<List<Guid>> DepotsAsync(int page, CancellationToken ct) => db.Depots
        .Where(d => d.Owner.IsActive && d.VerificationStatus == "APPROVED")
        .OrderBy(d => d.Id).Skip(page * 100).Take(100).Select(d => d.Id).ToListAsync(ct);
    public Task<List<EmployeeCollectionArea>> AreasAsync(Guid depot, CancellationToken ct) =>
        db.EmployeeCollectionAreas.Where(a => a.DepotId == depot).OrderBy(a => a.Id).ToListAsync(ct);
    public Task<List<Guid>> EmployeesAsync(Guid depot, CancellationToken ct) => db.DepotStaffs
        .Where(s => s.DepotId == depot && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE")
        .Select(s => s.UserId).Distinct().OrderBy(id => id).ToListAsync(ct);
    public Task<List<EmployeeDispatchShift>> DayShiftsAsync(Guid[] employees, DateTime start, DateTime end, CancellationToken ct) =>
        db.EmployeeDispatchShifts.Where(s => employees.Contains(s.EmployeeId) && s.StartsAt < end && s.EndsAt > start).ToListAsync(ct);
    public Task<List<DailyPoint>> PointsAsync(Guid depot, DateTime end, CancellationToken ct) {
        var day=DateOnly.FromDateTime(end.AddHours(7).AddDays(-1));
        return db.PickupRequests
        .Where(p => !p.RequiresOwnerReview && p.TargetDepotId == depot && (p.Status == "PENDING" || p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS")
            && (p.CollectionDate == null || p.CollectionDate == day)
            && (p.CollectionDate != null || (p.PreferredDatetime ?? p.CreatedAt) >= end.AddDays(-1))
            && (p.PreferredDatetime == null || p.PreferredDatetime < end) && p.Latitude.HasValue && p.Longitude.HasValue)
        .GroupBy(p => new { p.Latitude, p.Longitude })
        .Select(g => new DailyPoint(g.Key.Latitude!.Value, g.Key.Longitude!.Value, g.Count())).ToListAsync(ct);
    }
    public Task<HashSet<Guid>> BusyAsync(Guid[] employees, CancellationToken ct) => BusyListAsync(employees, ct);
    private async Task<HashSet<Guid>> BusyListAsync(Guid[] employees, CancellationToken ct) => (await db.PickupRequests
        .Where(p => p.AcceptedCollectorId.HasValue && employees.Contains(p.AcceptedCollectorId.Value)
            && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS" || p.Status == "WEIGHED" || p.Status == "SELLER_CONFIRMED"))
        .Select(p => p.AcceptedCollectorId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
    public void AddArea(EmployeeCollectionArea area) => db.EmployeeCollectionAreas.Add(area);
    public void AddShift(EmployeeDispatchShift shift) => db.EmployeeDispatchShifts.Add(shift);
}
