namespace Retrack.API.DTOs.Factory;

public sealed record ProfileResponse
{
    public required Guid Id { get; init; }
    public required string CompanyName { get; init; }
    public required string? TaxCode { get; init; }
    public required string Address { get; init; }
    public required string? IndustrialZone { get; init; }
    public required string? ContactPhone { get; init; }
    public required string? BusinessLicenseUrl { get; init; }
    public required string? EnvironmentalLicenseUrl { get; init; }
    public required decimal CapacityKgPerMonth { get; init; }
    public required decimal MinimumPurityPercent { get; init; }
    public required IReadOnlyList<string> AcceptedMaterials { get; init; }
    public required decimal? Latitude { get; init; }
    public required decimal? Longitude { get; init; }
    public required ProfileOwnerResponse? User { get; init; }
}
