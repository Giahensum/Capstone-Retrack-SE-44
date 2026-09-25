namespace Retrack.API.DTOs.Factory;

public sealed record QualityResponse
{
    public required Guid Id { get; init; }
    public required string Status { get; init; }
    public required decimal? PurityPercent { get; init; }
    public required decimal? MoisturePercent { get; init; }
    public required decimal? ContaminationPercent { get; init; }
    public required string Grade { get; init; }
    public required bool IsAccepted { get; init; }
}
