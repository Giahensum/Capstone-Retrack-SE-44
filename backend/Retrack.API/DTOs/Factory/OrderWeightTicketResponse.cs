namespace Retrack.API.DTOs.Factory;

public sealed record OrderWeightTicketResponse
{
    public required string? TicketNumber { get; init; }
    public required decimal? GrossWeightKg { get; init; }
    public required decimal? TareWeightKg { get; init; }
    public required decimal NetWeightKg { get; init; }
    public required string? TicketImageUrl { get; init; }
}
