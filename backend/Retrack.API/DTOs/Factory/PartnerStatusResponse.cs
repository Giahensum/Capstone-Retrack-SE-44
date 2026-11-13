namespace Retrack.API.DTOs.Factory;

public sealed record PartnerStatusResponse
{
    public required Guid Id { get; init; }
    public required Guid DepotId { get; init; }
    public required string Status { get; init; }
    public string? ContractCode { get; init; }
    public string? ContractStatus { get; init; }
    public string? TerminationStatus { get; init; }
}
