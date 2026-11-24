namespace Retrack.API.Services.Interfaces;

public interface IInventoryService
{
    Task<List<Retrack.API.DTOs.Depot.InventoryRowDto>> GetAsync(Guid ownerId, Guid depotId);
    Task<Retrack.API.DTOs.PagedResult<Retrack.API.DTOs.Depot.InventoryHistoryDto>> HistoryAsync(Guid ownerId, Guid depotId, Retrack.API.DTOs.Depot.InventoryHistoryQuery query);
}


