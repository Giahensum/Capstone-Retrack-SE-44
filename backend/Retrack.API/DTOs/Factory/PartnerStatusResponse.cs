namespace Retrack.API.DTOs.Factory;

public sealed record PartnerStatusResponse
{
    public required Guid Id { get; init; }
    public required Guid DepotId { get; init; }
    public required string Status { get; init; }
}
