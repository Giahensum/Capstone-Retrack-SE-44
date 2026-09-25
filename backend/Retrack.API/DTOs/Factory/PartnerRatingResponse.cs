namespace Retrack.API.DTOs.Factory;

public sealed record PartnerRatingResponse
{
    public required Guid Id { get; init; }
    public required int? Rating { get; init; }
    public required string? Comment { get; init; }
}
