using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Driver;

namespace Retrack.API.Repositories;

public sealed class DriverDeliveryRepository(AppDbContext db) : IDriverDeliveryRepository
{
    public Task<List<DeliveryEventDto>> EventsAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.DriverDeliveryEvents.AsNoTracking().Where(e => e.JobId == id && e.DriverId == userId)
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Take(50)
            .Select(e => new DeliveryEventDto(e.Action, e.Reason, e.ImageUrl, e.DistanceMeters, e.AccuracyMeters, e.CreatedAt)).ToListAsync(ct);
    public Task<Guid?> DepotIdAsync(Guid id, CancellationToken ct) =>
        db.TransportJobs.Where(j => j.Id == id).Select(j => (Guid?)j.Batch.DepotId).SingleOrDefaultAsync(ct);
    public Task<bool> HasActiveStaffAsync(Guid userId, Guid depotId, CancellationToken ct) =>
        db.DepotStaffs.AnyAsync(s => s.UserId == userId && s.DepotId == depotId && s.IsActive
            && s.StaffType == "DRIVER" && s.User.IsActive && s.User.Role == "DRIVER"
            && s.Depot.Owner.IsActive && s.Depot.Owner.Role == "DEPOT_OWNER", ct);
    public async Task<TransportJob> LockJobAsync(Guid id, CancellationToken ct)
    {
        var batchId = await db.TransportJobs.Where(j => j.Id == id).Select(j => j.BatchId).SingleAsync(ct);
        if (db.Database.IsNpgsql())
        {
            await db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {batchId} FOR UPDATE").LoadAsync(ct);
            await db.TransportJobs.FromSqlInterpolated($"SELECT * FROM transport_jobs WHERE id = {id} FOR UPDATE").LoadAsync(ct);
        }
        var job = await db.TransportJobs.Include(j => j.Batch).ThenInclude(b => b.Depot)
            .Include(j => j.Batch).ThenInclude(b => b.TargetFactory).ThenInclude(f => f!.Owner)
            .SingleAsync(j => j.Id == id, ct);
        await db.Entry(job).ReloadAsync(ct);
        await db.Entry(job.Batch).ReloadAsync(ct);
        return job;
    }
    public Task<DriverDeliveryEvent?> FindOperationAsync(Guid userId, Guid id, Guid operationId, CancellationToken ct) =>
        db.DriverDeliveryEvents.AsNoTracking().SingleOrDefaultAsync(e => e.JobId == id
            && e.DriverId == userId && e.OperationId == operationId, ct);
    public Task<bool> HasRejectionAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.DriverDeliveryEvents.AnyAsync(e => e.JobId == id && e.DriverId == userId && e.Action == "reject", ct);
    public async Task QueueAvailableAsync(Guid depotId, Guid id, CancellationToken ct)
    {
        var recipients = await db.DepotStaffs.Where(s => s.DepotId == depotId && s.StaffType == "DRIVER"
            && s.IsActive && s.User.IsActive && s.User.Role == "DRIVER" && s.Depot.Owner.IsActive)
            .Select(s => s.UserId).Distinct().ToListAsync(ct);
        db.Notifications.AddRange(recipients.Select(userId => new Notification { UserId = userId, TransportJobId = id,
            Title = "Có chuyến vận chuyển mới", Message = "Kho có chuyến mới chờ nhận. Mở chi tiết để kiểm tra tình trạng hiện tại." }));
    }
    public void AddEvent(DriverDeliveryEvent entry) => db.DriverDeliveryEvents.Add(entry);
    public void AddNotification(Notification notification) => db.Notifications.Add(notification);
}
