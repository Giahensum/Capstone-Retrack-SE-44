namespace Retrack.API.DTOs.Factory;

public sealed record WeighResponse
{
    public required Guid Id { get; init; }
    public required string Status { get; init; }
    public required decimal GrossWeightKg { get; init; }
    public required decimal TareWeightKg { get; init; }
    public required decimal NetWeightKg { get; init; }
    public required decimal DepotWeightKg { get; init; }
    public required decimal DifferencePercentage { get; init; }
    public required bool Flagged { get; init; }
}
