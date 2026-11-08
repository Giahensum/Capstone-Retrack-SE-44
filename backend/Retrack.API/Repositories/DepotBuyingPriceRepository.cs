using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class DepotBuyingPriceRepository(AppDbContext db) : IDepotBuyingPriceRepository
{
    public async Task<PagedResult<DepotBuyingPriceDto>> ListAsync(Guid depotId, DepotQuery query)
    {
        var rows = db.DepotBuyingPrices.AsNoTracking().Where(x => x.DepotId == depotId);
        if (!string.IsNullOrWhiteSpace(query.Search)) rows = rows.Where(x => x.MaterialType.Contains(query.Search));
        var count = await rows.CountAsync();
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = count,
            Items = offset > int.MaxValue ? [] : await rows.OrderBy(x => x.MaterialType).Skip((int)offset).Take(query.PageSize)
                .Select(x => new DepotBuyingPriceDto(x.MaterialType, x.PricePerKg, x.ConditionNote, x.IsActive, x.UpdatedAt)).ToListAsync() };
    }
    public async Task<DepotBuyingPrice?> FindAsync(Guid depotId, string material)
    {
        var row = await db.DepotBuyingPrices.SingleOrDefaultAsync(x => x.DepotId == depotId && x.MaterialType == material);
        if (row != null) await db.Entry(row).ReloadAsync();
        return row;
    }
    public void Add(DepotBuyingPrice price) => db.DepotBuyingPrices.Add(price);
}
