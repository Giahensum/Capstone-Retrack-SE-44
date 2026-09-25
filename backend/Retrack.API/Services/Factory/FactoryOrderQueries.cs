using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Factory;

public static class FactoryOrderQueries
{
    public static IQueryable<InventoryBatch> ForFactory(AppDbContext db, Guid factoryId) => db.InventoryBatches
        .AsNoTracking()
        .Where(x => x.TargetFactoryId == factoryId && x.Status != "MARKETPLACE" && x.Status != "DRAFT")
        .Include(x => x.Depot).ThenInclude(x => x.Owner)
        .Include(x => x.TransportJob)
        .Include(x => x.QualityCheck);
}
