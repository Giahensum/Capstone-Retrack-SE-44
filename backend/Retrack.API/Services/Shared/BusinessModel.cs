using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.API.Services.Shared;

public static class BusinessModel
{
    public static Task<bool> IsSubscriptionAsync(AppDbContext db, CancellationToken ct = default) => db.SystemConfigs.AsNoTracking()
        .AnyAsync(x => x.ConfigKey == "BUSINESS_MODEL" && x.ConfigValue == "DEPOT_SUBSCRIPTION", ct);
}
