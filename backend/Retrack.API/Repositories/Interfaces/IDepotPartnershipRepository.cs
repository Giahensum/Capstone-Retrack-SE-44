using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotPartnershipRepository
{
    Task<List<DepotPartnershipDto>> ListAsync(Guid depotId, CancellationToken ct);
    Task<FactoryDepotPartnership?> FindAsync(Guid depotId, Guid factoryId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
