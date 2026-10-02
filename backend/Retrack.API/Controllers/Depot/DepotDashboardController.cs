using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>
/// Chỉ số tổng quan và thống kê kho vựa
/// </summary>
[ApiController]
[Route("api/depot/dashboard")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotDashboardController(IDepotReportService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid depotId) => Ok(ApiResponse<DashboardDto>.Ok(
        await service.DashboardAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), depotId)));
}


