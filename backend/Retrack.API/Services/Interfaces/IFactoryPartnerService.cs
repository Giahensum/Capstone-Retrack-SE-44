using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IFactoryPartnerService
{
    Task<ServiceResult<PageResponse<PartnerResponse>>> ListAsync(Guid userId, PageQuery query, CancellationToken ct);
    Task<ServiceResult<PartnerStatusResponse>> UpdateStatusAsync(Guid userId, Guid depotId, PartnerStatusRequest request, CancellationToken ct);
    Task<ServiceResult<PartnerStatusResponse>> ProposeContractAsync(Guid userId, Guid depotId, PartnershipContractRequest request, CancellationToken ct);
    Task<ServiceResult<PartnerStatusResponse>> RequestTerminationAsync(Guid userId, Guid depotId, ContractTerminationRequest request, CancellationToken ct);
    Task<ServiceResult<PartnerStatusResponse>> DecideTerminationAsync(Guid userId, Guid depotId, ContractTerminationDecisionRequest request, CancellationToken ct);
    Task<ServiceResult<PartnerRatingResponse>> RateAsync(Guid userId, Guid orderId, RatingRequest request, CancellationToken ct);
}
