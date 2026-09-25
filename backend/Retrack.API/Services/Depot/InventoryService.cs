using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class InventoryService(AppDbContext db, IDepotService scope) : IInventoryService
{
    public async Task<List<InventoryRowDto>> GetAsync(Guid ownerId, Guid depotId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var received = await db.PickupRequestItems.AsNoTracking()
            .Where(i => i.PickupRequest.TargetDepotId == depotId && i.PickupRequest.Status == "DONE")
            .GroupBy(i => i.MaterialType).Select(g => new { Material = g.Key, Kg = g.Sum(i => i.WeightKg) }).ToListAsync();
        var allocated = await db.InventoryBatches.AsNoTracking().Where(b => b.DepotId == depotId && b.Status != "CANCELLED")
            .GroupBy(b => new { b.MaterialType, b.Status }).Select(g => new { Material = g.Key.MaterialType, g.Key.Status, Kg = g.Sum(b => b.DeclaredWeightKg) }).ToListAsync();
        var exportedStates = new[] { "IN_PROGRESS", "IN_TRANSIT", "DELIVERED", "VERIFIED", "COMPLETED", "REJECTED" };
        return received.Select(i => i.Material).Union(allocated.Select(i => i.Material)).OrderBy(m => m)
            .Select(m => new InventoryRowDto(m, received.Where(i => i.Material == m).Sum(i => i.Kg),
                allocated.Where(i => i.Material == m && !exportedStates.Contains(i.Status)).Sum(i => i.Kg),
                allocated.Where(i => i.Material == m && exportedStates.Contains(i.Status)).Sum(i => i.Kg))).ToList();
    }
}
