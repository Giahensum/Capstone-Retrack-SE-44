using System.ComponentModel.DataAnnotations;
using Retrack.API.Models.Enums;

namespace Retrack.API.DTOs.Factory;

public class PageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public class MarketQuery : PageQuery
{
    [EnumDataType(typeof(MaterialType))] public MaterialType? Material { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    [Range(0, 1000000000)] public decimal? MinWeightKg { get; set; }
    [Range(0, 1000000000)] public decimal? MaxWeightKg { get; set; }
    [Range(0, 20040)] public double? MaxDistanceKm { get; set; }
    public bool DirectOnly { get; set; }
}

public class OrderQuery : PageQuery
{
    [EnumDataType(typeof(BatchStatus))] public BatchStatus? Status { get; set; }
}

public class ProfileRequest
{
    [Required, StringLength(200)] public string CompanyName { get; set; } = "";
    [StringLength(50)] public string? TaxCode { get; set; }
    [StringLength(1000000)] public string? BusinessLicenseUrl { get; set; }
    [StringLength(1000000)] public string? EnvironmentalLicenseUrl { get; set; }
    [Required, StringLength(500)] public string Address { get; set; } = "";
    [StringLength(200)] public string? IndustrialZone { get; set; }
    [Phone, StringLength(30)] public string? ContactPhone { get; set; }
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
    [Range(0.01, 1000000000)] public decimal CapacityKgPerMonth { get; set; }
    [Range(0, 100)] public decimal MinimumPurityPercent { get; set; }
    [Required, MinLength(1), MaxLength(11)] public MaterialType[] AcceptedMaterials { get; set; } = [];
}

public class DemandRequest
{
    [EnumDataType(typeof(MaterialType))] public MaterialType MaterialType { get; set; }
    [Range(0.01, 1000000000)] public decimal QuantityKg { get; set; }
    [Range(0, 1000000000)] public decimal? MinPricePerKg { get; set; }
    [Range(0, 1000000000)] public decimal? MaxPricePerKg { get; set; }
    public DateTime? Deadline { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(2000)] public string? Note { get; set; }
}

public class WeighRequest
{
    [Range(0.01, 1000000000)] public decimal GrossWeightKg { get; set; }
    [Range(0, 1000000000)] public decimal TareWeightKg { get; set; }
    [StringLength(100)] public string? TicketNumber { get; set; }
    [StringLength(1000000)] public string? TicketImageUrl { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
}

public class QualityRequest
{
    public bool Accept { get; set; }
    [Range(0, 100)] public decimal PurityPercent { get; set; }
    [Range(0, 100)] public decimal MoisturePercent { get; set; }
    [Range(0, 100)] public decimal ContaminationPercent { get; set; }
    [Required, RegularExpression("^[ABC]$")] public string Grade { get; set; } = "";
    [StringLength(2000)] public string? Note { get; set; }
    [RegularExpression("^(RETURN|RENEGOTIATE)?$")] public string? Resolution { get; set; }
}

public class DecisionRequest
{
    public bool Accept { get; set; }
    [StringLength(2000)] public string? Reason { get; set; }
}

public class SettlementRequest
{
    [Range(0, 1000000000)] public decimal AgreedPricePerKg { get; set; }
    [Required, StringLength(200)] public string PaymentReference { get; set; } = "";
}

public class PartnerRequest
{
    [EnumDataType(typeof(PartnershipStatus))] public PartnershipStatus Status { get; set; }
    [Range(1, 5)] public int? Rating { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
}

public class InvoiceRequest
{
    [StringLength(100)] public string? InvoiceNumber { get; set; }
    [Required, StringLength(1000000)] public string InvoiceFileUrl { get; set; } = "";
    [Range(0, 1000000000000)] public decimal? VatAmount { get; set; }
}

public class BatchOfferRequest
{
    // Factory receives a batch before KCS; final price is agreed at settlement.
    [Range(0, 1000000000)] public decimal AgreedPricePerKg { get; set; }
}

public class RatingRequest
{
    [Range(1, 5)] public int Rating { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    public bool BlockPartner { get; set; }
}
