using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/owned"), Authorize(Roles = "DEPOT_OWNER")]
public class DepotScopeController(IDepotService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<DepotSummaryDto>>.Ok(
        await service.GetDepotsAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))));
}
