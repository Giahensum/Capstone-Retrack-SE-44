using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Employee;

namespace Retrack.API.Controllers.Employee;

[ApiController, Authorize(Roles = "DEPOT_EMPLOYEE"), Route("api/employee")]
public sealed class EmployeeReportsController(EmployeeReportingService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    [HttpGet("pickup-history")]
    public async Task<IActionResult> History(CancellationToken ct, int page = 1, int pageSize = 20, string? status = null) =>
        Ok(ApiResponse<PagedResult<EmployeeHistoryRow>>.Ok(await service.HistoryAsync(UserId, page, pageSize, status, ct)));
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(ApiResponse<EmployeeStatsDto>.Ok(await service.StatsAsync(UserId, ct)));
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct, int page = 1, int pageSize = 20) =>
        Ok(ApiResponse<EmployeeNotificationPage>.Ok(await service.NotificationsAsync(UserId, page, pageSize, ct)));
    [HttpPatch("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await service.MarkReadAsync(UserId, id, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
