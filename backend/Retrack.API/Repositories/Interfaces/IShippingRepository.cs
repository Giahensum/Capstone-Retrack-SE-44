using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
namespace Retrack.API.Repositories.Interfaces;

public interface IShippingTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IShippingRepository
{
    Task<IShippingTransaction> BeginAsync(CancellationToken ct);
    Task LockDepotAsync(Guid depotId, CancellationToken ct);
    Task<InventoryBatch?> FindBatchAsync(Guid depotId, Guid batchId, CancellationToken ct);
    Task<InventoryBatch> FindBatchAsync(Guid batchId, CancellationToken ct);
    Task<Depot> FindDepotAsync(Guid depotId, CancellationToken ct);
    Task<Factory?> FindActiveFactoryAsync(Guid factoryId, CancellationToken ct);
    Task<ShippingQuote?> FindQuoteAsync(Guid ownerId, Guid depotId, Guid id, CancellationToken ct);
    Task<ShippingQuote?> ReadQuoteAsync(Guid id, CancellationToken ct);
    Task<ExternalShipment?> ReadLatestAsync(Guid batchId, CancellationToken ct);
    Task<ExternalShipment?> ReadRecoveryAsync(Guid batchId, CancellationToken ct);
    Task<ExternalShipment?> FindLatestAsync(Guid batchId, CancellationToken ct);
    Task<List<ShippingEventDto>> EventsAsync(Guid shipmentId, CancellationToken ct);
    Task<bool> HasOpenShipmentAsync(Guid batchId, string[] terminal, CancellationToken ct);
    Task<bool> HasUsedQuoteAsync(Guid quoteId, CancellationToken ct);
    Task<ExternalShipment?> FindOperationAsync(Guid operationId, CancellationToken ct);
    Task<ExternalShipment> FindShipmentAsync(Guid id, CancellationToken ct);
    Task<ExternalShipment> ReadShipmentAsync(Guid id, CancellationToken ct);
    Task<Guid> DepotForShipmentAsync(Guid shipmentId, CancellationToken ct);
    void AddQuote(ShippingQuote quote);
    void AddShipment(ExternalShipment shipment);
    void AddTransport(TransportJob job);
    void AddEvent(ShippingEvent entry);
    void ClearTracking();
    Task SaveAsync(CancellationToken ct);
    Task TouchSandboxWebhookAsync(string orderId, CancellationToken ct);
    Task MarkStaleBookingsAsync(DateTime staleBefore, string message, CancellationToken ct);
    Task<List<Guid>> ReconciliationIdsAsync(string provider, DateTime cutoff, CancellationToken ct);
}
