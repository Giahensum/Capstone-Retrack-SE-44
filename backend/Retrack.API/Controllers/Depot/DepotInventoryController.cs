using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>
/// Quản lý tồn kho
/// </summary>
[ApiController]
[Route("api/depot/inventory")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotInventoryController(IInventoryService inventory, IDepotService depot) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid depotId) =>
        Ok(ApiResponse<List<InventoryRowDto>>.Ok(await inventory.GetAsync(OwnerId, depotId)));
    [HttpGet("receipts")]
    public async Task<IActionResult> Receipts([FromQuery] Guid depotId, [FromQuery] DepotQuery query)
    {
        query.Status = "DONE";
        return Ok(ApiResponse<PagedResult<DepotPaymentDto>>.Ok(await depot.GetPaymentsAsync(OwnerId, depotId, query)));
    }
}


