using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>
/// Xem các khoản thanh toán cho người bán
/// </summary>
[ApiController]
[Route("api/depot/payments")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotPaymentController(IDepotService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<DepotPaymentDto>>.Ok(await service.GetPaymentsAsync(OwnerId, depotId, query)));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] Guid depotId) =>
        Ok(ApiResponse<PaymentSummaryDto>.Ok(await service.GetPaymentSummaryAsync(OwnerId, depotId)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, [FromQuery] Guid depotId) =>
        Ok(ApiResponse<DepotPaymentDto>.Ok(await service.GetPaymentAsync(OwnerId, depotId, id)));
}


