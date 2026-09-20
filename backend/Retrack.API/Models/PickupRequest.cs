using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("pickup_requests")]
    public class PickupRequest
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("seller_id")]
        public Guid SellerId { get; set; }

        [Column("target_depot_id")]
        public Guid? TargetDepotId { get; set; }

        [Column("accepted_collector_id")]
        public Guid? AcceptedCollectorId { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("request_image_url")]
        public string? RequestImageUrl { get; set; }

        [Required]
        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [Column("latitude")]
        public decimal? Latitude { get; set; }

        [Column("longitude")]
        public decimal? Longitude { get; set; }

        [Column("preferred_datetime")]
        public DateTime? PreferredDatetime { get; set; }

        [Column("checkin_image_url")]
        public string? CheckinImageUrl { get; set; }

        // Financial
        [Column("gross_amount")]
        public decimal GrossAmount { get; set; } = 0;

        [Column("platform_fee_percentage")]
        public decimal PlatformFeePercentage { get; set; } = 0;

        [Column("platform_fee_amount")]
        public decimal PlatformFeeAmount { get; set; } = 0;

        [Column("net_amount")]
        public decimal NetAmount { get; set; } = 0;

        [Column("payment_proof_url")]
        public string? PaymentProofUrl { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "PENDING";
        // PENDING, SCHEDULED, WEIGHED, SELLER_CONFIRMED, AWAITING_PAYMENT, PAYMENT_SENT, DONE

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("SellerId")]
        public User Seller { get; set; } = null!;

        [ForeignKey("TargetDepotId")]
        public Depot? TargetDepot { get; set; }

        [ForeignKey("AcceptedCollectorId")]
        public User? AcceptedCollector { get; set; }

        public ICollection<PickupRequestItem> Items { get; set; } = new List<PickupRequestItem>();
        public ICollection<SellerDepotReview> Reviews { get; set; } = new List<SellerDepotReview>();
    }
}

