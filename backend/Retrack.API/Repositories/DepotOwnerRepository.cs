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
    public async Task<PagedResult<FactoryPartnerDto>> FactoriesAsync(Guid depotId, DepotQuery query)
    {
        var source = db.Factories.AsNoTracking().Where(f => f.Owner != null && f.Owner.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(f => f.Name.Contains(query.Search));
        if (!string.IsNullOrEmpty(query.Status))
        {
            source = source.Where(f => db.FactoryDepotPartnerships.Any(p => p.DepotId == depotId && p.FactoryId == f.Id && p.Status == query.Status));
        }
        return await DepotRepositoryPage.ReadAsync(source.OrderBy(f => f.Name).ThenBy(f => f.Id).Select(f => new FactoryPartnerDto(f.Id,
            f.Name, f.Address, f.Rating, db.FactoryDepotPartnerships.Where(p => p.DepotId == depotId && p.FactoryId == f.Id).Select(p => p.Status).FirstOrDefault())), query);
    }
    public async Task<PagedResult<DemandDto>> DemandsAsync(DepotQuery query, DateTime now)
    {
        var source = db.FactoryDemands.AsNoTracking().Where(d => d.IsActive && d.Deadline >= now && d.Factory.Owner != null && d.Factory.Owner.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(d => d.MaterialType.Contains(query.Search) || d.Factory.Name.Contains(query.Search));
        return await DepotRepositoryPage.ReadAsync(source.OrderBy(d => d.Deadline).ThenBy(d => d.Id).Select(d => new DemandDto(d.Id,
            d.FactoryId, d.Factory.Name, d.MaterialType, d.RequiredWeightKg, d.MinPricePerKg, d.MaxPricePerKg, d.Deadline)), query);
    }
}
