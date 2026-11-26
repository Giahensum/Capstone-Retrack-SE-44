using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Liquidation;

// ── Seller tạo phiên nhiều hạng mục: lịch chung cho cả phiên, giá/bước giá/số lượng riêng từng hạng mục ──
public class LiquidationItemInput
{
    [StringLength(30)] public string? MaterialCode { get; set; }
    [Required, StringLength(255)] public string Name { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required, StringLength(10)] public string Unit { get; set; } = "kg";
    public decimal Quantity { get; set; }
    public decimal StartingUnitPrice { get; set; }
    public decimal BidIncrement { get; set; }
}

public class CreateLiquidationV2Dto
{
    public Guid OperationId { get; set; }
    [Required, StringLength(255)] public string Title { get; set; } = "";
    [Required, StringLength(5000)] public string Description { get; set; } = "";
    [Required, StringLength(2000)] public string CollectionTerms { get; set; } = "";
    [Required, StringLength(255)] public string PublicArea { get; set; } = "";
    [Required, StringLength(1000)] public string Address { get; set; } = "";
    [Required, StringLength(20)] public string ContactPhone { get; set; } = "";
    [MaxLength(10)] public List<string> ImageUrls { get; set; } = [];
    public DateTime SurveyStart { get; set; }
    public DateTime SurveyEnd { get; set; }
    public DateTime BiddingStart { get; set; }
    public DateTime BiddingEnd { get; set; }
    /// <summary>Hạn thu gom tính từ lúc đóng phiên thực tế (giờ).</summary>
    public int CollectionWindowHours { get; set; }
    public List<LiquidationItemInput> Items { get; set; } = [];
}

// ── Phía Chủ kho ──
public record LiquidationV2SummaryDto(Guid Id, string Title, string PublicArea, int ItemCount, string[] ItemNames,
    int BidCount, string Phase, string Status, DateTime SurveyStart, DateTime SurveyEnd, DateTime BiddingStart,
    DateTime BiddingEnd, DateTime HardEnd, int ExtensionCount, int ExtensionsLeft, DateTime CollectionDeadline,
    int CollectionWindowHours, string[] ImageUrls, DateTime ServerTime, bool Registered = false, Guid? AgreementId = null,
    int RegisteredItemCount = 0, int WonItemCount = 0);

/// <summary>Một dòng hạng mục: không có danh tính kho dẫn đầu, chỉ cờ "kho mình đang dẫn".</summary>
public record LiquidationV2ItemDto(Guid Id, int Position, string Kind, string? MaterialCode, string Name, string Description,
    string Unit, decimal Quantity, decimal StartingUnitPrice, decimal BidIncrement, decimal? HighestUnitPrice, int BidCount,
    decimal NextMinUnitPrice, string Status, bool Registered, decimal? MyUnitPrice, DateTime? MyBidAt, bool Leading,
    bool WonByMe, decimal? HighestTotal, decimal? MyTotal, decimal StartingTotal);

public record LiquidationV2DetailDto(LiquidationV2SummaryDto Campaign, string Description, string CollectionTerms,
    string? Address, string? ContactPhone, Guid? ParticipationDepotId, bool SurveyDeclared, bool CanRegister, bool CanBid,
    string? CancelReason, List<LiquidationV2ItemDto> Items, Guid? AgreementId);

public record LiquidationV2ParticipationInput(bool Accepted, List<Guid> ItemIds);
public record LiquidationV2BidInput(Guid OperationId, decimal UnitPrice, int? ExpectedBidCount);
public record LiquidationV2BidResultDto(Guid Id, Guid ItemId, decimal UnitPrice, decimal Total, DateTime CreatedAt,
    DateTime BiddingEnd, int ExtensionCount, bool Extended);
public record LiquidationV2MyBidDto(Guid Id, Guid ItemId, decimal UnitPrice, DateTime CreatedAt);

// ── Thỏa thuận theo kho thắng ──
public record LiquidationV2AgreementItemDto(Guid Id, Guid ItemId, string Name, string Unit, decimal Quantity, decimal UnitPrice, decimal Amount);
public record LiquidationV2AgreementDto(Guid Id, Guid LotId, string Title, Guid DepotId, string DepotName, string SellerName,
    string? SellerPhone, string? BuyerName, string? BuyerPhone, string? Address, string? ContactPhone, string CollectionTerms,
    DateTime CollectionDeadline, decimal TotalAmount, List<LiquidationV2AgreementItemDto> Items, string? PaymentReference,
    DateTime? PaymentReportedAt, DateTime? PaymentConfirmedAt, DateTime? SellerHandedOverAt, DateTime? OwnerCollectedAt,
    Guid? ReceiptId, string? ReceiptStatus, string Stage, string? NextAction, DateTime? AbandonReportedAt,
    string? AbandonReason, string? AbandonReporterRole, string? Resolution, string? ResolutionNote, DateTime? ResolvedAt, DateTime CreatedAt);
public record LiquidationV2ReasonInput([Required, StringLength(1000)] string Reason);

/// <summary>Contract nguồn nhập kho cho TV2: tiền mua chốt theo số lượng Seller đã đăng; lượng thực nhập sau phân loại do kho tự ghi.</summary>
public record LiquidationIntakeSourceDto(int FormatVersion, Guid AgreementId, Guid LotId, Guid DepotId, string Revision,
    string Currency, string PricingBasis, bool PaymentConfirmed, bool SellerHandedOver, bool OwnerReceived, bool Cancelled,
    bool ReadyForIntake, decimal FinalPurchaseAmount, Guid? ReceiptId, List<LiquidationIntakeItemDto> Items);
public record LiquidationIntakeItemDto(Guid AgreementItemId, Guid AuctionItemId, string Kind, string? MaterialCode, string Name,
    string Unit, decimal PostedQuantity, decimal WinningUnitPrice, decimal FinalAmount);

// ── Phía Seller ──
public record SellerLiquidationV2ItemDto(Guid Id, int Position, string Kind, string? MaterialCode, string Name, string Description,
    string Unit, decimal Quantity, decimal StartingUnitPrice, decimal BidIncrement, decimal? HighestUnitPrice, int BidCount,
    int ParticipantCount, string Status, string? WinnerDepotName, decimal? Amount);
public record SellerLiquidationV2SummaryDto(LiquidationV2SummaryDto Campaign, int ParticipantCount);
public record SellerLiquidationV2DetailDto(LiquidationV2SummaryDto Campaign, int ParticipantCount, string Description,
    string CollectionTerms, string Address, string ContactPhone, string? CancelReason, bool CanCancel,
    List<SellerLiquidationV2ItemDto> Items, List<LiquidationV2AgreementDto> Agreements);
public record SellerLiquidationV2BidDto(int Rank, string DepotName, decimal UnitPrice, DateTime CreatedAt, bool Leading);

// ── Admin xử lý báo bỏ mua ──
public record LiquidationDisputeDto(Guid AgreementId, Guid LotId, string Title, string SellerName, string DepotName,
    decimal TotalAmount, string Stage, DateTime AbandonReportedAt, string AbandonReporterRole, string AbandonReason,
    string? Resolution, string? ResolutionNote, DateTime? ResolvedAt);
public record LiquidationDisputeResolveInput([Required, StringLength(20)] string Resolution, [Required, StringLength(1000)] string Note);
