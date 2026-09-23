namespace Retrack.API.Models;

public class Factory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? IndustrialZone { get; set; }
    public string? ContactPhone { get; set; }
    public string? BusinessLicenseUrl { get; set; }
    public string? EnvironmentalLicenseUrl { get; set; }
    public decimal CapacityKgPerMonth { get; set; }
    public decimal MinimumPurityPercent { get; set; }
    public Models.Enums.MaterialType[] AcceptedMaterials { get; set; } = [];
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
