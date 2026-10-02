using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;

namespace Retrack.API.Controllers.Seller;

[ApiController]
[Route("api/seller/profile")]
[Authorize(Roles = "SELLER")]
public class SellerProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public SellerProfileController(AppDbContext db)
    {
        _db = db;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Lấy thông tin profile Seller</summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.FindAsync(GetUserId());
        if (user == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));

        return Ok(ApiResponse<object>.Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.Phone,
            user.Role,
            user.IsActive,
            user.CreatedAt,
        }));
    }

    /// <summary>Cập nhật profile Seller</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateSellerProfileDto dto)
    {
        var user = await _db.Users.FindAsync(GetUserId());
        if (user == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy."));

        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName;
        if (!string.IsNullOrWhiteSpace(dto.Phone)) user.Phone = dto.Phone;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Cập nhật thành công."));
    }
}
