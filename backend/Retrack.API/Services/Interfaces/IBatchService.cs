namespace Retrack.API.Services.Interfaces;

public interface IBatchService
{
    Task<Retrack.API.DTOs.PagedResult<Retrack.API.DTOs.Depot.DepotBatchDto>> ListAsync(Guid ownerId, Guid depotId, Retrack.API.DTOs.Depot.DepotQuery query);
    Task<Retrack.API.DTOs.Depot.DepotBatchDto> CreateAsync(Guid ownerId, Guid depotId, Retrack.API.DTOs.Depot.CreateDepotBatchDto dto);
    Task CancelAsync(Guid ownerId, Guid depotId, Guid batchId);
}


