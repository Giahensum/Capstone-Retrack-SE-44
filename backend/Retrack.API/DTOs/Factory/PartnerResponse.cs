namespace Retrack.API.DTOs.Factory;

public sealed record PartnerResponse
{
    public required Guid Id { get; init; }
    public required Guid DepotId { get; init; }
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required string? ContactPhone { get; init; }
    public required string Status { get; init; }
    public required decimal Rating { get; init; }
    public required int? LatestRating { get; init; }
    public required string? LatestComment { get; init; }
    public required int ReviewCount { get; init; }
    public required int OrderCount { get; init; }
    public required int CompletedOrderCount { get; init; }
}
