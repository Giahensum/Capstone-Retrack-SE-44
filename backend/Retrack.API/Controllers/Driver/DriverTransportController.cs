using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.API.Controllers.Driver;

[ApiController]
[Route("api/driver/transport")]
[Authorize(Roles = "DRIVER")]
public class DriverTransportController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var items = await db.TransportJobs.AsNoTracking()
            .Where(x => x.DriverId == userId || (x.DriverId == null && db.DepotStaffs.Any(s => s.UserId == userId && s.IsActive && s.DepotId == x.Batch.DepotId && s.StaffType == "DRIVER")))
            .Include(x => x.Batch).ThenInclude(x => x.Depot)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, batchId = x.BatchId, x.Status, x.DriverId, materialType = x.Batch.MaterialType, weightKg = x.Batch.DeclaredWeightKg, depotName = x.Batch.Depot.Name, depotAddress = x.Batch.Depot.Address, x.Batch.TargetFactoryId, x.CheckinDepotImageUrl, x.CheckoutFactoryImageUrl, x.CreatedAt, x.UpdatedAt })
            .ToListAsync(ct);
        return Ok(new { success = true, data = items });
    }

    [HttpPost("{jobId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid jobId, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = await db.TransportJobs.Include(x => x.Batch).SingleOrDefaultAsync(x => x.Id == jobId, ct);
        if (job is null) return NotFound(new { success = false, message = "Không tìm thấy chuyến vận chuyển." });
        var assignedToDepot = await db.DepotStaffs.AnyAsync(s => s.UserId == userId && s.DepotId == job.Batch.DepotId && s.IsActive && s.StaffType == "DRIVER", ct);
        if (!assignedToDepot) return Forbid();
        if (job.Status != "PENDING") return Conflict(new { success = false, message = "Chuyến xe không còn ở trạng thái chờ nhận." });
        if (job.DriverId is not null && job.DriverId != userId) return Conflict(new { success = false, message = "Chuyến xe đã được tài xế khác nhận." });
        job.DriverId = userId;
        job.Status = "ACCEPTED";
        job.UpdatedAt = DateTime.UtcNow;
        job.Batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { job.Id, job.BatchId, job.DriverId, job.Status } });
    }

    [HttpPost("{jobId:guid}/pickup")]
    public async Task<IActionResult> Pickup(Guid jobId, [FromBody] TransportEvidenceRequest request, CancellationToken ct)
    {
        var job = await AssignedJob(jobId, ct);
        if (job is null) return NotFound(new { success = false, message = "Không tìm thấy chuyến xe được giao cho tài khoản này." });
        if (job.Status != "ACCEPTED") return Conflict(new { success = false, message = "Chỉ xác nhận lấy hàng sau khi nhận chuyến." });
        job.Status = "PICKED_UP";
        job.CheckinDepotImageUrl = request.ImageUrl?.Trim();
        job.UpdatedAt = DateTime.UtcNow;
        job.Batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { job.Id, job.Status, job.CheckinDepotImageUrl } });
    }

    [HttpPost("{jobId:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid jobId, [FromBody] TransportEvidenceRequest request, CancellationToken ct)
    {
        var job = await AssignedJob(jobId, ct);
        if (job is null) return NotFound(new { success = false, message = "Không tìm thấy chuyến xe được giao cho tài khoản này." });
        if (job.Status is not ("PICKED_UP" or "IN_TRANSIT" or "ON_THE_WAY"))
            return Conflict(new { success = false, message = "Chỉ xác nhận giao hàng sau khi đã lấy hàng." });
        job.Status = "DELIVERED";
        job.CheckoutFactoryImageUrl = request.ImageUrl?.Trim();
        job.UpdatedAt = DateTime.UtcNow;
        job.Batch.Status = "DELIVERED";
        job.Batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { job.Id, job.BatchId, job.Status, job.CheckoutFactoryImageUrl, deliveredAt = job.UpdatedAt } });
    }

    private Task<Models.TransportJob?> AssignedJob(Guid id, CancellationToken ct) =>
        db.TransportJobs.Include(x => x.Batch).SingleOrDefaultAsync(x => x.Id == id && x.DriverId == GetUserId(), ct);

    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token thiếu user id hợp lệ.");
    }
}

public record TransportEvidenceRequest(string? ImageUrl);
