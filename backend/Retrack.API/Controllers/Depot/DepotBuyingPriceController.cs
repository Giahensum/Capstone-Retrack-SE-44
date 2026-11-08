using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/buying-prices"), Authorize(Roles = "DEPOT_OWNER")]
public class DepotBuyingPriceController(IDepotBuyingPriceService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List(Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<DepotBuyingPriceDto>>.Ok(await service.ListAsync(Owner, depotId, query)));
    [HttpPut("{material}")]
    public async Task<IActionResult> Save(Guid depotId, string material, SaveDepotBuyingPriceDto dto) =>
        Ok(ApiResponse<DepotBuyingPriceDto>.Ok(await service.SaveAsync(Owner, depotId, material, dto)));
}
