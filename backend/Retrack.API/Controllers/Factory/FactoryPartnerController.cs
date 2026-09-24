using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/partners")]
[Authorize(Roles = "FACTORY")]
public class FactoryPartnerController(Retrack.API.Data.AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var source = Db.FactoryDepotPartnerships.AsNoTracking().Where(x => x.FactoryId == factory.Id)
            .Include(x => x.Depot).ThenInclude(x => x.Owner).OrderByDescending(x => x.CreatedAt);
        var total = await source.CountAsync(ct);
        var partnerships = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var depotIds = partnerships.Select(x => x.DepotId).ToArray();
        var reviews = await Db.FactoryDepotReviews.AsNoTracking().Where(x => x.FactoryId == factory.Id && depotIds.Contains(x.DepotId))
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var latestReviews = reviews.GroupBy(x => x.DepotId).ToDictionary(x => x.Key, x => x.First());
        var items = partnerships.Select(x => new
        {
            x.Id, x.DepotId, name = x.Depot.Name, x.Depot.Address, contactPhone = x.Depot.Owner.Phone,
            status = x.Status, rating = x.Depot.Rating,
            latestRating = latestReviews.TryGetValue(x.DepotId, out var review) ? review.Rating : null,
            latestComment = latestReviews.TryGetValue(x.DepotId, out review) ? review.Comment : null,
            reviewCount = reviews.Count(r => r.DepotId == x.DepotId),
            orderCount = Db.InventoryBatches.Count(b => b.TargetFactoryId == x.FactoryId && b.DepotId == x.DepotId && b.Status != "MARKETPLACE"),
            completedOrderCount = Db.InventoryBatches.Count(b => b.TargetFactoryId == x.FactoryId && b.DepotId == x.DepotId && b.Status == "COMPLETED")
        }).ToList();
        return Ok(new { success = true, data = new { items, totalCount = total, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)total / query.PageSize) } });
    }

    [HttpPut("{depotId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid depotId, [FromBody] PartnerStatusRequest request, CancellationToken ct)
    {
        var status = request.Status.ToString();
        if (status != "BLOCKED") return BadRequest(new { success = false, message = "Vựa duyệt quan hệ đối tác. Nhà máy chỉ có thể chặn đối tác." });
        var factory = await CurrentFactory(ct);
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == depotId, ct);
        if (partner is null) return NotFound(new { success = false, message = "Không tìm thấy đối tác." });
        if (partner.Status == status) return Ok(new { success = true, data = new { partner.Id, partner.DepotId, partner.Status } });
        partner.Status = status;
        partner.UpdatedAt = DateTime.UtcNow;
        var waitingBatches = await Db.InventoryBatches.Where(x => x.DepotId == depotId && x.DirectOfferFactoryId == factory.Id && x.Status == "PENDING_FACTORY").ToListAsync(ct);
        foreach (var batch in waitingBatches)
        {
            batch.Status = "REJECTED";
            batch.RejectionReason = "Nhà máy đã chặn vựa đối tác.";
            batch.DirectOfferFactoryId = null;
        }
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { partner.Id, partner.DepotId, partner.Status } });
    }

    [HttpPost("orders/{orderId:guid}/rating")]
    public async Task<IActionResult> Rate(Guid orderId, RatingRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.SingleOrDefaultAsync(x => x.Id == orderId && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (batch.Status is not ("PAID" or "COMPLETED")) return Conflict(new { success = false, message = "Chỉ đánh giá sau khi đã quyết toán." });
        var review = await Db.FactoryDepotReviews.SingleOrDefaultAsync(x => x.BatchId == batch.Id && x.FactoryId == factory.Id, ct);
        if (review is null)
        {
            review = new FactoryDepotReview { BatchId = batch.Id, FactoryId = factory.Id, DepotId = batch.DepotId };
            Db.FactoryDepotReviews.Add(review);
        }
        review.Rating = request.Rating;
        review.Comment = request.Comment?.Trim();
        var partner = await Db.FactoryDepotPartnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == batch.DepotId, ct);
        if (partner is not null)
        {
            if (request.BlockPartner) partner.Status = "BLOCKED";
            // Đánh giá không được tự phê duyệt quan hệ; vựa phải duyệt ở vai trò của họ.
            partner.UpdatedAt = DateTime.UtcNow;
        }
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã lưu đánh giá đối tác.", data = new { review.Id, review.Rating, review.Comment } });
    }
}

public class PartnerStatusRequest { public Retrack.API.Models.Enums.PartnershipStatus Status { get; set; } }
