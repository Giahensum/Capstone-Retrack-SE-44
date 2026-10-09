using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Depot;

public class DepotQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [StringLength(255)] public string? Search { get; set; }
    public string? Status { get; set; }
    [RegularExpression("^(newest|oldest|amount)$")] public string Sort { get; set; } = "newest";
}

public sealed class FactorySearchQuery : DepotQuery
{
    [Range(1, 2000)] public double? MaxDistanceKm { get; set; }
    public bool NearestFirst { get; set; }
    [StringLength(100)] public string? MaterialType { get; set; }
}

public record DepotSummaryDto(Guid Id, string Name);
public record PaymentItemDto(string MaterialType, decimal WeightKg, decimal PricePerKg, decimal SubTotal);
public record DepotPaymentDto(Guid Id, Guid DepotId, string SellerName, string SellerPhone,
    string? CollectorName, string Address, string Status, decimal GrossAmount,
    decimal PlatformFeePercentage, decimal PlatformFeeAmount, decimal NetAmount,
    string? PaymentProofUrl, string? CheckinImageUrl, DateTime CreatedAt, List<PaymentItemDto> Items);
public record PaymentSummaryDto(int PendingCount, decimal PendingAmount, int SentTodayCount, decimal SentTodayAmount);

public sealed class PaymentProofDto
{
    [Required, Url] public string PaymentProofUrl { get; set; } = string.Empty;
}
