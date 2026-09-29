using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Repositories;

internal static class DepotRepositoryPage
{
    internal static async Task<PagedResult<T>> ReadAsync<T>(IQueryable<T> source, DepotQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new ArgumentException("Phân trang không hợp lệ.");
        var offset = (long)(query.Page - 1) * query.PageSize;
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = await source.CountAsync(),
            Items = offset > int.MaxValue ? [] : await source.Skip((int)offset).Take(query.PageSize).ToListAsync() };
    }
}
