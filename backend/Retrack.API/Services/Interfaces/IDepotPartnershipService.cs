using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IDepotPartnershipService
{
    Task<PagedResult<DepotPartnershipDto>> ListAsync(Guid ownerId, Guid? depotId, DepotQuery query, CancellationToken ct);
    Task<DepotPartnershipStatusDto> UpdateAsync(Guid ownerId, Guid? depotId, Guid factoryId, string status, CancellationToken ct);
    Task<DepotPartnershipStatusDto> DecideContractAsync(Guid ownerId, Guid? depotId, Guid factoryId, DepotContractDecisionRequest request, CancellationToken ct);
    Task<DepotPartnershipStatusDto> RequestTerminationAsync(Guid ownerId, Guid? depotId, Guid factoryId, DepotContractTerminationRequest request, CancellationToken ct);
    Task<DepotPartnershipStatusDto> DecideTerminationAsync(Guid ownerId, Guid? depotId, Guid factoryId, DepotContractTerminationDecisionRequest request, CancellationToken ct);
}
