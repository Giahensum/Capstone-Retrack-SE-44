using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Models;

namespace Retrack.API.Repositories;

public sealed class FactoryDemandRepository(AppDbContext db) : IFactoryDemandRepository
{
    public Task<FactoryDemand?> FindAsync(Guid factoryId, Guid demandId, CancellationToken ct) =>
        db.FactoryDemands.SingleOrDefaultAsync(x => x.Id == demandId && x.FactoryId == factoryId, ct);

    public Task<bool> HasBatchAsync(Guid demandId, CancellationToken ct) =>
        db.InventoryBatches.AnyAsync(x => x.SourceDemandId == demandId, ct);

    public void Add(FactoryDemand demand) => db.FactoryDemands.Add(demand);
    public void Remove(FactoryDemand demand) => db.FactoryDemands.Remove(demand);
    public void ResetTrackedDelete(FactoryDemand demand) => db.Entry(demand).State = EntityState.Unchanged;

    public async Task<FactoryDemandPage> ListAsync(Guid factoryId, string[]? materialValues, int page, int pageSize, CancellationToken ct)
    {
        var source = db.FactoryDemands.AsNoTracking().Where(x => x.FactoryId == factoryId);
        if (materialValues is not null) source = source.Where(x => materialValues.Contains(x.MaterialType));
        var count = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new FactoryDemandPage(items, count);
    }
}
