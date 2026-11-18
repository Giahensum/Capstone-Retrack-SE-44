using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/batches"), Authorize(Roles = "DEPOT_OWNER")]
public sealed class DepotBatchWorkflowController(BatchSettlementService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPatch("{id:guid}/settlement-decision")]
    public async Task<IActionResult> Decide(Guid id, [FromQuery] Guid depotId, SettlementDecisionDto dto)
    {
        await service.DecideAsync(Owner, depotId, id, dto);
        return Ok(ApiResponse<string>.Ok(dto.Accept ? "Đã đồng ý giá; chờ nhà máy chuyển tiền." : "Đã gửi yêu cầu điều chỉnh cho nhà máy."));
    }
    [HttpGet("{id:guid}/workflow-events")]
    public async Task<IActionResult> History(Guid id, [FromQuery] Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<BatchWorkflowEventDto>>.Ok(await service.HistoryAsync(Owner, depotId, id, query)));
}
