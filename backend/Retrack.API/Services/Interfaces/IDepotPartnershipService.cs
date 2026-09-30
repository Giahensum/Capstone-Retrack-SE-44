using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IDepotPartnershipService
{
    Task<List<DepotPartnershipDto>> ListAsync(Guid ownerId, Guid? depotId, CancellationToken ct);
    Task<DepotPartnershipStatusDto> UpdateAsync(Guid ownerId, Guid? depotId, Guid factoryId, string status, CancellationToken ct);
}
