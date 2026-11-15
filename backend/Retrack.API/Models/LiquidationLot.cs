using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("liquidation_lots")]
public class LiquidationLot
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("seller_id")] public Guid SellerId { get; set; }
    [ForeignKey(nameof(SellerId))] public User Seller { get; set; } = null!;
    [Column("title"), MaxLength(255)] public string Title { get; set; } = "";
    [Column("description"), MaxLength(5000)] public string Description { get; set; } = "";
    [Column("scope_details"), MaxLength(3000)] public string ScopeDetails { get; set; } = "";
    [Column("collection_terms"), MaxLength(2000)] public string CollectionTerms { get; set; } = "";
    [Column("public_area"), MaxLength(255)] public string PublicArea { get; set; } = "";
    [Column("address"), MaxLength(1000)] public string Address { get; set; } = "";
    [Column("contact_phone"), MaxLength(20)] public string ContactPhone { get; set; } = "";
    [Column("image_urls")] public string[] ImageUrls { get; set; } = [];
    [Column("survey_start")] public DateTime SurveyStart { get; set; }
    [Column("survey_end")] public DateTime SurveyEnd { get; set; }
    [Column("bidding_start")] public DateTime BiddingStart { get; set; }
    [Column("original_end")] public DateTime OriginalEnd { get; set; }
    [Column("bidding_end")] public DateTime BiddingEnd { get; set; }
    [Column("collection_deadline")] public DateTime CollectionDeadline { get; set; }
    [Column("starting_price", TypeName = "numeric(18,0)")] public decimal StartingPrice { get; set; }
    [Column("bid_increment", TypeName = "numeric(18,0)")] public decimal BidIncrement { get; set; }
    [Column("deposit_percent", TypeName = "numeric(5,2)")] public decimal DepositPercent { get; set; }
    [Column("highest_price", TypeName = "numeric(18,0)")] public decimal? HighestPrice { get; set; }
    [Column("highest_bid_id")] public Guid? HighestBidId { get; set; }
    [Column("bid_count")] public int BidCount { get; set; }
    [Column("status"), MaxLength(20)] public string Status { get; set; } = "PUBLISHED";
    [Column("cancel_reason"), MaxLength(1000)] public string? CancelReason { get; set; }
    [Column("request_hash"), MaxLength(64)] public string RequestHash { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("liquidation_participations")]
public class LiquidationParticipation
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("lot_id")] public Guid LotId { get; set; }
    [ForeignKey(nameof(LotId))] public LiquidationLot Lot { get; set; } = null!;
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [ForeignKey(nameof(DepotId))] public Depot Depot { get; set; } = null!;
    [Column("registered_at")] public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    [Column("survey_declared_at")] public DateTime? SurveyDeclaredAt { get; set; }
}

[Table("liquidation_bids")]
public class LiquidationBid
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("lot_id")] public Guid LotId { get; set; }
    [ForeignKey(nameof(LotId))] public LiquidationLot Lot { get; set; } = null!;
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

[Table("liquidation_agreements")]
public class LiquidationAgreement
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("lot_id")] public Guid LotId { get; set; }
    [ForeignKey(nameof(LotId))] public LiquidationLot Lot { get; set; } = null!;
    [Column("winning_bid_id")] public Guid WinningBidId { get; set; }
    [ForeignKey(nameof(WinningBidId))] public LiquidationBid WinningBid { get; set; } = null!;
    [Column("depot_id")] public Guid DepotId { get; set; }
    [ForeignKey(nameof(DepotId))] public Depot Depot { get; set; } = null!;
    [Column("buyer_id")] public Guid BuyerId { get; set; }
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
    [Column("payment_reference"), MaxLength(255)] public string? PaymentReference { get; set; }
    [Column("payment_reported_at")] public DateTime? PaymentReportedAt { get; set; }
    [Column("payment_confirmed_at")] public DateTime? PaymentConfirmedAt { get; set; }
    [Column("owner_collected_at")] public DateTime? OwnerCollectedAt { get; set; }
    [Column("seller_handed_over_at")] public DateTime? SellerHandedOverAt { get; set; }
    [Column("deposit_amount", TypeName = "numeric(18,0)")] public decimal DepositAmount { get; set; }
    [Column("seller_signed_at")] public DateTime? SellerSignedAt { get; set; }
    [Column("buyer_signed_at")] public DateTime? BuyerSignedAt { get; set; }
    [Column("deposit_reference"), MaxLength(255)] public string? DepositReference { get; set; }
    [Column("deposit_reported_at")] public DateTime? DepositReportedAt { get; set; }
    [Column("deposit_confirmed_at")] public DateTime? DepositConfirmedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
