using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DriverNoticeRepository(AppDbContext db) : IDriverNoticeRepository
{
    public async Task<object> ListAsync(Guid userId, int page, CancellationToken ct)
    {
        var source = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        return new { page, pageSize = 20, totalCount = await source.CountAsync(ct),
            unreadCount = await source.CountAsync(n => !n.IsRead, ct),
            items = await source.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip((page - 1) * 20).Take(20)
                .Select(n => new { n.Id, n.Title, n.Message, n.IsRead, n.CreatedAt, jobId = n.TransportJobId }).ToListAsync(ct) };
    }
    public Task<int> MarkReadAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    public async Task<object> LegacyTripsAsync(Guid userId, CancellationToken ct) =>
        await db.TransportJobs.AsNoTracking()
            .Where(x => x.Batch.ShippingMethod == "INTERNAL" && (x.DriverId == userId || (x.DriverId == null && db.DepotStaffs.Any(s => s.UserId == userId && s.IsActive && s.DepotId == x.Batch.DepotId && s.StaffType == "DRIVER"))))
            .Include(x => x.Batch).ThenInclude(x => x.Depot)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100)
            .Select(x => new { x.Id, batchId = x.BatchId, x.Status, x.DriverId, materialType = x.Batch.MaterialType,
                weightKg = x.Batch.DeclaredWeightKg, depotName = x.Batch.Depot.Name, depotAddress = x.Batch.Depot.Address,
                x.Batch.TargetFactoryId, x.CheckinDepotImageUrl, x.CheckoutFactoryImageUrl, x.CreatedAt, x.UpdatedAt }).ToListAsync(ct);
}
