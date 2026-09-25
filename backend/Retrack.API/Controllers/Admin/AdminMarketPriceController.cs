using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers.Admin;

/// <summary>
/// UC-7.7 — Quản lý bảng giá thị trường
/// </summary>
[ApiController]
[Route("api/admin/market-prices")]
[Authorize(Roles = "ADMIN")]
public class AdminMarketPriceController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminMarketPriceController(IAdminService adminService) => _adminService = adminService;

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? materialType)
    {
        var result = await _adminService.GetMarketPricesAsync(materialType);
        return Ok(ApiResponse<List<MarketPriceDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertMarketPriceDto dto)
    {
        var result = await _adminService.CreateMarketPriceAsync(dto, GetUserId());
        return Ok(ApiResponse<MarketPriceDto>.Ok(result, "Đã thêm giá tham khảo."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertMarketPriceDto dto)
    {
        var result = await _adminService.UpdateMarketPriceAsync(id, dto, GetUserId());
        return Ok(ApiResponse<MarketPriceDto>.Ok(result, "Đã cập nhật giá tham khảo."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _adminService.DeleteMarketPriceAsync(id, GetUserId());
        return Ok(ApiResponse<string>.Ok(string.Empty, "Đã xóa giá tham khảo."));
    }
}
