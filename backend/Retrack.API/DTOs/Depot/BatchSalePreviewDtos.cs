namespace Retrack.API.DTOs.Depot;

public record BatchSalePreviewInput(string MaterialType, decimal WeightKg, decimal? PricePerKg, decimal SellingCost = 0);
public record BatchSalePreviewDto(decimal WeightKg, bool HasCompleteCost, decimal? CostOfGoods, decimal SellingCost,
    decimal? Revenue, decimal? EstimatedDifference, decimal? BreakEvenPricePerKg, int SourceCount);
