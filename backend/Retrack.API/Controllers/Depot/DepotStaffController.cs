using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>
/// Quản lý thông tin nhân viên thu gom và tài xế
/// </summary>
[ApiController]
[Route("api/depot/staff")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotStaffController(IStaffService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<StaffDto>>.Ok(await service.ListAsync(OwnerId, depotId, query)));
    [HttpPost]
    public async Task<IActionResult> Create([FromQuery] Guid depotId, CreateStaffDto dto) =>
        Ok(ApiResponse<StaffDto>.Ok(await service.CreateAsync(OwnerId, depotId, dto)));
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] Guid depotId, UpdateStaffDto dto) =>
        Ok(ApiResponse<StaffDto>.Ok(await service.UpdateAsync(OwnerId, depotId, id, dto)));
}


