using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers;

/// <summary>Giá và thời gian dùng thử của gói quản lý Depot cho trang giới thiệu công khai (không cần đăng nhập).</summary>
[ApiController, AllowAnonymous, Route("api/public/depot-plan")]
public sealed class PublicPlanController(IDepotSubscriptionService service) : ControllerBase
{
    [HttpGet, ResponseCache(Duration = 60)]
    public async Task<IActionResult> Get() => Ok(ApiResponse<PublicDepotPlanDto>.Ok(await service.PublicPlanAsync()));
}
