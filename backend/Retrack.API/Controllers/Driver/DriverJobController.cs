using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Driver;

namespace Retrack.API.Controllers.Driver;

[ApiController, Authorize(Roles = "DRIVER"), Route("api/driver")]
public sealed class DriverJobController(DriverJobService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
    [HttpGet("job-pool")]
    public async Task<IActionResult> Pool([FromQuery] int page = 1, CancellationToken ct = default) => Ok(ApiResponse<PagedResult<DriverJobDto>>.Ok(await service.ListAsync(UserId, false, page, ct)));
    [HttpGet("jobs/mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, CancellationToken ct = default) => Ok(ApiResponse<PagedResult<DriverJobDto>>.Ok(await service.ListAsync(UserId, true, page, ct)));
    [HttpGet("job/{id:guid}")]
    [HttpGet("job/{id:guid}/route-info")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Ok(ApiResponse<DriverJobDto>.Ok(await service.DetailAsync(UserId, id, ct)));
    [HttpPost("job/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct) => Ok(ApiResponse<DriverJobDto>.Ok(await service.AcceptAsync(UserId, id, ct)));
}
