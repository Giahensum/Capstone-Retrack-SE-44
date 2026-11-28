using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public sealed record FactoryOrderPage(IReadOnlyList<InventoryBatch> Items, int TotalCount);

public interface IFactoryOrderReadRepository
{
    Task<FactoryOrderPage> ListAsync(Guid factoryId, OrderQuery query, string? displayStatus, CancellationToken ct);
    Task<InventoryBatch?> GetAsync(Guid factoryId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<InventoryBatch>> DashboardPriorityAsync(Guid factoryId, CancellationToken ct);
    Task<IReadOnlyList<InventoryBatch>> DashboardRecentAsync(Guid factoryId, CancellationToken ct);
}
