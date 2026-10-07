using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

public sealed class DepotOwnerRepository(AppDbContext db) : IDepotOwnerRepository
{
    public Task<bool> IsActiveOwnerAsync(Guid ownerId) => db.Users.AnyAsync(u => u.Id == ownerId && u.IsActive && u.Role == "DEPOT_OWNER");
    public Task<bool> OwnsAsync(Guid ownerId, Guid depotId) => db.Depots.AnyAsync(d => d.Id == depotId && d.OwnerId == ownerId);
    public Task<List<DepotSummaryDto>> ListAsync(Guid ownerId) => db.Depots.AsNoTracking().Where(d => d.OwnerId == ownerId)
        .OrderBy(d => d.Name).ThenBy(d => d.Id).Select(d => new DepotSummaryDto(d.Id, d.Name)).ToListAsync();
    public Task<Models.Depot> FindAsync(Guid depotId) => db.Depots.SingleAsync(d => d.Id == depotId);
    public async Task SaveAsync() => await db.SaveChangesAsync();
    public async Task<DepotProfileDto> ProfileAsync(Guid depotId)
    {
        return await db.Depots.AsNoTracking().Where(d => d.Id == depotId).Select(d => new DepotProfileDto(d.Id,
            d.Name, d.Address, d.TaxCode, d.ContactPhone, d.Description, d.Latitude, d.Longitude, d.Rating, d.Owner.Email)).SingleAsync();
    }
    public async Task<PagedResult<FactoryPartnerDto>> FactoriesAsync(Guid depotId, FactorySearchQuery query)
    {
        var source = db.Factories.AsNoTracking().Where(f => f.Owner != null && f.Owner.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(f => EF.Functions.ILike(f.Name, $"%{query.Search}%"));
        if (!string.IsNullOrEmpty(query.Status))
        {
            source = source.Where(f => db.FactoryDepotPartnerships.Any(p => p.DepotId == depotId && p.FactoryId == f.Id && (p.Status == "BLOCKED" || p.BlockedByDepot || p.BlockedByFactory ? "BLOCKED" : p.Status) == query.Status));
        }
        var depot = await db.Depots.AsNoTracking().Where(d => d.Id == depotId)
            .Select(d => new { d.Latitude, d.Longitude }).SingleAsync();
        if ((query.MaxDistanceKm.HasValue || query.NearestFirst) && (!depot.Latitude.HasValue || !depot.Longitude.HasValue))
            throw new ArgumentException("Cần cập nhật tọa độ kho trước khi lọc khoảng cách.");
        var latitude = depot.Latitude.HasValue ? (double)depot.Latitude.Value * Math.PI / 180.0 : 0;
        var longitude = depot.Longitude.HasValue ? (double)depot.Longitude.Value : 0;
        // Khoảng cách đường chim bay chỉ để gợi ý; tính trong SQL trước khi phân trang.
        var withDistance = source.Select(f => new {
            Factory = f,
            DistanceKm = depot.Latitude.HasValue && depot.Longitude.HasValue && f.Latitude.HasValue && f.Longitude.HasValue
                ? (double?)(6371.0088 * Math.Acos(Math.Min(1.0, Math.Max(-1.0,
                    Math.Sin(latitude) * Math.Sin((double)f.Latitude!.Value * Math.PI / 180.0) +
                    Math.Cos(latitude) * Math.Cos((double)f.Latitude!.Value * Math.PI / 180.0) *
                    Math.Cos(((double)f.Longitude!.Value - longitude) * Math.PI / 180.0)))))
                : null
        });
        if (query.MaxDistanceKm.HasValue)
            withDistance = withDistance.Where(x => x.DistanceKm <= query.MaxDistanceKm.Value);
        var ordered = query.NearestFirst
            ? withDistance.OrderBy(x => x.DistanceKm == null).ThenBy(x => x.DistanceKm).ThenBy(x => x.Factory.Name).ThenBy(x => x.Factory.Id)
            : withDistance.OrderBy(x => x.Factory.Name).ThenBy(x => x.Factory.Id);
        return await DepotRepositoryPage.ReadAsync(ordered.Select(x => new FactoryPartnerDto(x.Factory.Id,
            x.Factory.Name, x.Factory.Address, x.Factory.Rating,
            db.FactoryDepotPartnerships.Where(p => p.DepotId == depotId && p.FactoryId == x.Factory.Id)
                .Select(p => p.Status == "BLOCKED" || p.BlockedByDepot || p.BlockedByFactory ? "BLOCKED" : p.Status).FirstOrDefault(),
            x.Factory.AcceptedMaterialsCsv, x.DistanceKm)), query);
    }
    public async Task<PagedResult<DemandDto>> DemandsAsync(Guid depotId, DepotQuery query, DateTime now)
    {
        var source = db.FactoryDemands.AsNoTracking().Where(d => d.IsActive && d.Deadline >= now && d.Factory.Owner != null && d.Factory.Owner.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(d => EF.Functions.ILike(d.MaterialType, $"%{query.Search}%") || EF.Functions.ILike(d.Factory.Name, $"%{query.Search}%"));
        return await DepotRepositoryPage.ReadAsync(source.OrderBy(d => d.Deadline).ThenBy(d => d.Id).Select(d => new DemandDto(d.Id,
            d.FactoryId, d.Factory.Name, d.MaterialType, d.RequiredWeightKg, d.MinPricePerKg, d.MaxPricePerKg, d.Deadline,
            db.FactoryDepotPartnerships.Any(p => p.DepotId == depotId && p.FactoryId == d.FactoryId &&
                (p.Status == "BLOCKED" || p.BlockedByDepot || p.BlockedByFactory)))), query);
    }
}
