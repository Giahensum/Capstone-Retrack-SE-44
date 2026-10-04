using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/partners"), Authorize(Roles = "DEPOT_OWNER")]
public sealed class DepotPartnerController(IDepotService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid depotId, [FromQuery] FactorySearchQuery query) =>
        Ok(ApiResponse<PagedResult<FactoryPartnerDto>>.Ok(await service.GetFactoriesAsync(OwnerId, depotId, query)));
    [HttpGet("demands")]
    public async Task<IActionResult> Demands([FromQuery] Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<DemandDto>>.Ok(await service.GetDemandsAsync(OwnerId, depotId, query)));
}
