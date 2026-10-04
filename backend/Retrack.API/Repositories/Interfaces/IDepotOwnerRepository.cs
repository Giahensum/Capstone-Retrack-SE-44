using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotOwnerRepository
{
    Task<bool> IsActiveOwnerAsync(Guid ownerId);
    Task<bool> OwnsAsync(Guid ownerId, Guid depotId);
    Task<List<DepotSummaryDto>> ListAsync(Guid ownerId);
    Task<DepotProfileDto> ProfileAsync(Guid depotId);
    Task<Models.Depot> FindAsync(Guid depotId);
    Task SaveAsync();
    Task<PagedResult<FactoryPartnerDto>> FactoriesAsync(Guid depotId, FactorySearchQuery query);
    Task<PagedResult<DemandDto>> DemandsAsync(Guid depotId, DepotQuery query, DateTime now);
}
