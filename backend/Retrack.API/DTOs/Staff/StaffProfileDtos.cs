using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Staff;

public sealed record StaffProfileResponse(
    Guid UserId, string FullName, string Email, string Phone, string Role,
    string? AvatarUrl, Guid StaffId, string EmployeeCode, Guid DepotId,
    string DepotName, string DepotAddress, string StaffType, bool IsActive);

public sealed class UpdateStaffProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^(0[35789]\d{8}|\+84[35789]\d{8})$",
        ErrorMessage = "Số điện thoại Việt Nam không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;
}
