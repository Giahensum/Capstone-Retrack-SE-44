namespace Retrack.API.Repositories.Interfaces;

public interface IDepotInventoryRepository
{
    Task<List<DepotReceivedWeight>> ReceivedAsync(Guid depotId);
    Task<List<DepotAllocatedWeight>> AllocatedAsync(Guid depotId);
    Task<Retrack.API.DTOs.PagedResult<Retrack.API.DTOs.Depot.InventoryHistoryDto>> HistoryAsync(Guid depotId, Retrack.API.DTOs.Depot.InventoryHistoryQuery query);
}
