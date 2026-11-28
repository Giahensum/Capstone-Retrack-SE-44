using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryPartnershipRepository
{
    Task<FactoryDepotPartnership?> LockAsync(Guid factoryId, Guid depotId, CancellationToken ct);
    Task<bool> HasInspectedBatchAsync(Guid factoryId, Guid depotId, CancellationToken ct);
    Task<bool> HasOpenOrdersAsync(Guid factoryId, Guid depotId, CancellationToken ct);
    Task<InventoryBatch?> FindOwnedBatchAsync(Guid factoryId, Guid batchId, CancellationToken ct);
    Task<FactoryDepotReview?> FindReviewAsync(Guid factoryId, Guid batchId, CancellationToken ct);
    Task<FactoryDepotPartnership?> FindAsync(Guid factoryId, Guid depotId, CancellationToken ct);
    void AddReview(FactoryDepotReview review);
    void AddAudit(AuditLog log);
}
