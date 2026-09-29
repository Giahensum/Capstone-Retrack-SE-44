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
        return await owners.ProfileAsync(depotId);
    }
    public async Task<DepotProfileDto> UpdateProfileAsync(Guid ownerId, Guid depotId, UpdateDepotProfileDto dto)
    {
        await RequireOwnerAsync(ownerId, depotId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        var depot = await owners.FindAsync(depotId);
        depot.Name = dto.Name.Trim(); depot.Address = dto.Address.Trim(); depot.TaxCode = dto.TaxCode;
        depot.ContactPhone = dto.ContactPhone; depot.Description = dto.Description;
        depot.Latitude = dto.Latitude; depot.Longitude = dto.Longitude;
        await owners.SaveAsync();
        return await GetProfileAsync(ownerId, depotId);
    }
    public async Task<PagedResult<FactoryPartnerDto>> GetFactoriesAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        if (!string.IsNullOrEmpty(query.Status) && query.Status is not ("APPROVED" or "PENDING" or "BLOCKED"))
            throw new ArgumentException("Trạng thái đối tác không hợp lệ.");
        return await owners.FactoriesAsync(depotId, query);
    }
    public async Task<PagedResult<DemandDto>> GetDemandsAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        return await owners.DemandsAsync(query, DateTime.UtcNow);
    }
    internal static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> source, DepotQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentException("Phân trang không hợp lệ.");
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = await source.CountAsync(),
            Items = offset > int.MaxValue ? [] : await source.Skip((int)offset).Take(query.PageSize).ToListAsync() };
    }
}
