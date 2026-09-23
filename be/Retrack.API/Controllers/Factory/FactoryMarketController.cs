using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using FactoryEntity = Retrack.API.Models.Factory;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/marketplace")]
[Authorize(Roles = "FACTORY")]
public class FactoryMarketController(AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet("batches")]
    public async Task<IActionResult> Batches([FromQuery] MarketQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        if (query.MinWeightKg.HasValue && query.MaxWeightKg.HasValue && query.MinWeightKg > query.MaxWeightKg)
            return BadRequest(new { success = false, message = "Khối lượng tối thiểu lớn hơn tối đa." });
        var source = Db.InventoryBatches.AsNoTracking().Where(x => x.Status == BatchStatus.LISTED && (x.TargetFactoryId == null || x.TargetFactoryId == factory.Id))
            .Include(x => x.Depot).ThenInclude(x => x.User).Include(x => x.Images).AsQueryable();
        if (query.Material.HasValue) source = source.Where(x => x.MaterialType == query.Material);
        if (query.MinWeightKg.HasValue) source = source.Where(x => x.EstimatedWeightKg >= query.MinWeightKg);
        if (query.MaxWeightKg.HasValue) source = source.Where(x => x.EstimatedWeightKg <= query.MaxWeightKg);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.BatchCode.Contains(query.Search) || (x.Description != null && x.Description.Contains(query.Search)) || x.Depot.CompanyName.Contains(query.Search));
        if (query.DirectOnly) source = source.Where(x => x.TargetFactoryId == factory.Id);
        var all = await source.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var matches = all.Where(b => CanAccept(factory, b)).ToList();
        var total = matches.Count;
        var items = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(b => new
        {
            b.Id, b.BatchCode, materialType = b.MaterialType.ToString(), b.EstimatedWeightKg,
            b.UnitPrice, b.Description, b.ThumbnailImageUrl, imageUrls = b.Images.Select(i => i.ImageUrl), b.CreatedAt,
            depot = new { b.Depot.Id, b.Depot.CompanyName, b.Depot.Address, b.Depot.ContactPhone, b.Depot.Latitude, b.Depot.Longitude },
            isDirectOffer = b.TargetFactoryId == factory.Id
        });
        return Ok(new { success = true, data = new { items, totalCount = total, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)total / query.PageSize) } });
    }

    [HttpGet("prices")]
    public async Task<IActionResult> Prices(CancellationToken ct)
    {
        var priceRows = await Db.MarketPrices.AsNoTracking()
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var prices = priceRows.GroupBy(x => x.MaterialType).Select(g => g.First())
            .OrderBy(x => x.MaterialType).ToList();
        return Ok(new { success = true, data = prices.Select(x => new { materialType = x.MaterialType.ToString(), x.PricePerKg, x.EffectiveDate, x.Source }) });
    }

    [HttpPost("batches/{batchId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid batchId, BatchOfferRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var batch = await Db.InventoryBatches.Include(x => x.Depot).SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy lô hàng." });
        if (batch.Status != BatchStatus.LISTED || (batch.TargetFactoryId.HasValue && batch.TargetFactoryId != factory.Id))
            return Conflict(new { success = false, message = "Lô hàng không còn khả dụng cho nhà máy này." });
        if (!CanAccept(factory, batch)) return BadRequest(new { success = false, message = "Vật liệu hoặc khối lượng lô không phù hợp năng lực nhà máy." });
        var partnership = await Db.Partnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == batch.DepotId, ct);
        if (partnership?.Status == PartnershipStatus.BLOCKED) return Conflict(new { success = false, message = "Đối tác này đang bị chặn." });
        if (partnership is null) Db.Partnerships.Add(new Partnership { Id = Guid.NewGuid(), FactoryId = factory.Id, DepotId = batch.DepotId, Status = PartnershipStatus.PENDING });
        batch.Status = BatchStatus.ACCEPTED;
        var order = new BatchOrder { Id = Guid.NewGuid(), BatchId = batch.Id, FactoryId = factory.Id, AgreedPrice = request.AgreedPricePerKg, Status = BatchStatus.ACCEPTED };
        Db.BatchOrders.Add(order);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Created($"/api/factory/orders/{order.Id}", new { success = true, message = "Đã nhận lô hàng.", data = new { order.Id, order.BatchId, order.Status, order.AgreedPrice } });
    }

    [HttpPost("offers/{batchId:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid batchId, [FromBody] DecisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { success = false, message = "Nhập lý do từ chối." });
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == batchId && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy lời mời hợp tác." });
        if (batch.Status != BatchStatus.LISTED) return Conflict(new { success = false, message = "Lời mời không còn khả dụng." });
        batch.Status = BatchStatus.REJECTED;
        var directOrder = await Db.BatchOrders.SingleOrDefaultAsync(x => x.BatchId == batch.Id && x.FactoryId == factory.Id, ct);
        if (directOrder is not null) directOrder.RejectionReason = request.Reason.Trim();
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã từ chối lời mời." });
    }

    private static bool CanAccept(FactoryEntity factory, InventoryBatch batch) =>
        (factory.AcceptedMaterials.Length == 0 || factory.AcceptedMaterials.Contains(batch.MaterialType)) &&
        batch.EstimatedWeightKg > 0 && batch.EstimatedWeightKg <= factory.CapacityKgPerMonth;
}
