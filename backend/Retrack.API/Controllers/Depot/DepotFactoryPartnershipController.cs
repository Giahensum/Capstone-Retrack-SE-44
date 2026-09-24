using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Depot;

[ApiController]
[Route("api/depot/partnerships")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotFactoryPartnershipController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var depot = await db.Depots.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == userId, ct);
        if (depot is null) return NotFound(new { success = false, message = "Tài khoản chưa có hồ sơ vựa." });
        var items = await db.FactoryDepotPartnerships.AsNoTracking()
            .Where(x => x.DepotId == depot.Id)
            .Include(x => x.Factory).ThenInclude(x => x.Owner)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.FactoryId, factoryName = x.Factory.Name, contactPhone = x.Factory.Owner!.Phone, x.Status, x.CreatedAt, x.UpdatedAt })
            .ToListAsync(ct);
        return Ok(new { success = true, data = items });
    }

    [HttpPut("{factoryId:guid}/status")]
    public async Task<IActionResult> Update(Guid factoryId, [FromBody] UpdatePartnershipRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var depotId = await db.Depots.Where(x => x.OwnerId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (depotId is null) return NotFound(new { success = false, message = "Tài khoản chưa có hồ sơ vựa." });
        if (request.Status is not ("APPROVED" or "BLOCKED"))
            return BadRequest(new { success = false, message = "Trạng thái phải là APPROVED hoặc BLOCKED." });

        var partnership = await db.FactoryDepotPartnerships
            .SingleOrDefaultAsync(x => x.DepotId == depotId && x.FactoryId == factoryId, ct);
        if (partnership is null) return NotFound(new { success = false, message = "Không tìm thấy yêu cầu hợp tác." });
        partnership.Status = request.Status;
        partnership.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { partnership.Id, partnership.FactoryId, partnership.DepotId, partnership.Status } });
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token thiếu user id hợp lệ.");
    }
}

public record UpdatePartnershipRequest(string Status);
