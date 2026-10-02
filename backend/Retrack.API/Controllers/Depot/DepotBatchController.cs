using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Depot;

/// <summary>
/// Tạo và quản lý lô xuất hàng
/// </summary>
[ApiController]
[Route("api/depot/batches")]
[Authorize(Roles = "DEPOT_OWNER")]
public class DepotBatchController(IBatchService service) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, [FromQuery] Guid depotId) =>
        Ok(ApiResponse<DepotBatchDetailDto>.Ok(await service.DetailAsync(OwnerId, depotId, id)));
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid depotId, [FromQuery] DepotQuery query) =>
        Ok(ApiResponse<PagedResult<DepotBatchDto>>.Ok(await service.ListAsync(OwnerId, depotId, query)));
    [HttpPost, Consumes("application/json")]
    public async Task<IActionResult> Create([FromQuery] Guid depotId, CreateDepotBatchDto dto) =>
        Ok(ApiResponse<DepotBatchDto>.Ok(await service.CreateAsync(OwnerId, depotId, dto)));
    [HttpPost, Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateWithImages([FromQuery] Guid depotId, [FromForm] CreateDepotBatchDto dto, [FromForm] List<IFormFile> images) =>
        Ok(ApiResponse<DepotBatchDto>.Ok(await service.CreateAsync(OwnerId, depotId, dto, images)));
    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] Guid depotId)
    {
        await service.CancelAsync(OwnerId, depotId, id);
        return Ok(ApiResponse<string>.Ok("Đã hủy lô."));
    }
}


