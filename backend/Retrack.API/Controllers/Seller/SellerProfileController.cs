using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Controllers.Seller;

[ApiController]
[Route("api/seller/profile")]
[Authorize(Roles = "SELLER")]
public class SellerProfileController : ControllerBase
{
    private readonly ISellerService _sellerService;

    public SellerProfileController(ISellerService sellerService)
    {
        _sellerService = sellerService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Lấy thông tin profile Seller</summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await _sellerService.GetProfileAsync(GetUserId());
        if (profile == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy thông tin tài khoản."));

        return Ok(ApiResponse<SellerProfileDto>.Ok(profile));
    }

    /// <summary>Cập nhật profile Seller</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateSellerProfileDto dto)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponse<string>.Fail(firstError ?? "Dữ liệu không hợp lệ."));
        }

        try
        {
            var updated = await _sellerService.UpdateProfileAsync(GetUserId(), dto);
            if (!updated) return NotFound(ApiResponse<string>.Fail("Không tìm thấy thông tin tài khoản."));

            return Ok(ApiResponse<string>.Ok("Cập nhật thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
