using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/marketplace")]
[Authorize(Roles = "FACTORY")]
public class FactoryMarketController(Retrack.API.Data.AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet("batches")]
    public async Task<IActionResult> Batches([FromQuery] MarketQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var source = Db.InventoryBatches.AsNoTracking()
            .Where(x => ((x.Status == "MARKETPLACE" || x.Status == "LISTED" || x.Status == "DRAFT") && x.TargetFactoryId == null) ||
                (x.DirectOfferFactoryId == factory.Id && (x.Status == "PENDING_FACTORY" || x.Status == "READY_FOR_PICKUP")))
            .Include(x => x.Depot).ThenInclude(x => x.Owner).AsQueryable();
        if (query.Material.HasValue) source = source.Where(x => x.MaterialType == query.Material.Value.ToString());
        if (query.MinWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg >= query.MinWeightKg.Value);
        if (query.MaxWeightKg.HasValue) source = source.Where(x => x.DeclaredWeightKg <= query.MaxWeightKg.Value);
        if (!string.IsNullOrWhiteSpace(query.Search)) source = source.Where(x => x.Description != null && x.Description.Contains(query.Search));
        if (query.DirectOnly) source = source.Where(x => x.DirectOfferFactoryId == factory.Id);
        else source = source.Where(x => x.TargetFactoryId == null && x.DirectOfferFactoryId == null);
        var all = await source.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var matches = all.Where(x => accepted.Length == 0 || accepted.Contains(x.MaterialType))
            .Where(x => factory.CapacityKgPerMonth <= 0 || x.DeclaredWeightKg <= factory.CapacityKgPerMonth).ToList();
        var total = matches.Count;
        var items = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(x => new
        {
            x.Id, batchCode = x.Id.ToString("N")[..8].ToUpperInvariant(), materialType = x.MaterialType,
            estimatedWeightKg = x.DeclaredWeightKg, unitPrice = (decimal?)null, x.Description,
            thumbnailImageUrl = (string?)null, imageUrls = Array.Empty<string>(), x.CreatedAt,
            depot = new { x.Depot.Id, companyName = x.Depot.Name, x.Depot.Address, contactPhone = x.Depot.Owner?.Phone, x.Depot.Latitude, x.Depot.Longitude },
            isDirectOffer = x.DirectOfferFactoryId == factory.Id
        });
        return Ok(new { success = true, data = new { items, totalCount = total, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)total / query.PageSize) } });
    }

    [HttpGet("prices")]
    public async Task<IActionResult> Prices(CancellationToken ct)
    {
        var rows = await Db.MarketPrices.AsNoTracking().OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var prices = rows.GroupBy(x => x.MaterialType).Select(x => x.First()).OrderBy(x => x.MaterialType);
        return Ok(new { success = true, data = prices.Select(x => new { materialType = x.MaterialType.ToString(), x.PricePerKg, x.EffectiveDate, x.Source }) });
    }

    [HttpPost("batches/{batchId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid batchId, BatchOfferRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.Include(x => x.Depot)
            .SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy lô hàng." });
        var availableOnMarketplace = (batch.Status is "MARKETPLACE" or "LISTED" or "DRAFT") && batch.TargetFactoryId is null;
        var directOfferForFactory = (batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP") && batch.DirectOfferFactoryId == factory.Id;
        if (!availableOnMarketplace && !directOfferForFactory)
            return Conflict(new { success = false, message = "Lô hàng không còn khả dụng cho nhà máy này." });
        var accepted = factory.AcceptedMaterialsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if ((accepted.Length > 0 && !accepted.Contains(batch.MaterialType)) || (factory.CapacityKgPerMonth > 0 && batch.DeclaredWeightKg > factory.CapacityKgPerMonth))
            return BadRequest(new { success = false, message = "Vật liệu hoặc khối lượng lô không phù hợp năng lực nhà máy." });
        var partnership = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == batch.DepotId, ct);
        if (partnership?.Status == "BLOCKED") return Conflict(new { success = false, message = "Đối tác này đang bị chặn." });
        if (partnership is null) Db.FactoryDepotPartnerships.Add(new FactoryDepotPartnership { FactoryId = factory.Id, DepotId = batch.DepotId, Status = "PENDING" });
        if (!directOfferForFactory && partnership?.Status != "APPROVED")
            return Conflict(new { success = false, message = "Vựa cần duyệt quan hệ đối tác trước khi nhà máy nhận lô." });
        if (directOfferForFactory && partnership?.Status != "APPROVED")
            return Conflict(new { success = false, message = "Quan hệ đối tác chưa được vựa duyệt." });
        if (directOfferForFactory) batch.DirectOfferFactoryId = null;
        batch.TargetFactoryId = factory.Id;
        batch.Status = "ACCEPTED";
        if (batch.TransportJob is null)
            Db.TransportJobs.Add(new TransportJob { BatchId = batch.Id, Status = "PENDING" });
        await Db.SaveChangesAsync(ct);
        return Created($"/api/factory/orders/{batch.Id}", new { success = true, message = "Đã nhận lô hàng.", data = new { id = batch.Id, batchId = batch.Id, status = "ACCEPTED", agreedPrice = request.AgreedPricePerKg } });
    }

    [HttpPost("offers/{batchId:guid}/reject")]
    public async Task<IActionResult> RejectOffer(Guid batchId, [FromBody] DecisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { success = false, message = "Nhập lý do từ chối." });
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy lời mời hợp tác." });
        var directOfferForFactory = (batch.Status is "PENDING_FACTORY" or "READY_FOR_PICKUP") && batch.DirectOfferFactoryId == factory.Id;
        var marketplaceOffer = (batch.Status is "MARKETPLACE" or "LISTED" or "DRAFT") && batch.TargetFactoryId is null;
        if (!directOfferForFactory && !marketplaceOffer)
            return Conflict(new { success = false, message = "Lời mời không còn khả dụng." });
        batch.Status = "REJECTED";
        batch.RejectionReason = request.Reason.Trim();
        batch.DirectOfferFactoryId = null;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã từ chối lời mời." });
    }
}
