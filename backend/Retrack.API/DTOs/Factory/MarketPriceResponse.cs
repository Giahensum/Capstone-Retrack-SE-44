namespace Retrack.API.DTOs.Factory;

public sealed record MarketPriceResponse
{
    public required string MaterialType { get; init; }
    public required decimal PricePerKg { get; init; }
    public required DateTime EffectiveDate { get; init; }
    public required string? Source { get; init; }
}
