using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services;

namespace Retrack.API.Controllers.Admin;

/// <summary>
/// UC-7.2 / UC-7.3 / UC-7.4 — Quản lý người dùng (CRUD, tìm kiếm, xem chi tiết theo role)
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService) => _adminService = adminService;

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Tìm kiếm & lọc danh sách người dùng (UC-7.2, UC-7.3)</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] string? role, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.SearchUsersAsync(keyword, role, isActive, page, pageSize);
        return Ok(ApiResponse<PagedResult<UserListItemDto>>.Ok(result));
    }

    /// <summary>Xem chi tiết người dùng theo role (UC-7.4)</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _adminService.GetUserDetailAsync(id);
        if (result == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy người dùng."));
        return Ok(ApiResponse<UserDetailDto>.Ok(result));
    }

    /// <summary>Tạo người dùng mới (UC-7.2)</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var result = await _adminService.CreateUserAsync(dto, GetUserId());
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<UserDetailDto>.Ok(result, "Tạo người dùng thành công."));
    }

    /// <summary>Cập nhật thông tin người dùng (UC-7.2)</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var result = await _adminService.UpdateUserAsync(id, dto, GetUserId());
        return Ok(ApiResponse<UserDetailDto>.Ok(result, "Cập nhật thành công."));
    }

    /// <summary>Vô hiệu hóa / kích hoạt lại tài khoản (UC-7.2)</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveBody body)
    {
        var result = await _adminService.SetUserActiveAsync(id, body.IsActive, GetUserId());
        return Ok(ApiResponse<UserDetailDto>.Ok(result, body.IsActive ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản."));
    }

    /// <summary>Xóa vĩnh viễn người dùng — chỉ khi không còn dữ liệu liên quan (UC-7.2)</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _adminService.DeleteUserAsync(id, GetUserId());
        return Ok(ApiResponse<string>.Ok(string.Empty, "Đã xóa người dùng."));
    }

    public record SetActiveBody(bool IsActive);
}
