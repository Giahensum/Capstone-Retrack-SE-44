using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
namespace Retrack.API.Controllers.Depot;

public sealed class SubmitDepotVerificationForm
{
    public Guid OperationId { get; set; }
    public string? Note { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}

[ApiController, Route("api/depot/verifications"), Authorize(Roles = "DEPOT_OWNER,ADMIN")]
public sealed class DepotVerificationController(DepotVerificationService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost("/api/depot/owned"), Authorize(Roles = "DEPOT_OWNER")]
    public async Task<IActionResult> Create(CreateDepotDto dto) => Ok(ApiResponse<DepotProfileDto>.Ok(await service.CreateDepotAsync(Actor, dto)));
    [HttpGet, Authorize(Roles = "DEPOT_OWNER")]
    public async Task<IActionResult> Mine(Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<VerificationSubmissionDto>>.Ok(await service.ListAsync(Actor, depotId, query)));
    [HttpPost, Authorize(Roles = "DEPOT_OWNER"), RequestSizeLimit(7 * 1024 * 1024)]
    public async Task<IActionResult> Submit(Guid depotId, [FromForm] SubmitDepotVerificationForm form) =>
        Ok(ApiResponse<Guid>.Ok(await service.SubmitAsync(Actor, depotId, form.OperationId, form.Note, form.Files)));
    [HttpGet("/api/admin/depot-verifications"), Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Queue([FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<VerificationSubmissionDto>>.Ok(await service.ListAsync(Actor, null, query)));
    [HttpPatch("/api/admin/depot-verifications/{id:guid}"), Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Review(Guid id, VerificationReviewDto dto)
    {
        await service.ReviewAsync(Actor, id, dto); return Ok(ApiResponse<string>.Ok("Đã lưu kết quả duyệt hồ sơ."));
    }
    [HttpGet("documents/{id:guid}")]
    public async Task<IActionResult> Document(Guid id)
    {
        var document = await service.FileAsync(Actor, id);
        Response.Headers.CacheControl = "no-store"; Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(document.Content, document.ContentType, document.Name);
    }
}
