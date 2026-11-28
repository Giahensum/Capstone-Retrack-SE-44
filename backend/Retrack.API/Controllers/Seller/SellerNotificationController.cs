using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Services.Seller;
using Retrack.API.DTOs;

namespace Retrack.API.Controllers.Seller;

[ApiController]
[Route("api/seller/notifications")]
[Authorize(Roles = "SELLER")]
public class SellerNotificationController(SellerNotificationService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var result = await service.ListAsync(UserId, page, pageSize, unreadOnly, category, ct);
        return Ok(ApiResponse<PagedResult<SellerNotificationDto>>.Ok(result));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var count = await service.UnreadCountAsync(UserId, ct);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var changed = await service.SetReadAsync(UserId, id, true, ct);

        return !changed
            ? NotFound(ApiResponse<string>.Fail("Không tìm thấy thông báo."))
            : Ok(ApiResponse<string>.Ok("Đã đánh dấu đã đọc."));
    }

    [HttpPatch("{id:guid}/unread")]
    public async Task<IActionResult> MarkUnread(Guid id, CancellationToken ct = default)
    {
        var changed = await service.SetReadAsync(UserId, id, false, ct);

        return !changed
            ? NotFound(ApiResponse<string>.Fail("Không tìm thấy thông báo."))
            : Ok(ApiResponse<string>.Ok("Đã đánh dấu chưa đọc."));
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct = default)
    {
        var updated = await service.ReadAllAsync(UserId, ct);

        return Ok(ApiResponse<int>.Ok(updated, "Đã đánh dấu tất cả thông báo là đã đọc."));
    }
}
