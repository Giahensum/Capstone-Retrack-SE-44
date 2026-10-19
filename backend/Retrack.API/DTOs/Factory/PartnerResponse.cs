namespace Retrack.API.DTOs.Factory;

public sealed record PartnerResponse
{
    public bool BlockedByFactory { get; init; }
    public bool BlockedByDepot { get; init; }
    public bool LegacyBlocked { get; init; }
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
    public string? ContractCode { get; init; }
    public int ContractVersion { get; init; }
    public string? ContractTerms { get; init; }
    public string? ContractTermsHash { get; init; }
    public string? ContractStatus { get; init; }
    public DateTime? FactorySignedAt { get; init; }
    public DateTime? DepotSignedAt { get; init; }
    public DateTime? ContractEffectiveAt { get; init; }
    public DateTime? ContractExpiresAt { get; init; }
    public string? TerminationStatus { get; init; }
    public Guid? TerminationRequestedBy { get; init; }
    public DateTime? TerminationRequestedAt { get; init; }
    public string? TerminationReason { get; init; }
    public DateTime? TerminatedAt { get; init; }
}
