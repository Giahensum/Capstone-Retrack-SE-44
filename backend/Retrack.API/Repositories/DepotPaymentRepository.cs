using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotPaymentRepository(AppDbContext db) : IDepotPaymentRepository
{
    public async Task<PickupRequest?> LockAsync(Guid requestId)
    {
        var request = await db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {requestId} FOR UPDATE").SingleOrDefaultAsync();
        if (request is null) return null;
        // Context có thể đã theo dõi bản cũ trước lúc chờ khóa.
        await db.Entry(request).ReloadAsync();
        return await db.PickupRequests.Include(p => p.Seller).Include(p => p.TargetDepot).Include(p => p.Items)
            .SingleAsync(p => p.Id == requestId);
    }
    public void AddFee(PlatformTransaction fee) => db.PlatformTransactions.Add(fee);
}
