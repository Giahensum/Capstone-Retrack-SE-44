using System.ComponentModel.DataAnnotations;
using Retrack.API.Models.Enums;

namespace Retrack.API.DTOs.Auth;

public class RegisterRequest
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, MinLength(8), MaxLength(100)] public string Password { get; set; } = "";
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [Phone, StringLength(30)] public string? Phone { get; set; }
    [EnumDataType(typeof(UserRole))] public UserRole Role { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}
