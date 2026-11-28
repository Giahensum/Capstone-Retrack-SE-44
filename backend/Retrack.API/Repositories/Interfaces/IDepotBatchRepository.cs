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
    Task<FactoryDemand?> FindDemandAsync(Guid id);
    Task<FactoryDepotPartnership?> FindPartnerAsync(Guid depotId, Guid factoryId);
    void AddPartner(FactoryDepotPartnership partner);
    Task<long> NextNumberAsync();
    Task<string?> FeeConfigurationAsync();
    void AddBatch(InventoryBatch batch);
    void AddTransport(TransportJob job);
    Task<InventoryBatch?> LockBatchAsync(Guid id);
    Task<bool> HasTransportAsync(Guid batchId);
    Task<PlatformTransaction?> FindFeeAsync(Guid batchId);
    Task<Guid?> FactoryOwnerAsync(Guid factoryId);
    void AddFee(PlatformTransaction transaction);
    void AddWorkflowEvent(BatchWorkflowEvent entry);
    Task<PagedResult<BatchWorkflowEventDto>> WorkflowEventsAsync(Guid batchId, DepotQuery query);
    Task QueuePaymentConfirmedNoticeAsync(InventoryBatch batch);
    Task QueueFactoryBatchNoticeAsync(Guid factoryId, InventoryBatch batch, string title, string message, string targetType = "BATCH");
}
