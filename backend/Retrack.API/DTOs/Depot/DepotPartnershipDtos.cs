namespace Retrack.API.DTOs.Depot;

public record DepotPartnershipDto(Guid Id, Guid FactoryId, string FactoryName, string? ContactPhone, string Status, DateTime CreatedAt, DateTime UpdatedAt);
public record DepotPartnershipStatusDto(Guid Id, Guid FactoryId, Guid DepotId, string Status);
