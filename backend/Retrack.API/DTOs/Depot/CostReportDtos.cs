namespace Retrack.API.DTOs.Depot;

public record CostChannelDto(string Channel, int TransactionCount, int IncompleteCount,
    decimal? GrossRevenue, decimal? PlatformFees, decimal? NetRevenue,
    decimal KnownCostOfGoods, decimal SellingCosts, decimal? PretaxDifference,
    decimal OutputVat = 0, decimal? SettlementAmount = null);
public record CostReportDto(int TransactionCount, int IncompleteCount, decimal? GrossRevenue,
    decimal? PlatformFees, decimal? NetRevenue, decimal KnownCostOfGoods, decimal SellingCosts,
    decimal? PretaxDifference, List<CostChannelDto> Channels, string Note,
    decimal OutputVat = 0, decimal? SettlementAmount = null);
