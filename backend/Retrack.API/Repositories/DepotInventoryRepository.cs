using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Repositories;

public record DepotReceivedWeight(string Material, decimal Kg);
public record DepotAllocatedWeight(string Material, string Status, bool HasLeftDepot, decimal Kg);

public sealed class DepotInventoryRepository(AppDbContext db) : IDepotInventoryRepository
{
    private sealed class HistoryHeader
    {
        public Guid Id { get; set; }
        public string SourceType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ReceivedAt { get; set; }
    }
    public async Task<PagedResult<InventoryHistoryDto>> HistoryAsync(Guid depotId, InventoryHistoryQuery query)
    {
        var pickups = db.PickupRequests.AsNoTracking().Where(x => x.TargetDepotId == depotId && x.Status == "DONE")
            .Select(x => new HistoryHeader { Id = x.Id, SourceType = "PICKUP", Title = "Thu gom từ " + x.Seller.FullName,
                Status = x.Status, ReceivedAt = x.StockReceivedAt ?? x.UpdatedAt });
        var receipts = db.DepotStockReceipts.AsNoTracking().Where(x => x.DepotId == depotId)
            .Select(x => new HistoryHeader { Id = x.Id, SourceType = x.SourceType, Title = x.Reference, Status = x.Status, ReceivedAt = x.ReceivedAt });
        var all = pickups.Concat(receipts);
        if (!string.IsNullOrEmpty(query.Source)) all = query.Source == "RECEIPT" ? all.Where(x => x.SourceType != "PICKUP") : all.Where(x => x.SourceType == query.Source);
        if (!string.IsNullOrEmpty(query.Status)) all = all.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            all = all.Where(x => x.Title.ToLower().Contains(search) || x.Id.ToString().Contains(search));
        }
        var count = await all.CountAsync();
        var headers = await all.OrderByDescending(x => x.ReceivedAt).ThenBy(x => x.Id).ThenBy(x => x.SourceType)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        var pickupIds = headers.Where(x => x.SourceType == "PICKUP").Select(x => x.Id).ToArray();
        var receiptIds = headers.Where(x => x.SourceType != "PICKUP").Select(x => x.Id).ToArray();
        // Chỉ lấy dòng hàng của trang đang xem; không tải toàn bộ lịch sử về frontend.
        var pickupLines = await db.PickupRequestItems.AsNoTracking().Where(x => pickupIds.Contains(x.PickupRequestId))
            .OrderBy(x => x.Id).Select(x => new { Id = x.PickupRequestId, Line = new InventoryHistoryLineDto(x.MaterialType, x.MaterialType, x.WeightKg, "kg") }).ToListAsync();
        var receiptLines = await db.DepotStockLines.AsNoTracking().Where(x => receiptIds.Contains(x.ReceiptId))
            .OrderBy(x => x.Position).ThenBy(x => x.Id).Select(x => new { Id = x.ReceiptId, Line = new InventoryHistoryLineDto(x.Name, x.MaterialType, x.Quantity, x.Unit) }).ToListAsync();
        return new() { Page = query.Page, PageSize = query.PageSize, TotalCount = count,
            Items = headers.Select(x => new InventoryHistoryDto(x.Id, x.SourceType, x.Title, x.Status, x.ReceivedAt,
                x.SourceType == "PICKUP" ? pickupLines.Where(l => l.Id == x.Id).Select(l => l.Line).ToList() : receiptLines.Where(l => l.Id == x.Id).Select(l => l.Line).ToList())).ToList() };
    }
    public async Task<List<DepotReceivedWeight>> ReceivedAsync(Guid depotId)
    {
        var received = await db.PickupRequestItems.AsNoTracking()
        .Where(i => i.PickupRequest.TargetDepotId == depotId && i.PickupRequest.Status == "DONE")
        .GroupBy(i => i.MaterialType).Select(g => new DepotReceivedWeight(g.Key, g.Sum(i => i.WeightKg))).ToListAsync();
        received.AddRange(await db.DepotStockLines.AsNoTracking()
            .Where(x => x.Receipt.DepotId == depotId && x.Receipt.Status == "POSTED" && x.Kind == "MATERIAL")
            .GroupBy(x => x.MaterialType).Select(g => new DepotReceivedWeight(g.Key!, g.Sum(x => x.Quantity))).ToListAsync());
        return received;
    }

    public Task<List<DepotAllocatedWeight>> AllocatedAsync(Guid depotId) => db.InventoryBatches.AsNoTracking()
        .Where(b => b.DepotId == depotId && b.Status != "CANCELLED" && b.Status != "RETURNED")
        .GroupBy(b => new { b.MaterialType, b.Status,
            HasLeftDepot = (b.TransportJob != null && (b.TransportJob.Status == "PICKED_UP" || b.TransportJob.Status == "DELIVERED")) ||
                b.FactoryReceivedAt != null || b.QualityCheck != null })
        .Select(g => new DepotAllocatedWeight(g.Key.MaterialType, g.Key.Status, g.Key.HasLeftDepot, g.Sum(b => b.DeclaredWeightKg))).ToListAsync();
}
