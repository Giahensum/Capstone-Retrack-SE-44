using Retrack.API.Models;
using Retrack.API.Services.Driver;

namespace Retrack.API.Repositories.Interfaces;

public interface IDriverDeliveryRepository
{
    Task<List<DeliveryEventDto>> EventsAsync(Guid userId, Guid id, CancellationToken ct);
    Task<Guid?> DepotIdAsync(Guid id, CancellationToken ct);
    Task<bool> HasActiveStaffAsync(Guid userId, Guid depotId, CancellationToken ct);
    Task<TransportJob> LockJobAsync(Guid id, CancellationToken ct);
    Task<DriverDeliveryEvent?> FindOperationAsync(Guid userId, Guid id, Guid operationId, CancellationToken ct);
    Task<bool> HasRejectionAsync(Guid userId, Guid id, CancellationToken ct);
    Task QueueAvailableAsync(Guid depotId, Guid id, CancellationToken ct);
    void AddEvent(DriverDeliveryEvent entry);
    void AddNotification(Notification notification);
}
