using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotPaymentReadRepository
{
    Task<PagedResult<DepotPaymentDto>> ListAsync(Guid depotId, DepotQuery query);
    Task<DepotPaymentDto?> FindAsync(Guid depotId, Guid requestId);
    Task<PaymentSummaryDto> SummaryAsync(Guid depotId, DateTime start);
}
