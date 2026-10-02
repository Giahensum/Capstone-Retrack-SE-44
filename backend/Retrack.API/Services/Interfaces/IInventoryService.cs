namespace Retrack.API.Services.Interfaces;

public interface IInventoryService
{
    Task<List<Retrack.API.DTOs.Depot.InventoryRowDto>> GetAsync(Guid ownerId, Guid depotId);
}


