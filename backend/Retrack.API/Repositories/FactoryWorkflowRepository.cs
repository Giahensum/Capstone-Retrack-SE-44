using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryWorkflowRepository(AppDbContext db) : IFactoryWorkflowRepository
{
    public Task<Guid> DepotOwnerAsync(Guid depotId, CancellationToken ct) =>
        db.Depots.Where(x => x.Id == depotId).Select(x => x.OwnerId).SingleAsync(ct);

    public async Task<IReadOnlyList<Guid>> NotifiedRecipientsAsync(Guid transportJobId, Guid[] recipients, string title, CancellationToken ct) =>
        await db.Notifications.AsNoTracking().Where(x => recipients.Contains(x.UserId) && x.TransportJobId == transportJobId && x.Title == title)
            .Select(x => x.UserId).ToListAsync(ct);

    public void AddNotification(Notification notice) => db.Notifications.Add(notice);

    public Task<PlatformTransaction?> TransactionAsync(Guid batchId, CancellationToken ct) =>
        db.PlatformTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.SourceType == "BATCH_ORDER" && x.SourceId == batchId, ct);

    public Task<bool> HasFeeAsync(Guid batchId, CancellationToken ct) =>
        db.PlatformTransactions.AnyAsync(x => x.SourceType == "BATCH_ORDER" && x.SourceId == batchId, ct);

    public Task<bool> OwnsBatchAsync(Guid factoryId, Guid batchId, CancellationToken ct) =>
        db.InventoryBatches.AnyAsync(x => x.Id == batchId && x.TargetFactoryId == factoryId, ct);

    public async Task<FactoryWorkflowPage> HistoryAsync(Guid batchId, int page, int pageSize, CancellationToken ct)
    {
        var rows = db.BatchWorkflowEvents.AsNoTracking().Where(x => x.BatchId == batchId);
        var total = await rows.CountAsync(ct);
        var offset = (long)(page - 1) * pageSize;
        var items = offset > int.MaxValue ? [] : await rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((int)offset).Take(pageSize)
            .Select(x => new BatchWorkflowEventDto(x.Id, x.Action, x.FromStatus, x.ToStatus, x.Revision, x.Note, x.CreatedAt, x.PricePerKg, x.NetAmount))
            .ToListAsync(ct);
        return new FactoryWorkflowPage(items, total);
    }

    public Task<bool> HasResolutionAsync(Guid batchId, string status, string note, CancellationToken ct) =>
        db.BatchWorkflowEvents.AnyAsync(x => x.BatchId == batchId && x.Action == "QC_RESOLUTION" && x.ToStatus == status && x.Note == note, ct);

    public void AddEvent(BatchWorkflowEvent workflowEvent) => db.BatchWorkflowEvents.Add(workflowEvent);
    public void AddQualityCheck(BatchQualityCheck check) => db.BatchQualityChecks.Add(check);

    public Task<FactoryDepotPartnership?> PartnershipAsync(Guid factoryId, Guid depotId, CancellationToken ct) =>
        db.FactoryDepotPartnerships.AsNoTracking().SingleOrDefaultAsync(x => x.FactoryId == factoryId && x.DepotId == depotId, ct);

    public void AddPartnership(FactoryDepotPartnership partnership) => db.FactoryDepotPartnerships.Add(partnership);
    public void AddTransport(TransportJob job) => db.TransportJobs.Add(job);
}
