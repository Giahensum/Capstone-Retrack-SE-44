using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryDemandPage(IReadOnlyList<FactoryDemand> Items, int TotalCount);

public interface IFactoryDemandRepository
{
    Task<FactoryDemandPage> ListAsync(Guid factoryId, string[]? materialValues, int page, int pageSize, CancellationToken ct);
}
