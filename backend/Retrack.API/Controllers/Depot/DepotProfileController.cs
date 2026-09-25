using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>
/// Xem và cập nhật hồ sơ kho vựa
/// </summary>
[ApiController]
[Route("api/depot/profile")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotProfileController(IDepotService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid depotId) =>
        Ok(ApiResponse<DepotProfileDto>.Ok(await service.GetProfileAsync(OwnerId, depotId)));
    [HttpPut]
    public async Task<IActionResult> Update([FromQuery] Guid depotId, UpdateDepotProfileDto dto) =>
        Ok(ApiResponse<DepotProfileDto>.Ok(await service.UpdateProfileAsync(OwnerId, depotId, dto)));
}


