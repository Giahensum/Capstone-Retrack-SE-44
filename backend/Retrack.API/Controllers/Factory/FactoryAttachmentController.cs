using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Factory;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/attachments")]
[Authorize]
public sealed class FactoryAttachmentController(FactoryAttachmentService attachments) : FactoryControllerBase
{
    [HttpPost]
    [Authorize(Roles = "FACTORY")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] FactoryAttachmentUploadDto upload, CancellationToken ct)
    {
        var url = await attachments.UploadAsync(CurrentUserId, upload.File, ct);
        return Ok(ApiResponse<object>.Ok(new { url }, "Đã tải tệp lên."));
    }

    [HttpGet("{ownerId:guid}/{id:guid}")]
    [Authorize(Roles = "FACTORY,DEPOT_OWNER,ADMIN")]
    public async Task<IActionResult> Download(Guid ownerId, Guid id, CancellationToken ct)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        var (path, contentType) = await attachments.OpenAsync(CurrentUserId, role, ownerId, id, ct);
        return PhysicalFile(path, contentType, $"retrack-factory-{id:N}{Path.GetExtension(path)}");
    }
}

public sealed class FactoryAttachmentUploadDto
{
    public IFormFile File { get; set; } = default!;
}
