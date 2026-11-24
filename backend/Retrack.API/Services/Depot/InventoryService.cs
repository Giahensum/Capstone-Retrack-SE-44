using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Repositories;

namespace Retrack.API.Services.Depot;

public sealed class InventoryService(IDepotInventoryRepository repository, IDepotService scope) : IInventoryService
{
    public async Task<Retrack.API.DTOs.PagedResult<InventoryHistoryDto>> HistoryAsync(Guid ownerId, Guid depotId, InventoryHistoryQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.Search?.Length > 255 ||
            query.Source is not (null or "" or "PICKUP" or "RECEIPT" or "EXTERNAL_PURCHASE" or "OPENING_BALANCE" or "BULK_PURCHASE") ||
            query.Status is not (null or "" or "DONE" or "DRAFT" or "SUBMITTED" or "POSTED" or "REJECTED" or "VOIDED"))
            throw new ArgumentException("Bộ lọc lịch sử nhập hoặc phân trang không hợp lệ.");
        return await repository.HistoryAsync(depotId, query);
    }
    public async Task<List<InventoryRowDto>> GetAsync(Guid ownerId, Guid depotId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var received = (await repository.ReceivedAsync(depotId)).Select(x => x with { Material = Retrack.API.Services.Shared.MaterialCatalog.Normalize(x.Material) }).ToList();
        var allocated = (await repository.AllocatedAsync(depotId)).Select(x => x with { Material = Retrack.API.Services.Shared.MaterialCatalog.Normalize(x.Material) }).ToList();
        var exportedStates = new[] { "IN_PROGRESS", "IN_TRANSIT", "DELIVERED", "RECEIVED", "WEIGHED", "VERIFIED", "COMPLETED", "PAID" };
        bool Exported(DepotAllocatedWeight row) => row.HasLeftDepot || exportedStates.Contains(row.Status);
        // Từ chối lời mời chưa xuất kho giải phóng giữ chỗ; QC từ chối không chứng minh hàng đã trả về.
        return received.Select(i => i.Material).Union(allocated.Select(i => i.Material)).OrderBy(m => m)
            .Select(m => new InventoryRowDto(m, received.Where(i => i.Material == m).Sum(i => i.Kg),
                allocated.Where(i => i.Material == m && !Exported(i) && i.Status != "REJECTED").Sum(i => i.Kg),
                allocated.Where(i => i.Material == m && Exported(i)).Sum(i => i.Kg))).ToList();
    }
}
