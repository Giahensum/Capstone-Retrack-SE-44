using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Interfaces;

public interface IDepotBuyingPriceService
{
    Task<PagedResult<DepotBuyingPriceDto>> ListAsync(Guid ownerId, Guid depotId, DepotQuery query);
    Task<DepotBuyingPriceDto> SaveAsync(Guid ownerId, Guid depotId, string material, SaveDepotBuyingPriceDto dto);
}
