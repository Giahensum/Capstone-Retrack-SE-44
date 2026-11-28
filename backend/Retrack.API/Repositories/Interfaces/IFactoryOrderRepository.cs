using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryOrderRepository
{
    Task<InventoryBatch?> LockAsync(Guid id, Guid factoryId, CancellationToken ct);
    Task<InventoryBatch?> LockForReceivingAsync(Guid id, Guid factoryId, CancellationToken ct);
    Task<InventoryBatch?> LockMarketAsync(Guid id, bool includeTransport, CancellationToken ct);
}
