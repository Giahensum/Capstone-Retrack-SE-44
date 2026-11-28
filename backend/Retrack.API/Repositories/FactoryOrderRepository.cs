using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryOrderRepository(AppDbContext db) : IFactoryOrderRepository
{
    public Task<InventoryBatch?> LockAsync(Guid id, Guid factoryId, CancellationToken ct) => LockAsync(id, factoryId, false, ct);
    public Task<InventoryBatch?> LockForReceivingAsync(Guid id, Guid factoryId, CancellationToken ct) => LockAsync(id, factoryId, true, ct);

    public async Task<InventoryBatch?> LockMarketAsync(Guid id, bool includeTransport, CancellationToken ct)
    {
        var depotId = await db.InventoryBatches.AsNoTracking().Where(x => x.Id == id)
            .Select(x => (Guid?)x.DepotId).SingleOrDefaultAsync(ct);
        if (depotId is null) return null;
        if (db.Database.IsRelational())
        {
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").LoadAsync(ct);
            await db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} FOR UPDATE").LoadAsync(ct);
        }
        var source = db.InventoryBatches.Include(x => x.Depot);
        var batch = includeTransport
            ? await source.Include(x => x.TransportJob).SingleOrDefaultAsync(x => x.Id == id, ct)
            : await source.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (batch is not null) await db.Entry(batch).ReloadAsync(ct);
        return batch;
    }

    private async Task<InventoryBatch?> LockAsync(Guid id, Guid factoryId, bool includeTransport, CancellationToken ct)
    {
        // Cùng thứ tự khóa với Depot/Driver: lấy mã kho, khóa kho, khóa lô rồi nạp lại state.
        var depotId = await db.InventoryBatches.AsNoTracking().Where(x => x.Id == id && x.TargetFactoryId == factoryId)
            .Select(x => (Guid?)x.DepotId).SingleOrDefaultAsync(ct);
        if (depotId is null) return null;
        if (db.Database.IsRelational())
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").LoadAsync(ct);
        var query = db.Database.IsRelational()
            ? db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} AND target_factory_id = {factoryId} FOR UPDATE")
            : db.InventoryBatches.Where(x => x.Id == id && x.TargetFactoryId == factoryId);
        var batch = includeTransport
            ? await query.Include(x => x.TransportJob).Include(x => x.QualityCheck).SingleOrDefaultAsync(ct)
            : await query.Include(x => x.QualityCheck).SingleOrDefaultAsync(ct);
        if (batch is null) return null;
        await db.Entry(batch).ReloadAsync(ct);
        if (batch.QualityCheck is not null) await db.Entry(batch.QualityCheck).ReloadAsync(ct);
        if (includeTransport && batch.TransportJob is not null) await db.Entry(batch.TransportJob).ReloadAsync(ct);
        return batch;
    }
}
