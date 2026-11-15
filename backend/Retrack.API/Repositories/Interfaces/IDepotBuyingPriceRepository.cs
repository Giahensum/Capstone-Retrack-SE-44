using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotBuyingPriceRepository
{
    Task<PagedResult<DepotBuyingPriceDto>> ListAsync(Guid depotId, DepotQuery query);
    Task<DepotBuyingPrice?> FindAsync(Guid depotId, string material);
    void Add(DepotBuyingPrice price);
}
