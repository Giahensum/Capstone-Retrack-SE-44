using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Services.Driver;

namespace Retrack.API.Controllers.Driver;

[ApiController, Authorize(Roles = "DRIVER"), Route("api/driver/job/{id:guid}")]
public sealed class DriverDeliveryController(DriverDeliveryService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
    [HttpGet("delivery")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(ApiResponse<DriverDeliveryDto>.Ok(await service.GetAsync(UserId, id, ct)));
    [HttpPost("checkin"), HttpPost("checkout"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Evidence(Guid id, [FromForm] Guid operationId, [FromForm] EmployeeCheckInRequest input, CancellationToken ct)
    {
        var action = Request.Path.Value!.TrimEnd('/').EndsWith("/checkout", StringComparison.OrdinalIgnoreCase) ? "checkout" : "checkin";
        await service.ExecuteAsync(UserId, id, action, new(operationId, null), input, ct);
        return Ok(ApiResponse<object>.Ok(new { completed = true }));
    }
    [HttpPost("start"), HttpPost("cancel"), HttpPost("reject"), HttpPost("incident")]
    public async Task<IActionResult> Action(Guid id, [FromBody] DriverActionRequest input, CancellationToken ct)
    {
        var action = Request.Path.Value!.TrimEnd('/').Split('/')[^1].ToLowerInvariant();
        await service.ExecuteAsync(UserId, id, action, input, null, ct);
        return Ok(ApiResponse<object>.Ok(new { completed = true }));
    }
}
