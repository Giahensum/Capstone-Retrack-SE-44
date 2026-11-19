using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;

namespace Retrack.API.Controllers.Employee;

[ApiController, Authorize(Roles = "DEPOT_EMPLOYEE")]
public class EmployeePickupController(EmployeePickupService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

    [HttpGet("/api/employee/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(ApiResponse<EmployeeDashboardDto>.Ok(await service.DashboardAsync(UserId, ct)));

    [HttpGet("/api/employee/pickup-pool")]
    public Task<IActionResult> Pool(CancellationToken ct, [FromQuery] int page = 1) => Respond(async () =>
    {
        var result = await service.PoolAsync(UserId, page, ct);
        return result.Automatic
            ? Ok(ApiResponse<object>.Ok(new { items = result.Items, page, pageSize = 20, total = result.Total, automatic = true }))
            : Ok(ApiResponse<List<PickupPoolItemDto>>.Ok(result.Items));
    });

    [HttpGet("/api/employee/pickups/active")]
    public Task<IActionResult> Active([FromQuery] int page = 1, CancellationToken ct = default) => Respond(async () =>
        Ok(ApiResponse<object>.Ok(await service.ActiveAsync(UserId, page, ct))));

    [HttpGet("/api/employee/pickup/{pickupId:guid}")]
    public Task<IActionResult> Detail(Guid pickupId, CancellationToken ct) => Respond(async () =>
        Ok(ApiResponse<EmployeePickupDetailDto>.Ok(await service.DetailAsync(UserId, pickupId, ct))));

    [HttpPost("/api/employee/pickup/{pickupId:guid}/accept")]
    public Task<IActionResult> Accept(Guid pickupId, CancellationToken ct) => Respond(async () =>
        Ok(ApiResponse<PickupPoolItemDto>.Ok(await service.AcceptAsync(UserId, pickupId, ct), "Đã nhận đơn thành công.")));

    // Giữ nguyên loại phản hồi lỗi HTTP của các endpoint cũ khi gọi trực tiếp controller.
    private async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (DepotConflictException ex) { return Conflict(ApiResponse<object>.Fail(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.Fail(ex.Message)); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }
}
