using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Repositories;

namespace Retrack.API.Services.Depot;

public sealed class InventoryService(IDepotInventoryRepository repository, IDepotService scope) : IInventoryService
{
    public async Task<List<InventoryRowDto>> GetAsync(Guid ownerId, Guid depotId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var received = await repository.ReceivedAsync(depotId);
        var allocated = await repository.AllocatedAsync(depotId);
        var exportedStates = new[] { "IN_PROGRESS", "IN_TRANSIT", "DELIVERED", "RECEIVED", "WEIGHED", "VERIFIED", "COMPLETED", "PAID" };
        bool Exported(DepotAllocatedWeight row) => row.HasLeftDepot || exportedStates.Contains(row.Status);
        // Từ chối lời mời chưa xuất kho giải phóng giữ chỗ; QC từ chối không chứng minh hàng đã trả về.
        return received.Select(i => i.Material).Union(allocated.Select(i => i.Material)).OrderBy(m => m)
            .Select(m => new InventoryRowDto(m, received.Where(i => i.Material == m).Sum(i => i.Kg),
                allocated.Where(i => i.Material == m && !Exported(i) && i.Status != "REJECTED").Sum(i => i.Kg),
                allocated.Where(i => i.Material == m && Exported(i)).Sum(i => i.Kg))).ToList();
    }
}
