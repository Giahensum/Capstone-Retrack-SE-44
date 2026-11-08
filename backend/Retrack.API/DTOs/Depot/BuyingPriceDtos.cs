namespace Retrack.API.DTOs.Depot;

public record DepotBuyingPriceDto(string MaterialType, decimal PricePerKg, string ConditionNote, bool IsActive, DateTime UpdatedAt);
public record SaveDepotBuyingPriceDto(decimal PricePerKg, string ConditionNote, bool IsActive, DateTime? ExpectedUpdatedAt);
