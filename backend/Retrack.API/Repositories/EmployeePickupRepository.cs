using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class EmployeePickupRepository(AppDbContext db) : IEmployeePickupRepository
{
    private IQueryable<DepotStaff> Staff(Guid id) => db.DepotStaffs.Where(s => s.UserId == id
        && s.IsActive && s.StaffType == "DEPOT_EMPLOYEE" && s.User.IsActive
        && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive);
    private IQueryable<Guid> ActiveDepotIds(Guid userId) => Staff(userId).Select(s => s.DepotId);
    public Task<Guid?> PrimaryDepotAsync(Guid userId, CancellationToken ct) =>
        Staff(userId).AsNoTracking().OrderBy(s => s.Id).Select(s => (Guid?)s.DepotId).FirstOrDefaultAsync(ct);
    public Task<bool> HasAreasAsync(Guid depotId, CancellationToken ct) =>
        db.EmployeeCollectionAreas.AnyAsync(a => a.DepotId == depotId, ct);
    public Task<bool> IsApprovedAsync(Guid depotId, CancellationToken ct) =>
        db.Depots.AnyAsync(d => d.Id == depotId && d.VerificationStatus == "APPROVED", ct);
    public Task<DepotCoordinates?> CoordinatesAsync(Guid depotId, CancellationToken ct) =>
        db.Depots.AsNoTracking().Where(d => d.Id == depotId)
            .Select(d => new DepotCoordinates(d.Latitude, d.Longitude)).FirstOrDefaultAsync(ct);
    public async Task<(List<PickupPoolItemDto> Items, int Total)> AssignedAsync(Guid userId, int page, CancellationToken ct)
    {
        var depots = ActiveDepotIds(userId);
        var own = db.PickupRequests.AsNoTracking().Where(p => p.AcceptedCollectorId == userId
            && p.TargetDepotId != null && depots.Contains(p.TargetDepotId.Value) && p.Status == "SCHEDULED");
        var items = await own.OrderBy(p => p.PreferredDatetime ?? p.CreatedAt).ThenBy(p => p.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(p => new PickupPoolItemDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address,
                p.Latitude, p.Longitude, p.PreferredDatetime, p.Description, p.RequestImageUrl, p.CreatedAt)).ToListAsync(ct);
        return (items, await own.CountAsync(ct));
    }
    public Task<List<PickupPoolCandidate>> ManualPoolAsync(Guid depotId, CancellationToken ct) =>
        db.PickupRequests.AsNoTracking()
            .Where(p => (p.TargetDepotId == depotId || p.TargetDepotId == null) && p.Status == "PENDING" && p.AcceptedCollectorId == null)
            .OrderBy(p => p.PreferredDatetime == null).ThenBy(p => p.PreferredDatetime).ThenBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => new PickupPoolCandidate(new PickupPoolItemDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address,
                p.Latitude, p.Longitude, p.PreferredDatetime, p.Description, p.RequestImageUrl, p.CreatedAt),
                p.TargetDepotId, p.Latitude, p.Longitude)).ToListAsync(ct);
    public async Task<object> ActiveAsync(Guid userId, int page, CancellationToken ct)
    {
        var depots = ActiveDepotIds(userId);
        var query = db.PickupRequests.AsNoTracking().Where(p => p.TargetDepotId != null && depots.Contains(p.TargetDepotId.Value)
            && p.AcceptedCollectorId == userId && (p.Status == "SCHEDULED" || p.Status == "IN_PROGRESS" || p.Status == "SELLER_CONFIRMED"));
        var items = await query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Skip((page - 1) * 20).Take(20)
            .Select(p => new ActivePickupSummaryDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address, p.Status, p.PreferredDatetime)).ToListAsync(ct);
        return new { items, page, pageSize = 20, total = await query.CountAsync(ct) };
    }
    public Task<PickupDetailCandidate?> DetailAsync(Guid userId, Guid depotId, Guid pickupId, CancellationToken ct)
    {
        var depots = ActiveDepotIds(userId);
        return db.PickupRequests.AsNoTracking()
            .Where(p => p.Id == pickupId && ((p.TargetDepotId != null && depots.Contains(p.TargetDepotId.Value)) || p.TargetDepotId == null)
                && ((p.Status == "PENDING" && p.AcceptedCollectorId == null
                    && !db.EmployeeCollectionAreas.Any(a => a.DepotId == depotId)) || p.AcceptedCollectorId == userId))
            .Select(p => new PickupDetailCandidate(new EmployeePickupDetailDto(p.Id, p.Seller.FullName, p.Seller.Phone, p.Address,
                p.Latitude, p.Longitude, p.PreferredDatetime, p.Description, p.RequestImageUrl, p.CreatedAt,
                p.Status, p.AcceptedCollectorId == userId), p.TargetDepotId, p.Latitude, p.Longitude)).FirstOrDefaultAsync(ct);
    }
    public Task<PickupRequest?> FindAvailableAsync(Guid depotId, Guid pickupId, CancellationToken ct) =>
        db.PickupRequests.AsNoTracking().Include(p => p.Seller)
            .SingleOrDefaultAsync(p => p.Id == pickupId && (p.TargetDepotId == depotId || p.TargetDepotId == null), ct);
    public Task<int> ClaimAsync(Guid userId, Guid depotId, Guid pickupId, DateTime now, CancellationToken ct) =>
        db.PickupRequests.Where(p => p.Id == pickupId && (p.TargetDepotId == depotId || p.TargetDepotId == null)
            && p.Status == "PENDING" && p.AcceptedCollectorId == null
            && db.Depots.Any(d => d.Id == depotId && d.VerificationStatus == "APPROVED"))
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.AcceptedCollectorId, (Guid?)userId)
                .SetProperty(p => p.TargetDepotId, depotId).SetProperty(p => p.Status, "SCHEDULED")
                .SetProperty(p => p.UpdatedAt, now), ct);
}
