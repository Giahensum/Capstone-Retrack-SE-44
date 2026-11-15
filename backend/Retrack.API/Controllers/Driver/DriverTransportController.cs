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
        await new Retrack.API.Services.Driver.DriverJobService(db).RequireAsync(userId, ct);
        var items = await db.TransportJobs.AsNoTracking()
            .Where(x => x.DriverId == userId || (x.DriverId == null && db.DepotStaffs.Any(s => s.UserId == userId && s.IsActive && s.DepotId == x.Batch.DepotId && s.StaffType == "DRIVER")))
            .Include(x => x.Batch).ThenInclude(x => x.Depot)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100)
            .Select(x => new { x.Id, batchId = x.BatchId, x.Status, x.DriverId, materialType = x.Batch.MaterialType, weightKg = x.Batch.DeclaredWeightKg, depotName = x.Batch.Depot.Name, depotAddress = x.Batch.Depot.Address, x.Batch.TargetFactoryId, x.CheckinDepotImageUrl, x.CheckoutFactoryImageUrl, x.CreatedAt, x.UpdatedAt })
            .ToListAsync(ct);
        return Ok(new { success = true, data = items });
    }

    [HttpPost("{jobId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid jobId, CancellationToken ct)
    {
        var result = await new Retrack.API.Services.Driver.DriverJobService(db).AcceptAsync(GetUserId(), jobId, ct);
        return Ok(Retrack.API.DTOs.ApiResponse<Retrack.API.Services.Driver.DriverJobDto>.Ok(result));
    }
    // API URL ảnh cũ không còn đủ bằng chứng camera/GPS; chặn đường đi tắt.
    [HttpPost("{jobId:guid}/pickup")]
    [HttpPost("{jobId:guid}/deliver")]
    public IActionResult LegacyEvidence(Guid jobId) => StatusCode(410,
        Retrack.API.DTOs.ApiResponse<object>.Fail("Cập nhật ứng dụng và dùng /api/driver/job/{id}/checkin hoặc /checkout với ảnh camera và GPS."));
    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token thiếu user id hợp lệ.");
    }
}

public record TransportEvidenceRequest(string? ImageUrl);
