using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryWorkflowPage(IReadOnlyList<BatchWorkflowEventDto> Items, int TotalCount);

public interface IFactoryWorkflowRepository
{
    Task<Guid> DepotOwnerAsync(Guid depotId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> NotifiedRecipientsAsync(Guid transportJobId, Guid[] recipients, string title, CancellationToken ct);
    void AddNotification(Notification notice);
    Task<PlatformTransaction?> TransactionAsync(Guid batchId, CancellationToken ct);
    Task<bool> HasFeeAsync(Guid batchId, CancellationToken ct);
    Task<bool> OwnsBatchAsync(Guid factoryId, Guid batchId, CancellationToken ct);
    Task<FactoryWorkflowPage> HistoryAsync(Guid batchId, int page, int pageSize, CancellationToken ct);
    Task<bool> HasResolutionAsync(Guid batchId, string status, string note, CancellationToken ct);
    void AddEvent(BatchWorkflowEvent workflowEvent);
    void AddQualityCheck(BatchQualityCheck check);
    Task<FactoryDepotPartnership?> PartnershipAsync(Guid factoryId, Guid depotId, CancellationToken ct);
    void AddPartnership(FactoryDepotPartnership partnership);
    void AddTransport(TransportJob job);
}
