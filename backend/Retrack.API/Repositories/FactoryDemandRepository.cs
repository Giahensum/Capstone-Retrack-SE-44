using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryDemandRepository(AppDbContext db) : IFactoryDemandRepository
{
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
