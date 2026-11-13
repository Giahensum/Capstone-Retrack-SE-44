using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
namespace Retrack.API.Controllers.Depot;
[ApiController, Route("api/depot"), Authorize(Roles = "DEPOT_OWNER")]
public sealed class DepotOperationsController(DepotOperationsService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("notifications")]
    public async Task<IActionResult> Notices(Guid depotId, [FromQuery] DepotNoticeQuery query) => Ok(ApiResponse<PagedResult<DepotNoticeDto>>.Ok(await service.NoticesAsync(Owner, depotId, query)));
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> Unread(Guid depotId) => Ok(ApiResponse<int>.Ok(await service.UnreadAsync(Owner, depotId)));
    [HttpPatch("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid depotId, Guid id) { await service.ReadAsync(Owner, depotId, id); return Ok(ApiResponse<string>.Ok("Đã đánh dấu đã đọc.")); }
    [HttpPatch("notifications/{id:guid}/unread")]
    public async Task<IActionResult> MarkUnread(Guid depotId, Guid id) { await service.ReadAsync(Owner, depotId, id, false); return Ok(ApiResponse<string>.Ok("Đã đánh dấu chưa đọc.")); }
    [HttpPatch("notifications/read-all")]
    public async Task<IActionResult> ReadAll(Guid depotId) => Ok(ApiResponse<int>.Ok(await service.ReadAllAsync(Owner, depotId)));
    [HttpGet("batches/{id:guid}/driver-events")]
    public async Task<IActionResult> Events(Guid depotId, Guid id, [FromQuery] DepotQuery query) => Ok(ApiResponse<PagedResult<DepotDriverEventDto>>.Ok(await service.EventsAsync(Owner, depotId, id, query)));
    [HttpGet("driver-events/{id:guid}/followups")]
    public async Task<IActionResult> Followups(Guid depotId, Guid id, [FromQuery] DepotQuery query) => Ok(ApiResponse<PagedResult<IncidentFollowupDto>>.Ok(await service.FollowupsAsync(Owner, depotId, id, query)));
    [HttpPost("driver-events/{id:guid}/followups")]
    public async Task<IActionResult> Followup(Guid depotId, Guid id, CreateIncidentFollowupDto dto)
    { await service.FollowupAsync(Owner, depotId, id, dto); return Ok(ApiResponse<string>.Ok("Đã ghi nội dung theo dõi; trạng thái vận chuyển và tồn kho giữ nguyên.")); }
}
