using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Depot;

public sealed class UpdateDepotProfileDto : IValidatableObject
{
    [Required, StringLength(255)] public string Name { get; set; } = string.Empty;
    [Required] public string Address { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    [Phone, StringLength(20)] public string? ContactPhone { get; set; }
    public string? Description { get; set; }
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Latitude.HasValue != Longitude.HasValue) yield return new("Cần nhập cả vĩ độ và kinh độ.", [nameof(Latitude), nameof(Longitude)]);
    }
}
public record DepotProfileDto(Guid Id, string Name, string Address, string? TaxCode,
    string? ContactPhone, string? Description, decimal? Latitude, decimal? Longitude, decimal Rating, string Email);
public record StaffDto(Guid Id, Guid UserId, string FullName, string Email, string Phone, string Role, bool IsActive);
public sealed class CreateStaffDto
{
    [Required, StringLength(255)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; } = string.Empty;
    [Required, Phone, StringLength(20)] public string Phone { get; set; } = string.Empty;
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required, RegularExpression("^(DEPOT_EMPLOYEE|DRIVER)$")] public string Role { get; set; } = string.Empty;
}
public sealed class UpdateStaffDto
{
    [Required, StringLength(255)] public string FullName { get; set; } = string.Empty;
    [Required, Phone, StringLength(20)] public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
public record FactoryPartnerDto(Guid Id, string Name, string Address, decimal Rating, string? PartnershipStatus);
public record DemandDto(Guid Id, Guid FactoryId, string FactoryName, string MaterialType, decimal RequiredWeightKg,
    decimal? MinPricePerKg, decimal? MaxPricePerKg, DateTime Deadline);
