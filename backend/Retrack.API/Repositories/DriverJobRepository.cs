using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Driver;
namespace Retrack.API.Repositories;

public sealed class DriverJobRepository(AppDbContext db) : IDriverJobRepository
{

    private IQueryable<DepotStaff> Staff(Guid userId) => db.DepotStaffs.Where(s => s.UserId == userId
        && s.StaffType == "DRIVER" && s.IsActive && s.User.IsActive && s.User.Role == "DRIVER"
        && s.Depot.Owner.IsActive && s.Depot.Owner.Role == "DEPOT_OWNER");
    public Task<bool> HasActiveStaffAsync(Guid userId, CancellationToken ct) => Staff(userId).AnyAsync(ct);
    internal IQueryable<TransportJob> Scoped(Guid userId)
    {
        var staff = Staff(userId);
        return db.TransportJobs.Where(j => j.Batch.ShippingMethod == "INTERNAL" && staff.Any(s => s.DepotId == j.Batch.DepotId));
    }
    private static IQueryable<TransportJob> Ready(IQueryable<TransportJob> jobs) => jobs.Where(j =>
        (j.Batch.Status == "TRANSPORT_READY" || j.Batch.Status == "ACCEPTED" || j.Batch.Status == "READY_FOR_PICKUP")
        && j.Batch.DeclaredWeightKg > 0 && j.Batch.FactoryReceivedAt == null
        && j.Batch.TargetFactory != null && j.Batch.TargetFactory.Owner != null && j.Batch.TargetFactory.Owner.IsActive);
    internal IQueryable<TransportJob> Pool(Guid userId) => Ready(Scoped(userId)).Where(j => j.DriverId == null && j.Status == "PENDING"
        && !db.DriverDeliveryEvents.Any(e => e.JobId == j.Id && e.DriverId == userId && e.Action == "reject"));
    private static IQueryable<DriverJobDto> Project(IQueryable<TransportJob> jobs, Guid userId) => jobs.Select(j => new DriverJobDto(
        j.Id, j.BatchId, j.Batch.Code, j.Batch.MaterialType, j.Batch.DeclaredWeightKg, j.Status, j.DriverId == userId,
        new(j.Batch.Depot.Name, j.Batch.Depot.Address, j.Batch.Depot.Latitude, j.Batch.Depot.Longitude),
        new(j.Batch.TargetFactory!.Name, j.Batch.TargetFactory.Address, j.Batch.TargetFactory.Latitude, j.Batch.TargetFactory.Longitude), j.CreatedAt));
    public async Task<PagedResult<DriverJobDto>> ListAsync(Guid userId, bool mine, int page, CancellationToken ct)
    {
        var source = Scoped(userId).AsNoTracking();
        source = mine ? source.Where(j => j.DriverId == userId) : Pool(userId).AsNoTracking();
        return new() { Page = page, PageSize = 20, TotalCount = await source.CountAsync(ct),
            Items = await Project(source.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id).Skip((page - 1) * 20).Take(20), userId).ToListAsync(ct) };
    }
    public async Task<DriverJobDto?> DetailAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var source = Scoped(userId).AsNoTracking().Where(j => j.Id == id);
        var ready = Ready(source);
        var visible = source.Where(j => j.DriverId == userId || (j.DriverId == null && j.Status == "PENDING" && ready.Any(r => r.Id == j.Id)));
        return await Project(visible, userId).SingleOrDefaultAsync(ct);
    }

    public Task<DriverJobScope?> ScopeAsync(Guid userId, Guid id, CancellationToken ct) =>
        Scoped(userId).Where(j => j.Id == id).Select(j => new DriverJobScope(j.Batch.DepotId, j.Batch.Depot.OwnerId)).SingleOrDefaultAsync(ct);
    public async Task LockDepotAsync(Guid depotId, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
    }
    public Task<int> ClaimAsync(Guid userId, Guid id, DateTime now, CancellationToken ct) =>
        Ready(Scoped(userId)).Where(j => j.Id == id && j.Status == "PENDING" && j.DriverId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.DriverId, (Guid?)userId).SetProperty(j => j.Status, "ACCEPTED")
                .SetProperty(j => j.UpdatedAt, now), ct);
    public Task<bool> IsAcceptedRetryAsync(Guid userId, Guid id, CancellationToken ct) =>
        Ready(Scoped(userId)).AnyAsync(j => j.Id == id && j.DriverId == userId && j.Status == "ACCEPTED", ct);
}
