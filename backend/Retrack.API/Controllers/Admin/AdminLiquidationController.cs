using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Liquidation;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Admin;

/// <summary>Admin xử lý báo bỏ mua của phiên nhiều hạng mục: chỉ quyết định tiếp tục hoặc hủy, có ghi chú và lịch sử.</summary>
[ApiController, Authorize(Roles = "ADMIN"), Route("api/admin/liquidations")]
public sealed class AdminLiquidationController(IAdminLiquidationService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("disputes")]
    public async Task<IActionResult> Disputes(bool openOnly = true, int page = 1, int pageSize = 20) =>
        Ok(ApiResponse<PagedResult<LiquidationDisputeDto>>.Ok(await service.DisputesAsync(openOnly, page, pageSize)));

    [HttpPost("disputes/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, LiquidationDisputeResolveInput dto) =>
        Ok(ApiResponse<LiquidationDisputeDto>.Ok(await service.ResolveAsync(Actor, id, dto), "Đã lưu quyết định xử lý."));
}
