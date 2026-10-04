using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IDepotService
{
    Task<List<DepotSummaryDto>> GetDepotsAsync(Guid ownerId);
    Task RequireOwnerAsync(Guid ownerId, Guid depotId);
    Task<PagedResult<DepotPaymentDto>> GetPaymentsAsync(Guid ownerId, Guid depotId, DepotQuery query);
    Task<DepotPaymentDto> GetPaymentAsync(Guid ownerId, Guid depotId, Guid requestId);
    Task<PaymentSummaryDto> GetPaymentSummaryAsync(Guid ownerId, Guid depotId);
    Task<DepotProfileDto> GetProfileAsync(Guid ownerId, Guid depotId);
    Task<DepotProfileDto> UpdateProfileAsync(Guid ownerId, Guid depotId, UpdateDepotProfileDto dto);
    Task<PagedResult<FactoryPartnerDto>> GetFactoriesAsync(Guid ownerId, Guid depotId, FactorySearchQuery query);
    Task<PagedResult<DemandDto>> GetDemandsAsync(Guid ownerId, Guid depotId, DepotQuery query);
}


