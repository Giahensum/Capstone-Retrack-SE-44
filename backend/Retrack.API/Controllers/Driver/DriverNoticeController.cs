using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Driver;

namespace Retrack.API.Controllers.Driver;

[ApiController, Authorize(Roles = "DRIVER"), Route("api/driver/notifications")]
public sealed class DriverNoticeController(DriverNoticeService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        return Ok(ApiResponse<object>.Ok(await service.ListAsync(UserId, page, ct)));
    }
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        return await service.MarkReadAsync(UserId, id, ct)
            ? Ok(ApiResponse<bool>.Ok(true))
            : NotFound(ApiResponse<object>.Fail("Không tìm thấy thông báo."));
    }
}
