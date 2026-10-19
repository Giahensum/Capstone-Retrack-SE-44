using System.ComponentModel.DataAnnotations;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.DTOs.Liquidation;

public class LiquidationQuery : DepotQuery
{
    public bool ParticipatingOnly { get; set; }
}
public record LiquidationSummaryDto(Guid Id, string Title, string PublicArea, decimal StartingPrice,
    decimal BidIncrement, decimal? HighestPrice, int BidCount, string Phase, string Status,
    DateTime SurveyStart, DateTime SurveyEnd, DateTime BiddingStart, DateTime BiddingEnd,
    DateTime HardEnd, DateTime CollectionDeadline, string[] ImageUrls, DateTime ServerTime);
public record MyLiquidationBidDto(Guid Id, decimal Amount, DateTime CreatedAt);
public record LiquidationDetailDto(LiquidationSummaryDto Lot, string Description, string ScopeDetails,
    string CollectionTerms, string? Address, string? ContactPhone, Guid? ParticipationDepotId,
    bool SurveyDeclared, bool Leading, bool CanBid, string? CancelReason, Guid? AgreementId);
public record LiquidationBidInput(Guid OperationId, decimal Amount);
public record LiquidationConsentInput(bool Accepted);
public record LiquidationPaymentInput([Required, StringLength(255)] string Reference);
public record LiquidationAgreementDto(Guid Id, Guid LotId, string Title, Guid DepotId, decimal Amount,
    string ScopeDetails, string CollectionTerms, string Address, string ContactPhone,
    DateTime CollectionDeadline, string? PaymentReference, DateTime? PaymentReportedAt,
    DateTime? PaymentConfirmedAt, DateTime? OwnerCollectedAt, DateTime? SellerHandedOverAt,
    Guid? ReceiptId, string? ReceiptStatus);
