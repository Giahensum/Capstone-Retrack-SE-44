using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Employee;

// Lập lại phần còn thiếu, không reset ca đã ngừng hoặc đơn đã bắt đầu.
public sealed class EmployeeDailyPlanService(EmployeeDailyPlanRepository repository,
    IEmployeeDispatchRepository dispatch, IStaffUnitOfWork work)
{
    public async Task PrepareAsync(DateTime now, CancellationToken ct)
    {
        var start = DateTime.SpecifyKind(now.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
        var end = start.AddDays(1);
        for (var page = 0; ; page++)
        {
            var depots = await repository.DepotsAsync(page, ct);
            foreach (var depot in depots)
            {
                await using var tx = await work.BeginAsync(ct);
                await dispatch.LockAsync(ct);
                var areas = await repository.AreasAsync(depot, ct);
                foreach (var region in DanangCollectionCatalog.Regions)
                {
                    var id = DanangCollectionCatalog.AreaId(depot, region.Code);
                    if (areas.Any(a => a.Id == id || (a.Latitude == region.Latitude && a.Longitude == region.Longitude))) continue;
                    var area = new EmployeeCollectionArea { Id = id, DepotId = depot, Name = region.Name,
                        Latitude = region.Latitude, Longitude = region.Longitude, RadiusMeters = region.RadiusMeters };
                    repository.AddArea(area); areas.Add(area);
                }
                await work.SaveAsync(ct);
                var activeAreas = areas.Where(a => a.IsActive).OrderBy(a => a.Id).ToList();
                var employees = (await repository.EmployeesAsync(depot, ct)).ToArray();
                var shifts = await repository.DayShiftsAsync(employees, start, end, ct);
                var busy = await repository.BusyAsync(employees, ct);
                var demand = activeAreas.ToDictionary(a => a.Id, _ => 0);
                foreach (var point in await repository.PointsAsync(depot, end, ct))
                {
                    if (Math.Abs(point.Latitude) > 90 || Math.Abs(point.Longitude) > 180 || activeAreas.Count == 0) continue;
                    var nearest = activeAreas.OrderBy(a => CollectionValidation.Distance((double)point.Latitude, (double)point.Longitude, a.Latitude, a.Longitude)).ThenBy(a => a.Id).First();
                    demand[nearest.Id] += point.Count;
                }
                // Người đang giữ đơn giữ vùng. Người chưa có việc được điều động vào vùng có nhu cầu.
                var movable = shifts.Where(s => s.IsAutomatic && s.Availability == "AVAILABLE" && s.StartsAt <= now && s.EndsAt > now
                    && areas.Any(a => a.Id == s.AreaId) && !busy.Contains(s.EmployeeId)).OrderBy(s => s.EmployeeId).ToList();
                var assigned = shifts.Where(s => !movable.Contains(s) && s.Availability == "AVAILABLE" && s.StartsAt <= now && s.EndsAt > now)
                    .GroupBy(s => s.AreaId).ToDictionary(g => g.Key, g => g.Count());
                Guid NextArea()
                {
                    // Mỗi vùng có đơn chỉ cần một người đang sẵn sàng, không chia theo số đơn.
                    return activeAreas.OrderByDescending(a => demand[a.Id] > 0 && assigned.GetValueOrDefault(a.Id) == 0)
                        .ThenByDescending(a => demand[a.Id]).ThenBy(a => a.Id).First().Id;
                }
                bool NeedsEmployee() => activeAreas.Any(a => demand[a.Id] > 0 && assigned.GetValueOrDefault(a.Id) == 0);
                if (activeAreas.Count > 0)
                {
                    foreach (var shift in movable)
                    {
                        if (NeedsEmployee()) shift.AreaId = NextArea();
                        assigned[shift.AreaId] = assigned.GetValueOrDefault(shift.AreaId) + 1;
                    }
                    foreach (var employee in employees.Where(id => !shifts.Any(s => s.EmployeeId == id)))
                    {
                        if (!NeedsEmployee()) break;
                        var areaId = NextArea();
                        repository.AddShift(new EmployeeDispatchShift { EmployeeId = employee, AreaId = areaId,
                            StartsAt = start, EndsAt = end, IsAutomatic = true, MaxPendingOrders = 50,
                            Reason = "Kế hoạch thu gom tự động theo ngày Việt Nam" });
                        assigned[areaId] = assigned.GetValueOrDefault(areaId) + 1;
                    }
                }
                await work.SaveAsync(ct); await tx.CommitAsync(ct); work.ClearTracking();
            }
            if (depots.Count < 100) break;
        }
    }
}
