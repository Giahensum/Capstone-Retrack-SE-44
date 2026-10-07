using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public interface IDepotBatchRepository
{
    Task<DepotBatchDetailDto?> DetailAsync(Guid depotId, Guid batchId);
    Task<PagedResult<DepotBatchDto>> ListAsync(Guid depotId, DepotQuery query);
    Task LockDepotAsync(Guid depotId);
    Task<InventoryBatch?> FindOperationAsync(Guid id);
    Task<Factory?> FindActiveFactoryAsync(Guid id);
    Task<FactoryDepotPartnership?> FindPartnerAsync(Guid depotId, Guid factoryId);
    void AddPartner(FactoryDepotPartnership partner);
    Task<long> NextNumberAsync();
    void AddBatch(InventoryBatch batch);
    Task AddTransportAsync(TransportJob job, Guid depotId);
    Task<InventoryBatch?> LockBatchAsync(Guid id);
    Task<bool> HasTransportAsync(Guid batchId);
}
