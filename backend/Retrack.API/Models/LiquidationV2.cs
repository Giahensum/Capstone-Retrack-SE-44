using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

// Phiên thanh lý nhiều hạng mục (V2): mỗi hạng mục có đơn vị, số lượng, giá khởi điểm/bước giá và người thắng riêng.
[Table("liquidation_items")]
public class LiquidationItem
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("lot_id")] public Guid LotId { get; set; }
    [ForeignKey(nameof(LotId))] public LiquidationLot Lot { get; set; } = null!;
    [Column("position")] public short Position { get; set; }
    [Column("kind"), MaxLength(20)] public string Kind { get; set; } = "MATERIAL";
    [Column("material_code"), MaxLength(30)] public string? MaterialCode { get; set; }
    [Column("name"), MaxLength(255)] public string Name { get; set; } = "";
    [Column("description"), MaxLength(1000)] public string Description { get; set; } = "";
    [Column("unit"), MaxLength(10)] public string Unit { get; set; } = "kg";
    [Column("quantity", TypeName = "numeric(18,3)")] public decimal Quantity { get; set; }
    [Column("starting_unit_price", TypeName = "numeric(18,0)")] public decimal StartingUnitPrice { get; set; }
    [Column("bid_increment", TypeName = "numeric(18,0)")] public decimal BidIncrement { get; set; }
    [Column("highest_unit_price", TypeName = "numeric(18,0)")] public decimal? HighestUnitPrice { get; set; }
    [Column("highest_bid_id")] public Guid? HighestBidId { get; set; }
    [Column("bid_count")] public int BidCount { get; set; }
    [Column("status"), MaxLength(10)] public string Status { get; set; } = "OPEN";
}

[Table("liquidation_participation_items")]
public class LiquidationParticipationItem
{
    [Column("participation_id")] public Guid ParticipationId { get; set; }
    [ForeignKey(nameof(ParticipationId))] public LiquidationParticipation Participation { get; set; } = null!;
    [Column("item_id")] public Guid ItemId { get; set; }
    [Column("registered_at")] public DateTime RegisteredAt { get; set; }
}

// Lượt trả giá V2 ghi đơn giá của một hạng mục (không phải tổng tiền); bất biến sau khi ghi.
[Table("liquidation_item_bids")]
public class LiquidationItemBid
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("lot_id")] public Guid LotId { get; set; }
    [Column("item_id")] public Guid ItemId { get; set; }
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("unit_price", TypeName = "numeric(18,0)")] public decimal UnitPrice { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("extended_end")] public DateTime? ExtendedEnd { get; set; }
}

// Ảnh chụp bất biến của hạng mục thắng tại lúc đóng phiên: tiền = đơn giá trúng x số lượng đã đăng.
[Table("liquidation_agreement_items")]
public class LiquidationAgreementItem
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("agreement_id")] public Guid AgreementId { get; set; }
    [ForeignKey(nameof(AgreementId))] public LiquidationAgreement Agreement { get; set; } = null!;
    [Column("item_id")] public Guid ItemId { get; set; }
    [ForeignKey(nameof(ItemId))] public LiquidationItem Item { get; set; } = null!;
    [Column("winning_bid_id")] public Guid WinningBidId { get; set; }
    [Column("name"), MaxLength(255)] public string Name { get; set; } = "";
    [Column("unit"), MaxLength(10)] public string Unit { get; set; } = "kg";
    [Column("quantity", TypeName = "numeric(18,3)")] public decimal Quantity { get; set; }
    [Column("unit_price", TypeName = "numeric(18,0)")] public decimal UnitPrice { get; set; }
    [Column("amount", TypeName = "numeric(18,0)")] public decimal Amount { get; set; }
}
