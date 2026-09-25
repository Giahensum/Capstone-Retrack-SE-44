using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers.Admin;

/// <summary>
/// UC-7.1 — Dashboard thống kê toàn hệ thống
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "ADMIN")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminDashboardController(IAdminService adminService) => _adminService = adminService;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _adminService.GetDashboardStatsAsync();
        return Ok(ApiResponse<AdminDashboardStatsDto>.Ok(result));
    }
}
