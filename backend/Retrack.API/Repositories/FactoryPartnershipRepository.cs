using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryPartnershipRepository(AppDbContext db) : IFactoryPartnershipRepository
{
    public async Task<FactoryDepotPartnership?> LockAsync(Guid factoryId, Guid depotId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
        var partner = await FindAsync(factoryId, depotId, ct);
        if (partner is not null) await db.Entry(partner).ReloadAsync(ct);
        return partner;
    }

    public Task<FactoryDepotPartnership?> FindAsync(Guid factoryId, Guid depotId, CancellationToken ct) =>
        db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factoryId && x.DepotId == depotId, ct);

    public Task<bool> HasInspectedBatchAsync(Guid factoryId, Guid depotId, CancellationToken ct) =>
        db.InventoryBatches.AnyAsync(b => b.DepotId == depotId && b.TargetFactoryId == factoryId &&
            b.QualityCheck != null && (b.QualityCheck.IsAccepted || b.QualityCheck.Grade != "" && b.QualityCheck.Grade != "PENDING"), ct);

    public Task<bool> HasOpenOrdersAsync(Guid factoryId, Guid depotId, CancellationToken ct) =>
        db.InventoryBatches.AnyAsync(x => x.TargetFactoryId == factoryId && x.DepotId == depotId &&
            (x.Status != "COMPLETED" || x.SettlementWorkflowVersion > 0) && x.Status != "PAID" && x.Status != "CANCELLED" && x.Status != "RETURNED" &&
            (x.Status != "REJECTED" || x.QualityCheck != null || x.FactoryReceivedAt != null), ct);

    public Task<InventoryBatch?> FindOwnedBatchAsync(Guid factoryId, Guid batchId, CancellationToken ct) =>
        db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == batchId && x.TargetFactoryId == factoryId, ct);

    public Task<FactoryDepotReview?> FindReviewAsync(Guid factoryId, Guid batchId, CancellationToken ct) =>
        db.FactoryDepotReviews.SingleOrDefaultAsync(x => x.BatchId == batchId && x.FactoryId == factoryId, ct);

    public void AddReview(FactoryDepotReview review) => db.FactoryDepotReviews.Add(review);
    public void AddAudit(AuditLog log) => db.AuditLogs.Add(log);
}
