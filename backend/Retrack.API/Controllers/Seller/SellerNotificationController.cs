using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;

namespace Retrack.API.Controllers.Seller;

[ApiController]
[Route("api/seller/notifications")]
[Authorize(Roles = "SELLER")]
public class SellerNotificationController(AppDbContext db) : ControllerBase
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
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var source = db.Notifications.AsNoTracking().Where(n => n.UserId == UserId);

        if (unreadOnly == true)
        {
            source = source.Where(n => !n.IsRead);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            source = source.Where(n => n.Category == category);
        }

        var total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new SellerNotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message ?? string.Empty,
                Category = n.Category,
                PickupRequestId = n.PickupRequestId,
                TargetType = n.TargetType,
                TargetId = n.TargetId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<SellerNotificationDto>>.Ok(new PagedResult<SellerNotificationDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        }));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var count = await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == UserId && !n.IsRead, ct);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var changed = await db.Notifications
            .Where(n => n.Id == id && n.UserId == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return changed == 0
            ? NotFound(ApiResponse<string>.Fail("Không tìm thấy thông báo."))
            : Ok(ApiResponse<string>.Ok("Đã đánh dấu đã đọc."));
    }

    [HttpPatch("{id:guid}/unread")]
    public async Task<IActionResult> MarkUnread(Guid id, CancellationToken ct = default)
    {
        var changed = await db.Notifications
            .Where(n => n.Id == id && n.UserId == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, false), ct);

        return changed == 0
            ? NotFound(ApiResponse<string>.Fail("Không tìm thấy thông báo."))
            : Ok(ApiResponse<string>.Ok("Đã đánh dấu chưa đọc."));
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct = default)
    {
        var updated = await db.Notifications
            .Where(n => n.UserId == UserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);

        return Ok(ApiResponse<int>.Ok(updated, "Đã đánh dấu tất cả thông báo là đã đọc."));
    }
}
