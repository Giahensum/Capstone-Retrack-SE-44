using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;
public sealed record DispatchDepotLocation(Guid Id, decimal? Latitude, decimal? Longitude);
public interface IEmployeeDispatchRepository
{
    Task LockAsync(CancellationToken ct);
    Task<bool> HasStaffAsync(Guid userId, Guid? depotId, CancellationToken ct);
    Task<object> MineAsync(Guid userId, DateTime now, CancellationToken ct);
    Task<EmployeeDispatchShift?> FindShiftAsync(Guid shiftId, Guid userId, CancellationToken ct);
    Task<EmployeeCollectionArea> AreaAsync(Guid areaId, CancellationToken ct);
    Task<List<Guid>> UnstartedAsync(Guid depotId, Guid userId, CancellationToken ct);
    Task<Guid> OwnerIdAsync(Guid? depotId, CancellationToken ct);
    Task<List<Guid>> DueAsync(DateTime endDay, CancellationToken ct);
    Task<PickupRequest?> LockPickupAsync(Guid id, CancellationToken ct);
    Task<bool> HasCheckInAsync(Guid id, CancellationToken ct);
    Task<bool> KeepManualAsync(PickupRequest pickup, DateTime now, CancellationToken ct);
    Task<List<EmployeeCollectionArea>> AreasAsync(Guid? depotId, CancellationToken ct);
    Task<List<DispatchDepotLocation>> DepotLocationsAsync(Guid[] depotIds, CancellationToken ct);
    Task<List<EmployeeDispatchShift>> AvailableShiftsAsync(Guid[] areaIds, Guid depotId, DateTime now, CancellationToken ct);
    Task<Dictionary<Guid, int>> WorkloadAsync(Guid depotId, CancellationToken ct);
    Task<List<Guid>> ExcludedAsync(Guid id, DateTime day, CancellationToken ct);
    Task<EmployeeDispatchQueue?> QueueAsync(Guid id, CancellationToken ct);
    void AddEvent(EmployeeDispatchEvent entry);
    void AddQueue(EmployeeDispatchQueue entry);
    void AddNotification(Notification entry);
}
