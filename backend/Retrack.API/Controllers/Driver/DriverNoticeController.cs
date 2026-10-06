using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Services.Driver;

namespace Retrack.API.Controllers.Driver;

[ApiController, Authorize(Roles = "DRIVER"), Route("api/driver/notifications")]
public sealed class DriverNoticeController(AppDbContext db, DriverJobService scope) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("Trang không hợp lệ.");
        await scope.RequireAsync(UserId, ct);
        var source = db.Notifications.AsNoTracking().Where(n => n.UserId == UserId);
        return Ok(ApiResponse<object>.Ok(new { page, pageSize = 20, totalCount = await source.CountAsync(ct),
            unreadCount = await source.CountAsync(n => !n.IsRead, ct),
            items = await source.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip((page - 1) * 20).Take(20)
                .Select(n => new { n.Id, n.Title, n.Message, n.IsRead, n.CreatedAt, jobId = n.TransportJobId }).ToListAsync(ct) }));
    }
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await scope.RequireAsync(UserId, ct);
        var changed = await db.Notifications.Where(n => n.Id == id && n.UserId == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
        return changed == 0 ? NotFound(ApiResponse<object>.Fail("Không tìm thấy thông báo.")) : Ok(ApiResponse<bool>.Ok(true));
    }
}
