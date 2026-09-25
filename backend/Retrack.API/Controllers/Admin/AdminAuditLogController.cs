using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers.Admin;

/// <summary>
/// UC-7.6 — Xem Audit Logs
/// </summary>
[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Roles = "ADMIN")]
public class AdminAuditLogController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminAuditLogController(IAdminService adminService) => _adminService = adminService;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? entityName, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetAuditLogsAsync(entityName, userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }
}
