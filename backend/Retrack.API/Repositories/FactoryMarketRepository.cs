using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryMarketRepository(AppDbContext db) : IFactoryMarketRepository
{
    public async Task<FactoryMarketPage> BatchesAsync(Guid factoryId, MarketQuery query, string[]? materialValues,
        string[] acceptedValues, string? search, decimal capacityKg, CancellationToken ct)
    {
        var source = db.InventoryBatches.AsNoTracking()
            .Where(x => ((x.Status == "MARKETPLACE" || x.Status == "LISTED" || x.Status == "DRAFT") && x.TargetFactoryId == null && x.DirectOfferFactoryId == null) ||
                (x.DirectOfferFactoryId == factoryId && (x.Status == "PENDING_FACTORY" || x.Status == "READY_FOR_PICKUP" || x.Status == "PENDING_APPROVAL")) ||
                (x.TargetFactoryId == factoryId && x.Status == "PENDING_APPROVAL"))
            .Include(x => x.Depot).ThenInclude(x => x.Owner).AsQueryable();
        if (materialValues is not null) source = source.Where(x => materialValues.Contains(x.MaterialType));
        if (query.MinWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg >= query.MinWeightKg.Value);
        if (query.MaxWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg <= query.MaxWeightKg.Value);
        if (search is not null) source = source.Where(x => (x.Code != null && x.Code.Contains(search)) || x.Depot.Name.Contains(search) ||
            (x.Description != null && x.Description.Contains(search)));
        if (query.DirectOnly) source = source.Where(x => x.DirectOfferFactoryId == factoryId || (x.TargetFactoryId == factoryId && x.Status == "PENDING_APPROVAL"));
        else source = source.Where(x => x.TargetFactoryId == null && x.DirectOfferFactoryId == null);
        if (acceptedValues.Length > 0) source = source.Where(x => acceptedValues.Contains(x.MaterialType));
        if (capacityKg > 0) source = source.Where(x => x.DeclaredWeightKg <= capacityKg);
        source = source.Where(x => !db.FactoryDepotPartnerships.Any(p => p.FactoryId == factoryId && p.DepotId == x.DepotId &&
            (p.Status == "BLOCKED" || p.BlockedByDepot || p.BlockedByFactory)));
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new FactoryMarketPage(rows, total);
    }

    public async Task<IReadOnlyList<MarketPrice>> PricesAsync(CancellationToken ct) =>
        await db.MarketPrices.AsNoTracking()
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
}
