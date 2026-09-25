namespace Retrack.API.DTOs.Factory;

public sealed record MarketBatchResponse
{
    public required Guid Id { get; init; }
    public required string BatchCode { get; init; }
    public required string MaterialType { get; init; }
    public required decimal EstimatedWeightKg { get; init; }
    public required decimal? UnitPrice { get; init; }
    public required string? Description { get; init; }
    public required string? ThumbnailImageUrl { get; init; }
    public required IReadOnlyList<string> ImageUrls { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required MarketDepotResponse Depot { get; init; }
    public required bool IsDirectOffer { get; init; }
}
