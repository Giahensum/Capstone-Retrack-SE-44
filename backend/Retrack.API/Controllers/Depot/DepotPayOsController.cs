using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Controllers.Depot;

[ApiController]
public sealed class DepotPayOsController(IDepotPayOsService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("api/depot/reports/invoices/{id:guid}/payos-link"), Authorize(Roles = "DEPOT_OWNER")]
    public async Task<IActionResult> CreateLink(Guid id, [FromQuery] Guid depotId)
    {
        try { return Ok(ApiResponse<PayOsLinkDto>.Ok(await service.CreateLinkAsync(OwnerId, depotId, id))); }
        catch (PayOsException ex) { return StatusCode(502, ApiResponse<string>.Fail(ex.Message)); }
    }

    [HttpPost("api/depot/reports/invoices/{id:guid}/payos-sync"), Authorize(Roles = "DEPOT_OWNER")]
    public async Task<IActionResult> Sync(Guid id, [FromQuery] Guid depotId)
    {
        try { return Ok(ApiResponse<string>.Ok(await service.SyncAsync(OwnerId, depotId, id))); }
        catch (PayOsException ex) { return StatusCode(502, ApiResponse<string>.Fail(ex.Message)); }
    }

    /// <summary>Webhook PayOS (khai báo URL tại my.payos.vn). Bảo vệ bằng chữ ký HMAC, không dùng JWT.</summary>
    [HttpPost("api/payos/webhook"), AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] JsonElement body) =>
        await service.HandleWebhookAsync(body) ? Ok(new { success = true }) : BadRequest(new { success = false });
}
