using Retrack.API.DTOs.Factory;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryPartnershipPage(IReadOnlyList<PartnerResponse> Items, int TotalCount);

public interface IFactoryPartnershipReadRepository
{
    Task<FactoryPartnershipPage> ListAsync(Guid factoryId, PageQuery query, CancellationToken ct);
}
