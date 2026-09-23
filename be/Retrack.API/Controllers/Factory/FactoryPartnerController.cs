using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models.Enums;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/partners")]
[Authorize(Roles = "FACTORY")]
public class FactoryPartnerController(AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var source = Db.Partnerships.AsNoTracking().Where(x => x.FactoryId == factory.Id).Include(x => x.Depot).ThenInclude(x => x.User).OrderByDescending(x => x.CreatedAt);
        var count = await source.CountAsync(ct);
        var items = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(x => new
        {
            x.Id, x.DepotId, name = x.Depot.CompanyName, x.Depot.Address, x.Depot.ContactPhone,
            status = x.Status.ToString(), x.Rating, x.Comment, x.CreatedAt,
            orderCount = Db.BatchOrders.Count(o => o.FactoryId == x.FactoryId && o.Batch.DepotId == x.DepotId),
            completedOrderCount = Db.BatchOrders.Count(o => o.FactoryId == x.FactoryId && o.Batch.DepotId == x.DepotId && o.Status == BatchStatus.VERIFIED)
        }).ToListAsync(ct);
        return Ok(new { success = true, data = new { items, totalCount = count, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)count / query.PageSize) } });
    }

    [HttpPut("{depotId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid depotId, [FromBody] PartnerStatusRequest request, CancellationToken ct)
    {
        if (request.Status is not (PartnershipStatus.APPROVED or PartnershipStatus.BLOCKED)) return BadRequest(new { success = false, message = "Trạng thái phải là APPROVED hoặc BLOCKED." });
        var factory = await CurrentFactory(ct);
        var partnership = await Db.Partnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == depotId, ct);
        if (partnership is null) return NotFound(new { success = false, message = "Không tìm thấy đối tác." });
        partnership.Status = request.Status;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { partnership.Id, partnership.DepotId, status = partnership.Status.ToString() } });
    }

    [HttpPost("orders/{orderId:guid}/rating")]
    public async Task<IActionResult> Rate(Guid orderId, RatingRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var order = await Db.BatchOrders.Include(x => x.Batch).SingleOrDefaultAsync(x => x.Id == orderId && x.FactoryId == factory.Id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (order.Status != BatchStatus.PAID || !order.SettledAt.HasValue) return Conflict(new { success = false, message = "Chỉ đánh giá sau khi đã quyết toán." });
        var partner = await Db.Partnerships.SingleOrDefaultAsync(x => x.FactoryId == factory.Id && x.DepotId == order.Batch.DepotId, ct);
        if (partner is null) return NotFound(new { success = false, message = "Không tìm thấy đối tác của đơn hàng." });
        partner.Rating = request.Rating;
        partner.Comment = request.Comment?.Trim();
        if (request.BlockPartner) partner.Status = PartnershipStatus.BLOCKED;
        else if (partner.Status == PartnershipStatus.PENDING) partner.Status = PartnershipStatus.APPROVED;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã lưu đánh giá đối tác.", data = new { partner.Id, partner.DepotId, partner.Rating, partner.Comment, status = partner.Status.ToString() } });
    }
}

public class PartnerStatusRequest { public PartnershipStatus Status { get; set; } }
