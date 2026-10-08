using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Driver;

namespace Retrack.API.Controllers.Driver;

[ApiController, Authorize(Roles = "DRIVER"), Route("api/driver")]
public sealed class DriverDashboardController(DriverReportingService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(ApiResponse<DriverDashboardDto>.Ok(await service.DashboardAsync(UserId, ct)));
    [HttpGet("trip-history")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string period = "all", [FromQuery] string receipt = "all", CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<DriverHistoryRow>>.Ok(await service.HistoryAsync(UserId, page, pageSize, period, receipt, ct)));
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) =>
        Ok(ApiResponse<DriverStatsDto>.Ok(await service.StatsAsync(UserId, ct)));
}


