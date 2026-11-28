using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

// Một repository scoped dùng cùng DbContext với các repository Depot/Driver trong transaction.
public sealed class ShippingRepository(AppDbContext db) : IShippingRepository
{
    private sealed class Transaction(IDbContextTransaction tx) : IShippingTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
    public async Task<IShippingTransaction> BeginAsync(CancellationToken ct) => new Transaction(await db.Database.BeginTransactionAsync(ct));
    public async Task LockDepotAsync(Guid id, CancellationToken ct)
    {
        if(db.Database.IsNpgsql()) await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id={id} FOR UPDATE").LoadAsync(ct);
    }
    public Task<InventoryBatch?> FindBatchAsync(Guid depot, Guid id, CancellationToken ct) => db.InventoryBatches.Include(b=>b.TransportJob).SingleOrDefaultAsync(b=>b.Id==id && b.DepotId==depot,ct);
    public Task<InventoryBatch> FindBatchAsync(Guid id, CancellationToken ct) => db.InventoryBatches.Include(b=>b.TransportJob).SingleAsync(b=>b.Id==id,ct);
    public Task<Depot> FindDepotAsync(Guid id,CancellationToken ct) => db.Depots.SingleAsync(d=>d.Id==id,ct);
    public Task<Factory?> FindActiveFactoryAsync(Guid id,CancellationToken ct) => db.Factories.SingleOrDefaultAsync(f=>f.Id==id && f.Owner!=null && f.Owner.IsActive && f.Owner.Role=="FACTORY",ct);
    public Task<ShippingQuote?> FindQuoteAsync(Guid owner,Guid depot,Guid id,CancellationToken ct) => db.ShippingQuotes.SingleOrDefaultAsync(q=>q.Id==id && q.OwnerId==owner && q.DepotId==depot,ct);
    public Task<ShippingQuote?> ReadQuoteAsync(Guid id,CancellationToken ct) => db.ShippingQuotes.AsNoTracking().SingleOrDefaultAsync(q=>q.Id==id,ct);
    public Task<ExternalShipment?> ReadLatestAsync(Guid batch,CancellationToken ct) => db.ExternalShipments.AsNoTracking().Where(s=>s.BatchId==batch).OrderByDescending(s=>s.CreatedAt).ThenByDescending(s=>s.Id).FirstOrDefaultAsync(ct);
    public Task<ExternalShipment?> ReadRecoveryAsync(Guid batch,CancellationToken ct) => db.ExternalShipments.AsNoTracking().Where(s=>s.BatchId==batch).OrderByDescending(s=>s.CreatedAt).FirstOrDefaultAsync(ct);
    public Task<ExternalShipment?> FindLatestAsync(Guid batch,CancellationToken ct) => db.ExternalShipments.Where(s=>s.BatchId==batch).OrderByDescending(s=>s.CreatedAt).FirstOrDefaultAsync(ct);
    public Task<List<ShippingEventDto>> EventsAsync(Guid id,CancellationToken ct) => db.ShippingEvents.AsNoTracking().Where(e=>e.ShipmentId==id).OrderByDescending(e=>e.CreatedAt).ThenBy(e=>e.Id).Take(50).Select(e=>new ShippingEventDto(e.Status,e.Source,e.CreatedAt)).ToListAsync(ct);
    public Task<bool> HasOpenShipmentAsync(Guid batch,string[] terminal,CancellationToken ct) => db.ExternalShipments.AnyAsync(s=>s.BatchId==batch && !terminal.Contains(s.Status),ct);
    public Task<bool> HasUsedQuoteAsync(Guid id,CancellationToken ct) => db.ExternalShipments.AnyAsync(s=>s.QuoteId==id,ct);
    public Task<ExternalShipment?> FindOperationAsync(Guid id,CancellationToken ct) => db.ExternalShipments.SingleOrDefaultAsync(s=>s.OperationId==id,ct);
    public Task<ExternalShipment> FindShipmentAsync(Guid id,CancellationToken ct) => db.ExternalShipments.SingleAsync(s=>s.Id==id,ct);
    public Task<ExternalShipment> ReadShipmentAsync(Guid id,CancellationToken ct) => db.ExternalShipments.AsNoTracking().SingleAsync(s=>s.Id==id,ct);
    public async Task<Guid> DepotForShipmentAsync(Guid id,CancellationToken ct)
    {
        var batch=await db.ExternalShipments.Where(s=>s.Id==id).Select(s=>s.BatchId).SingleAsync(ct);
        return await db.InventoryBatches.Where(b=>b.Id==batch).Select(b=>b.DepotId).SingleAsync(ct);
    }
    public void AddQuote(ShippingQuote quote) => db.ShippingQuotes.Add(quote);
    public void AddShipment(ExternalShipment shipment) => db.ExternalShipments.Add(shipment);
    public void AddTransport(TransportJob job) => db.TransportJobs.Add(job);
    public void AddEvent(ShippingEvent entry) => db.ShippingEvents.Add(entry);
    public void ClearTracking() => db.ChangeTracker.Clear();
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task TouchSandboxWebhookAsync(string orderId,CancellationToken ct) =>
        await db.ExternalShipments.Where(s=>s.Provider=="LALAMOVE_SANDBOX" && s.ProviderOrderId==orderId && (s.LastWebhookAt==null || s.LastWebhookAt<DateTime.UtcNow.AddSeconds(-25)))
            .ExecuteUpdateAsync(u=>u.SetProperty(s=>s.LastWebhookAt,DateTime.UtcNow),ct);
    public async Task MarkStaleBookingsAsync(DateTime before,string message,CancellationToken ct) =>
        await db.ExternalShipments.Where(s=>s.Status=="BOOKING" && s.CreatedAt<before).ExecuteUpdateAsync(u=>u.SetProperty(s=>s.Status,"UNKNOWN").SetProperty(s=>s.LastError,message),ct);
    public Task<List<Guid>> ReconciliationIdsAsync(string provider,DateTime cutoff,CancellationToken ct) =>
        db.ExternalShipments.AsNoTracking().Where(s=>s.Provider==provider && s.ProviderOrderId!=null && (s.Status=="ASSIGNING_DRIVER"||s.Status=="ON_GOING"||s.Status=="PICKED_UP"||s.Status=="CANCEL_REQUESTED") && s.LastCheckedAt<cutoff)
            .OrderBy(s=>s.LastCheckedAt).ThenBy(s=>s.Id).Take(5).Select(s=>s.Id).ToListAsync(ct);
}
