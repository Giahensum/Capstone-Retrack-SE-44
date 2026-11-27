using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class EmployeeCollectionRepository(AppDbContext db) : IEmployeeCollectionRepository
{
    public Task<bool> CanStartShiftAsync(Guid userId,Guid depotId,DateTime now,CancellationToken ct) =>
        db.EmployeeDispatchShifts.AnyAsync(s=>s.EmployeeId==userId && s.Availability=="AVAILABLE" && s.StartsAt<=now && s.EndsAt>now &&
            db.EmployeeCollectionAreas.Any(a=>a.Id==s.AreaId && a.DepotId==depotId && a.IsActive),ct);
    public Task<bool> HasActiveStaffAsync(Guid userId, Guid? depotId, CancellationToken ct) =>
        db.DepotStaffs.AnyAsync(s => s.UserId == userId && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE"
            && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive
            && (depotId == null || s.DepotId == depotId), ct);

    public async Task<PickupRequest?> FindPickupAsync(Guid id, bool locked, CancellationToken ct)
    {
        var query = locked && db.Database.IsNpgsql()
            ? db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {id} FOR UPDATE")
            : db.PickupRequests.Where(p => p.Id == id);
        var pickup = await query.SingleOrDefaultAsync(ct);
        if (locked && pickup != null) await db.Entry(pickup).ReloadAsync(ct);
        return pickup;
    }
    public async Task<PickupCheckIn?> FindCheckInAsync(Guid id, CancellationToken ct) =>
        await db.PickupCheckIns.FindAsync([id], ct);
    public Task<PickupCheckIn?> ReadCheckInAsync(Guid id, CancellationToken ct) =>
        db.PickupCheckIns.AsNoTracking().SingleOrDefaultAsync(c => c.PickupRequestId == id, ct);
    public Task<List<PickupRequestItem>> ItemsAsync(Guid id, CancellationToken ct) =>
        db.PickupRequestItems.Where(i => i.PickupRequestId == id).ToListAsync(ct);
    public Task<List<ClassificationItemDto>> ReadItemsAsync(Guid id, CancellationToken ct) =>
        db.PickupRequestItems.AsNoTracking().Where(i => i.PickupRequestId == id)
            .OrderBy(i => i.MaterialType).ThenBy(i => i.Id)
            .Select(i => new ClassificationItemDto(i.Id, i.MaterialType, i.WeightKg, i.PricePerKg, i.SubTotal)).ToListAsync(ct);
    public Task<bool> HasEventAsync(Guid id, string action, int revision, CancellationToken ct) =>
        db.EmployeeCollectionEvents.AnyAsync(e => e.PickupRequestId == id && e.Kind == action && e.Revision == revision, ct);
    public Task<EmployeeCollectionEvent?> SubmittedAsync(Guid id, int revision, CancellationToken ct) =>
        db.EmployeeCollectionEvents.AsNoTracking().SingleOrDefaultAsync(e => e.PickupRequestId == id
            && e.Kind == "SUBMITTED" && e.Revision == revision, ct);
    public Task<bool> ActiveSellerAsync(Guid sellerId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == sellerId && u.IsActive && u.Role == "SELLER", ct);
    public Task<Guid> OwnerIdAsync(Guid? depotId, CancellationToken ct) =>
        db.Depots.Where(d => d.Id == depotId).Select(d => d.OwnerId).SingleAsync(ct);
    public Task<MarketPrice?> LatestPriceAsync(MaterialType type, DateTime now, CancellationToken ct) =>
        db.MarketPrices.AsNoTracking().Where(p => p.MaterialType == type && p.EffectiveDate <= now && p.PricePerKg > 0)
            .OrderByDescending(p => p.EffectiveDate).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id).FirstOrDefaultAsync(ct);
    public void AddCheckIn(PickupCheckIn evidence) => db.PickupCheckIns.Add(evidence);
    public void ReplaceItems(IEnumerable<PickupRequestItem> previous, IEnumerable<PickupRequestItem> items)
    {
        db.PickupRequestItems.RemoveRange(previous);
        db.PickupRequestItems.AddRange(items);
    }
    public void AddEvent(EmployeeCollectionEvent entry) => db.EmployeeCollectionEvents.Add(entry);
    public void AddNotification(Notification notification) => db.Notifications.Add(notification);
}
