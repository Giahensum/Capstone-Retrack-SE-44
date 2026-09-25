namespace Retrack.API.DTOs.Factory;

public sealed record MarketDepotResponse
{
    public required Guid Id { get; init; }
    public required string CompanyName { get; init; }
    public required string Address { get; init; }
    public required string? ContactPhone { get; init; }
    public required decimal? Latitude { get; init; }
    public required decimal? Longitude { get; init; }
}
