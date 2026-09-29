using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Staff;
using Retrack.API.Services.Staff;

namespace Retrack.API.Controllers.Shared;

// Explicit role policies prevent using a driver's token against employee routes (and vice versa).
[ApiController]
public abstract class StaffProfileController(IStaffProfileService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role)!;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponse<StaffProfileResponse>.Ok(await service.GetAsync(UserId, Role, ct)));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateStaffProfileRequest request, CancellationToken ct) =>
        Ok(ApiResponse<StaffProfileResponse>.Ok(await service.UpdateAsync(UserId, Role, request, ct), "Đã cập nhật hồ sơ."));

    [HttpPost("avatar")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Avatar(IFormFile file, CancellationToken ct) =>
        Ok(ApiResponse<StaffProfileResponse>.Ok(await service.UploadAvatarAsync(UserId, Role, file, ct), "Đã cập nhật ảnh đại diện."));
}

[Authorize(Roles = "DEPOT_EMPLOYEE")]
[Route("api/employee/profile")]
public sealed class EmployeeProfileController(IStaffProfileService service) : StaffProfileController(service);

[Authorize(Roles = "DRIVER")]
[Route("api/driver/profile")]
public sealed class DriverProfileController(IStaffProfileService service) : StaffProfileController(service);
