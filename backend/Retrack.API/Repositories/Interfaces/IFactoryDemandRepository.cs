using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryDemandPage(IReadOnlyList<FactoryDemand> Items, int TotalCount);

public interface IFactoryDemandRepository
{
    Task<FactoryDemandPage> ListAsync(Guid factoryId, string[]? materialValues, int page, int pageSize, CancellationToken ct);
    Task<FactoryDemand?> FindAsync(Guid factoryId, Guid demandId, CancellationToken ct);
    Task<bool> HasBatchAsync(Guid demandId, CancellationToken ct);
    void Add(FactoryDemand demand);
    void Remove(FactoryDemand demand);
    void ResetTrackedDelete(FactoryDemand demand);
}
