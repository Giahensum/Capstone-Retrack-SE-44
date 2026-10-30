using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface IDepotDispatchRepository
{
    Task LockAsync();
    Task<PagedResult<DispatchAreaDto>> AreasAsync(Guid depot, DepotQuery query);
    Task<PagedResult<DispatchShiftDto>> ShiftsAsync(Guid depot, DepotQuery query, DateTime now, bool availableOnly = false, Guid? excludeEmployeeId = null);
    Task<PagedResult<DispatchPickupDto>> PickupsAsync(Guid depot, DepotQuery query);
    Task<DispatchSummaryDto> SummaryAsync(Guid depot, DateTime now);
    Task<DispatchPickupDto?> DetailAsync(Guid depot, Guid id);
    Task<PagedResult<DispatchHistoryDto>> HistoryAsync(Guid depot, Guid id, DepotQuery query);
    Task<EmployeeCollectionArea?> AreaAsync(Guid id);
    Task<EmployeeDispatchShift?> ShiftAsync(Guid id);
    Task<bool> ActiveEmployeeAsync(Guid depot, Guid employee);
    Task<bool> OverlapAsync(Guid employee, DateTime start, DateTime end);
    Task<PickupRequest?> LockPickupAsync(Guid depot, Guid id);
    Task<bool> HasCheckInAsync(Guid id);
    Task<int> AssignedCountAsync(Guid depot, Guid employee);
    Task<EmployeeDispatchEvent?> EventAsync(Guid operation);
    Task<EmployeeDispatchQueue?> QueueAsync(Guid pickup);
    void AddArea(EmployeeCollectionArea area);
    void AddShift(EmployeeDispatchShift shift);
    void AddEvent(EmployeeDispatchEvent entry);
    void AddQueue(EmployeeDispatchQueue queue);
    void AddNotice(Notification notice);
}
