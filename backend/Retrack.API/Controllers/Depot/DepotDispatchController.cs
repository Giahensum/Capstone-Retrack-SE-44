using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
namespace Retrack.API.Controllers.Depot;

[ApiController, Authorize(Roles = "DEPOT_OWNER"), Route("api/depot/{depotId:guid}/employee-dispatch")]
public sealed class DepotDispatchController(DepotDispatchService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("areas")] public async Task<IActionResult> Areas(Guid depotId, [FromQuery] DepotQuery query) => Ok(ApiResponse<PagedResult<DispatchAreaDto>>.Ok(await service.AreasAsync(Owner, depotId, query)));
    [HttpGet("shifts")] public async Task<IActionResult> Shifts(Guid depotId, [FromQuery] DepotQuery query, bool availableOnly = false, Guid? excludeEmployeeId = null) => Ok(ApiResponse<PagedResult<DispatchShiftDto>>.Ok(await service.ShiftsAsync(Owner, depotId, query, availableOnly, excludeEmployeeId)));
    [HttpGet("pickups")] public async Task<IActionResult> Pickups(Guid depotId, [FromQuery] DepotQuery query) => Ok(ApiResponse<PagedResult<DispatchPickupDto>>.Ok(await service.PickupsAsync(Owner, depotId, query)));
    [HttpGet("summary")] public async Task<IActionResult> Summary(Guid depotId) => Ok(ApiResponse<DispatchSummaryDto>.Ok(await service.SummaryAsync(Owner, depotId)));
    [HttpGet("pickups/{id:guid}")] public async Task<IActionResult> Detail(Guid depotId, Guid id) => Ok(ApiResponse<DispatchPickupDto>.Ok(await service.DetailAsync(Owner, depotId, id)));
    [HttpPut("areas/{id:guid}")] public async Task<IActionResult> Area(Guid depotId, Guid id, DispatchAreaInput input) => Ok(ApiResponse<DispatchAreaDto>.Ok(await service.SaveAreaAsync(Owner, depotId, id, input)));
    [HttpPost("shifts")] public async Task<IActionResult> Shift(Guid depotId, DispatchShiftInput input) => Ok(ApiResponse<object>.Ok(await service.CreateShiftAsync(Owner, depotId, input)));
    [HttpPatch("shifts/{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid depotId, Guid id, DispatchStopInput input)
    { await service.StopShiftAsync(Owner, depotId, id, input.Reason); return Ok(ApiResponse<string>.Ok("Đã ngừng ca. Đơn chưa check-in được điều phối lại ở lượt kế tiếp; đơn đang giữ hàng giữ nguyên người.")); }
    [HttpGet("pickups/{id:guid}/history")] public async Task<IActionResult> History(Guid depotId, Guid id, [FromQuery] DepotQuery query) => Ok(ApiResponse<PagedResult<DispatchHistoryDto>>.Ok(await service.HistoryAsync(Owner, depotId, id, query)));
    [HttpPost("pickups/{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid depotId, Guid id, DispatchAssignInput input)
    { await service.AssignAsync(Owner, depotId, id, input); return Ok(ApiResponse<string>.Ok("Đã lưu phân công và thông báo cho người liên quan.")); }
    // Giữ đường dẫn cấu hình cũ; client mới dùng từng danh sách phân trang ở trên.
    [HttpGet]
    public async Task<IActionResult> Legacy(Guid depotId, int page = 1)
    {
        var areas = await service.AreasAsync(Owner, depotId, new() { PageSize = 100 }); var shifts = await service.ShiftsAsync(Owner, depotId, new() { Page = page }); var waiting = await service.PickupsAsync(Owner, depotId, new() { Page = page });
        return Ok(ApiResponse<object>.Ok(new { areas = areas.Items, shifts = shifts.Items, waiting = waiting.Items, waitingCount = waiting.TotalCount, page, pageSize = 20 }));
    }
}
