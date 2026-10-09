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
            user.BankName,
            user.BankAccountNumber,
            user.BankAccountName,
            user.BankQrUrl,
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
        
        // Cập nhật thông tin ngân hàng (có thể null nếu xoá)
        if (dto.BankName != null) user.BankName = dto.BankName;
        if (dto.BankAccountNumber != null) user.BankAccountNumber = dto.BankAccountNumber;
        if (dto.BankAccountName != null) user.BankAccountName = dto.BankAccountName;
        if (dto.BankQrUrl != null) user.BankQrUrl = dto.BankQrUrl;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Cập nhật thành công."));
    }
}
