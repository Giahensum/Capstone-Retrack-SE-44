namespace Retrack.API.DTOs.Factory;

public sealed record OrderWeightVerificationResponse
{
    public required decimal DepotWeightKg { get; init; }
    public required decimal FactoryWeightKg { get; init; }
    public required decimal DifferencePercentage { get; init; }
    public required bool IsVerified { get; init; }
    public required decimal? PurityPercent { get; init; }
    public required decimal? MoisturePercent { get; init; }
    public required decimal? ContaminationPercent { get; init; }
    public required string? Grade { get; init; }
    public required string? QualityNote { get; init; }
    public required string? Note { get; init; }
}
