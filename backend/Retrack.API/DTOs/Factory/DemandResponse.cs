namespace Retrack.API.DTOs.Factory;

public sealed record DemandResponse
{
    public required Guid Id { get; init; }
    public required string MaterialType { get; init; }
    public required decimal QuantityKg { get; init; }
    public required decimal? MinPricePerKg { get; init; }
    public required decimal? MaxPricePerKg { get; init; }
    public required DateTime Deadline { get; init; }
    public required bool IsActive { get; init; }
    public required string? Note { get; init; }
    public required DateTime CreatedAt { get; init; }
}
