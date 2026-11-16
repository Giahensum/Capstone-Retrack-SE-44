using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Services.Driver;

namespace Retrack.API.Controllers.Driver;

[ApiController]
[Route("api/driver/transport")]
[Authorize(Roles = "DRIVER")]
public class DriverTransportController(DriverJobService jobs, DriverNoticeService notices) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await notices.LegacyTripsAsync(GetUserId(), ct);
        return Ok(new { success = true, data = items });
    }

    [HttpPost("{jobId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid jobId, CancellationToken ct)
    {
        var result = await jobs.AcceptAsync(GetUserId(), jobId, ct);
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
