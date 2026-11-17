using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Factory;

namespace Retrack.API.Controllers.Factory;

[ApiController]
[Route("api/factory/notifications")]
[Authorize(Roles = "FACTORY")]
public sealed class FactoryNotificationController(FactoryNotificationService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        => Ok(ApiResponse<PageResponse<FactoryNotificationResponse>>.Ok(await service.ListAsync(UserId, page, pageSize, unreadOnly, ct)));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await service.UnreadCountAsync(UserId, ct)));

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(UserId, id, true, ct);
        return Ok(ApiResponse<string>.Ok("Đã đánh dấu đã đọc."));
    }

    [HttpPatch("{id:guid}/unread")]
    public async Task<IActionResult> Unread(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(UserId, id, false, ct);
        return Ok(ApiResponse<string>.Ok("Đã đánh dấu chưa đọc."));
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
        => Ok(ApiResponse<int>.Ok(await service.ReadAllAsync(UserId, ct)));
}
