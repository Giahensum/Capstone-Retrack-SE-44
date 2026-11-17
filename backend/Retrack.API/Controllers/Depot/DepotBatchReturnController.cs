using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
namespace Retrack.API.Controllers.Depot;

[ApiController, Authorize(Roles = "DEPOT_OWNER"), Route("api/depot/batches")]
public sealed class DepotBatchReturnController(BatchReturnService service) : ControllerBase
{
    [HttpPost("{id:guid}/return-received")]
    public async Task<IActionResult> Receive(Guid id, [FromQuery] Guid depotId, BatchReturnReceiptDto input)
    {
        await service.ReceiveAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), depotId, id, input);
        return Ok(ApiResponse<string>.Ok("Đã xác nhận nhận đủ toàn bộ lô trả về và hoàn nguồn tồn đúng một lần."));
    }
}
