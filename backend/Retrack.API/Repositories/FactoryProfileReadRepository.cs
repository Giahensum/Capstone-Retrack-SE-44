using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Repositories;

public sealed class FactoryProfileReadRepository(AppDbContext db) : IFactoryProfileReadRepository
{
    public async Task<FactoryProfileSnapshot> GetAsync(Guid factoryId, CancellationToken ct)
    {
        var factory = await db.Factories.Include(x => x.Owner).AsNoTracking().SingleAsync(x => x.Id == factoryId, ct);
        var feeConfig = await db.SystemConfigs.AsNoTracking().Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE")
            .Select(x => x.ConfigValue).FirstOrDefaultAsync(ct);
        return new FactoryProfileSnapshot(factory, feeConfig, await BusinessModel.IsSubscriptionAsync(db, ct));
    }
}
