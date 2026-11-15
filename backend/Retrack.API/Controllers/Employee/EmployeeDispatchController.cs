using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;

namespace Retrack.API.Controllers.Employee;

[ApiController, Authorize(Roles = "DEPOT_EMPLOYEE"), Route("api/employee/dispatch")]
public sealed class EmployeeDispatchController(EmployeeDispatchService dispatch) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts(CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await dispatch.MineAsync(UserId, ct)));
    [HttpPut("shifts/{id:guid}/availability")]
    public async Task<IActionResult> Availability(Guid id, AvailabilityInput input, CancellationToken ct)
    {
        await dispatch.ChangeAvailabilityAsync(UserId, id, input, ct);
        return Ok(ApiResponse<object>.Ok(await dispatch.MineAsync(UserId, ct)));
    }
}
