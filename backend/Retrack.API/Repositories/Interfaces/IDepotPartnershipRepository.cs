using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotPartnershipRepository
{
    Task<PagedResult<DepotPartnershipDto>> ListAsync(Guid depotId, DepotQuery query, CancellationToken ct);
    Task<FactoryDepotPartnership?> FindAsync(Guid depotId, Guid factoryId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task LockDepotAsync(Guid depotId, CancellationToken ct);
}
