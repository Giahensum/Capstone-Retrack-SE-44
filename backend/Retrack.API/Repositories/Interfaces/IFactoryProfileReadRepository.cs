using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryProfileSnapshot(Factory Factory, string? FeeConfig, bool IsSubscription);

public interface IFactoryProfileReadRepository
{
    Task<FactoryProfileSnapshot> GetAsync(Guid factoryId, CancellationToken ct);
}
