namespace Retrack.API.DTOs.Depot;

public record DepotPartnershipDto(Guid Id, Guid FactoryId, string FactoryName, string? ContactPhone, string Status, DateTime CreatedAt, DateTime UpdatedAt,
    bool BlockedByDepot = false, bool BlockedByFactory = false, bool LegacyBlocked = false,
    string? ContractCode = null, int ContractVersion = 0, string? ContractTerms = null, string? ContractTermsHash = null,
    string? ContractStatus = null, DateTime? FactorySignedAt = null, DateTime? DepotSignedAt = null,
    DateTime? ContractEffectiveAt = null, DateTime? ContractExpiresAt = null,
    string? TerminationStatus = null, Guid? TerminationRequestedBy = null, DateTime? TerminationRequestedAt = null,
    string? TerminationReason = null, DateTime? TerminatedAt = null);
public record DepotPartnershipStatusDto(Guid Id, Guid FactoryId, Guid DepotId, string Status, string? ContractCode = null, string? ContractStatus = null, string? TerminationStatus = null);
public record DepotContractDecisionRequest(string Decision, bool ConfirmElectronicSignature);
public record DepotContractTerminationRequest(string Reason);
public record DepotContractTerminationDecisionRequest(string Decision);
