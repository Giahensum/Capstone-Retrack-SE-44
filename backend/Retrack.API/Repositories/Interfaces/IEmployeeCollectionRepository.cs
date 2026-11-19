using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Models.Enums;

namespace Retrack.API.Repositories.Interfaces;

public interface IEmployeeCollectionRepository
{
    Task<bool> HasActiveStaffAsync(Guid userId, Guid? depotId, CancellationToken ct);
    Task<PickupRequest?> FindPickupAsync(Guid id, bool locked, CancellationToken ct);
    Task<PickupCheckIn?> FindCheckInAsync(Guid id, CancellationToken ct);
    Task<PickupCheckIn?> ReadCheckInAsync(Guid id, CancellationToken ct);
    Task<List<PickupRequestItem>> ItemsAsync(Guid id, CancellationToken ct);
    Task<List<ClassificationItemDto>> ReadItemsAsync(Guid id, CancellationToken ct);
    Task<bool> HasEventAsync(Guid id, string action, int revision, CancellationToken ct);
    Task<EmployeeCollectionEvent?> SubmittedAsync(Guid id, int revision, CancellationToken ct);
    Task<bool> ActiveSellerAsync(Guid sellerId, CancellationToken ct);
    Task<Guid> OwnerIdAsync(Guid? depotId, CancellationToken ct);
    Task<MarketPrice?> LatestPriceAsync(MaterialType type, DateTime now, CancellationToken ct);
    void AddCheckIn(PickupCheckIn evidence);
    void ReplaceItems(IEnumerable<PickupRequestItem> previous, IEnumerable<PickupRequestItem> items);
    void AddEvent(EmployeeCollectionEvent entry);
    void AddNotification(Notification notification);
}
