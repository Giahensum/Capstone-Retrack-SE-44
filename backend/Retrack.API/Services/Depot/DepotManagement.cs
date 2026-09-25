using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotService
{
    public async Task<DepotProfileDto> GetProfileAsync(Guid ownerId, Guid depotId)
    {
        await RequireOwnerAsync(ownerId, depotId);
        return await db.Depots.AsNoTracking().Where(d => d.Id == depotId).Select(d => new DepotProfileDto(d.Id,
            d.Name, d.Address, d.TaxCode, d.ContactPhone, d.Description, d.Latitude, d.Longitude, d.Rating, d.Owner.Email)).SingleAsync();
    }
    public async Task<DepotProfileDto> UpdateProfileAsync(Guid ownerId, Guid depotId, UpdateDepotProfileDto dto)
    {
        await RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var depot = await db.Depots.SingleAsync(d => d.Id == depotId);
        depot.Name = dto.Name.Trim(); depot.Address = dto.Address.Trim(); depot.TaxCode = dto.TaxCode;
        depot.ContactPhone = dto.ContactPhone; depot.Description = dto.Description;
        depot.Latitude = dto.Latitude; depot.Longitude = dto.Longitude;
        await db.SaveChangesAsync();
        return await GetProfileAsync(ownerId, depotId);
    }
    public async Task<PagedResult<FactoryPartnerDto>> GetFactoriesAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        var source = db.Factories.AsNoTracking().Where(f => f.Owner != null && f.Owner.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(f => f.Name.Contains(query.Search));
        if (!string.IsNullOrEmpty(query.Status))
        {
            if (query.Status is not ("APPROVED" or "PENDING" or "BLOCKED")) throw new ArgumentException("Trạng thái đối tác không hợp lệ.");
            source = source.Where(f => db.FactoryDepotPartnerships.Any(p => p.DepotId == depotId && p.FactoryId == f.Id && p.Status == query.Status));
        }
        return await PageAsync(source.OrderBy(f => f.Name).ThenBy(f => f.Id).Select(f => new FactoryPartnerDto(f.Id,
            f.Name, f.Address, f.Rating, db.FactoryDepotPartnerships.Where(p => p.DepotId == depotId && p.FactoryId == f.Id).Select(p => p.Status).FirstOrDefault())), query);
    }
    public async Task<PagedResult<DemandDto>> GetDemandsAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        var now = DateTime.UtcNow;
        var source = db.FactoryDemands.AsNoTracking().Where(d => d.IsActive && d.Deadline >= now && d.Factory.Owner != null && d.Factory.Owner.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(d => d.MaterialType.Contains(query.Search) || d.Factory.Name.Contains(query.Search));
        return await PageAsync(source.OrderBy(d => d.Deadline).ThenBy(d => d.Id).Select(d => new DemandDto(d.Id,
            d.FactoryId, d.Factory.Name, d.MaterialType, d.RequiredWeightKg, d.MinPricePerKg, d.MaxPricePerKg, d.Deadline)), query);
    }
    internal static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> source, DepotQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentException("Phân trang không hợp lệ.");
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = await source.CountAsync(),
            Items = offset > int.MaxValue ? [] : await source.Skip((int)offset).Take(query.PageSize).ToListAsync() };
    }
}
