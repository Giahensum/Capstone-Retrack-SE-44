using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryMarketPage(IReadOnlyList<InventoryBatch> Items, int TotalCount);

public interface IFactoryMarketRepository
{
    Task<FactoryMarketPage> BatchesAsync(Guid factoryId, MarketQuery query, string[]? materialValues,
        string[] acceptedValues, string? search, decimal capacityKg, CancellationToken ct);
    Task<IReadOnlyList<MarketPrice>> PricesAsync(CancellationToken ct);
}
