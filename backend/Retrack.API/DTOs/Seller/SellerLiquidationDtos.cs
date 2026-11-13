using System.ComponentModel.DataAnnotations;
using Retrack.API.DTOs.Liquidation;

namespace Retrack.API.DTOs.Seller;

public class SellerLiquidationQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [StringLength(255)] public string? Search { get; set; }
    public string? Status { get; set; }
}

// OperationId do client sinh một lần cho mỗi form; gửi lại cùng nội dung sẽ trả về cùng lô.
public class CreateLiquidationLotDto
{
    [Required] public Guid OperationId { get; set; }
    [Required, StringLength(255, MinimumLength = 5)] public string Title { get; set; } = "";
    [Required, StringLength(5000, MinimumLength = 10)] public string Description { get; set; } = "";
    [Required, StringLength(3000, MinimumLength = 10)] public string ScopeDetails { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 10)] public string CollectionTerms { get; set; } = "";
    [Required, StringLength(255, MinimumLength = 2)] public string PublicArea { get; set; } = "";
    [Required, StringLength(1000, MinimumLength = 5)] public string Address { get; set; } = "";
    [Required, RegularExpression(@"^[0-9+\-\s]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ.")] public string ContactPhone { get; set; } = "";
    [MaxLength(10)] public List<string> ImageUrls { get; set; } = [];
    public DateTime SurveyStart { get; set; }
    public DateTime SurveyEnd { get; set; }
    public DateTime BiddingStart { get; set; }
    public DateTime BiddingEnd { get; set; }
    public DateTime CollectionDeadline { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal BidIncrement { get; set; }
    // Tỷ lệ cọc (% giá trúng) Seller công bố trước phiên; cọc chuyển ngoài hệ thống.
    public decimal DepositPercent { get; set; }
}

public class CancelLiquidationLotDto
{
    [Required, StringLength(1000, MinimumLength = 5)] public string Reason { get; set; } = "";
}

public record SellerLiquidationSummaryDto(Guid Id, string Title, string PublicArea, decimal StartingPrice,
    decimal BidIncrement, decimal? HighestPrice, int BidCount, int ParticipantCount, string Phase, string Status,
    DateTime SurveyStart, DateTime SurveyEnd, DateTime BiddingStart, DateTime BiddingEnd, DateTime HardEnd,
    DateTime CollectionDeadline, string[] ImageUrls, DateTime ServerTime, decimal DepositPercent);

public record SellerLiquidationBidDto(Guid Id, string DepotName, decimal Amount, DateTime CreatedAt, bool Leading);

public record SellerLiquidationAgreementDto(Guid Id, Guid LotId, string Title, string DepotName, decimal Amount,
    string Stage, string? PaymentReference, DateTime? PaymentReportedAt, DateTime? PaymentConfirmedAt,
    DateTime? OwnerCollectedAt, DateTime? SellerHandedOverAt, DateTime CollectionDeadline,
    decimal DepositPercent, decimal DepositAmount, decimal RemainingAmount, DateTime? SellerSignedAt, DateTime? BuyerSignedAt,
    string? DepositReference, DateTime? DepositReportedAt, DateTime? DepositConfirmedAt,
    DateTime CreatedAt, ContractPartiesDto Parties);

public record SellerLiquidationDetailDto(SellerLiquidationSummaryDto Lot, string Description, string ScopeDetails,
    string CollectionTerms, string Address, string ContactPhone, string? CancelReason, bool CanCancel,
    SellerLiquidationAgreementDto? Agreement);
