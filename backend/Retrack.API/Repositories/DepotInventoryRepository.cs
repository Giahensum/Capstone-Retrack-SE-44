using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.API.Repositories;

public record DepotReceivedWeight(string Material, decimal Kg);
public record DepotAllocatedWeight(string Material, string Status, bool HasLeftDepot, decimal Kg);

public interface IDepotInventoryRepository
{
    Task<List<DepotReceivedWeight>> ReceivedAsync(Guid depotId);
    Task<List<DepotAllocatedWeight>> AllocatedAsync(Guid depotId);
}

public sealed class DepotInventoryRepository(AppDbContext db) : IDepotInventoryRepository
{
    public Task<List<DepotReceivedWeight>> ReceivedAsync(Guid depotId) => db.PickupRequestItems.AsNoTracking()
        .Where(i => i.PickupRequest.TargetDepotId == depotId && i.PickupRequest.Status == "DONE")
        .GroupBy(i => i.MaterialType).Select(g => new DepotReceivedWeight(g.Key, g.Sum(i => i.WeightKg))).ToListAsync();

    public Task<List<DepotAllocatedWeight>> AllocatedAsync(Guid depotId) => db.InventoryBatches.AsNoTracking()
        .Where(b => b.DepotId == depotId && b.Status != "CANCELLED")
        .GroupBy(b => new { b.MaterialType, b.Status,
            HasLeftDepot = (b.TransportJob != null && (b.TransportJob.Status == "PICKED_UP" || b.TransportJob.Status == "DELIVERED")) ||
                b.FactoryReceivedAt != null || b.QualityCheck != null })
        .Select(g => new DepotAllocatedWeight(g.Key.MaterialType, g.Key.Status, g.Key.HasLeftDepot, g.Sum(b => b.DeclaredWeightKg))).ToListAsync();
}
