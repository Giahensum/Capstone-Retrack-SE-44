using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("seller_depot_reviews")]
    public class SellerDepotReview
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("pickup_request_id")]
        public Guid PickupRequestId { get; set; }

        [Required]
        [Column("depot_id")]
        public Guid DepotId { get; set; }

        [Column("rating")]
        [Range(1, 5)]
        public int? Rating { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("PickupRequestId")]
        public PickupRequest PickupRequest { get; set; } = null!;

        [ForeignKey("DepotId")]
        public Depot Depot { get; set; } = null!;
    }

    // -------------------------------------------------------
    [Table("factory_demands")]
    public class FactoryDemand
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("factory_id")]
        public Guid FactoryId { get; set; }

        [Required]
        [Column("material_type")]
        [MaxLength(100)]
        public string MaterialType { get; set; } = string.Empty;

        [Required]
        [Column("required_weight_kg")]
        public decimal RequiredWeightKg { get; set; }

        [Column("min_price_per_kg")]
        public decimal? MinPricePerKg { get; set; }

        [Column("max_price_per_kg")]
        public decimal? MaxPricePerKg { get; set; }

        [Required]
        [Column("deadline")]
        public DateTime Deadline { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("note")] public string? Note { get; set; }
        [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("FactoryId")]
        public Factory Factory { get; set; } = null!;
    }

    // -------------------------------------------------------
    [Table("factory_depot_partnerships")]
    public class FactoryDepotPartnership
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("depot_id")]
        public Guid DepotId { get; set; }

        [Required]
        [Column("factory_id")]
        public Guid FactoryId { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, BLOCKED

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("DepotId")]
        public Depot Depot { get; set; } = null!;

        [ForeignKey("FactoryId")]
        public Factory Factory { get; set; } = null!;
    }

    // -------------------------------------------------------
    [Table("inventory_batches")]
    public class InventoryBatch
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("depot_id")]
        public Guid DepotId { get; set; }

        [Column("target_factory_id")]
        public Guid? TargetFactoryId { get; set; }

        [Column("direct_offer_factory_id")]
        public Guid? DirectOfferFactoryId { get; set; }

        [Required]
        [Column("material_type")]
        [MaxLength(100)]
        public string MaterialType { get; set; } = string.Empty;

        [Required]
        [Column("declared_weight_kg")]
        public decimal DeclaredWeightKg { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;
        // MARKETPLACE, PENDING_APPROVAL, TRANSPORT_READY, COMPLETED

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("actual_weight_kg")] public decimal? ActualWeightKg { get; set; }
        [Column("factory_received_at")] public DateTime? FactoryReceivedAt { get; set; }
        [Column("factory_decided_at")] public DateTime? FactoryDecidedAt { get; set; }
        [Column("rejection_reason")] public string? RejectionReason { get; set; }
        [Column("agreed_price_per_kg")] public decimal? AgreedPricePerKg { get; set; }
        [Column("gross_amount")] public decimal? GrossAmount { get; set; }
        [Column("platform_fee_amount")] public decimal? PlatformFeeAmount { get; set; }
        [Column("net_amount")] public decimal? NetAmount { get; set; }
        [Column("payment_reference")] public string? PaymentReference { get; set; }
        [Column("settled_at")] public DateTime? SettledAt { get; set; }

        // Navigation
        [ForeignKey("DepotId")]
        public Depot Depot { get; set; } = null!;

        [ForeignKey("TargetFactoryId")]
        public Factory? TargetFactory { get; set; }

        [ForeignKey("DirectOfferFactoryId")]
        public Factory? DirectOfferFactory { get; set; }

        public TransportJob? TransportJob { get; set; }
        public BatchQualityCheck? QualityCheck { get; set; }
    }

    // -------------------------------------------------------
    [Table("transport_jobs")]
    public class TransportJob
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("batch_id")]
        public Guid BatchId { get; set; }

        [Column("driver_id")]
        public Guid? DriverId { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "PENDING"; // PENDING, IN_TRANSIT, DELIVERED

        [Column("checkin_depot_image_url")]
        public string? CheckinDepotImageUrl { get; set; }

        [Column("checkout_factory_image_url")]
        public string? CheckoutFactoryImageUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BatchId")]
        public InventoryBatch Batch { get; set; } = null!;

        [ForeignKey("DriverId")]
        public User? Driver { get; set; }
    }

    // -------------------------------------------------------
    [Table("batch_quality_checks")]
    public class BatchQualityCheck
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("batch_id")]
        public Guid BatchId { get; set; }

        [Required]
        [Column("factory_id")]
        public Guid FactoryId { get; set; }

        [Required]
        [Column("actual_weight_kg")]
        public decimal ActualWeightKg { get; set; }

        [Required]
        [Column("grade")]
        [MaxLength(10)]
        public string Grade { get; set; } = string.Empty;

        [Required]
        [Column("agreed_price_per_kg")]
        public decimal AgreedPricePerKg { get; set; }

        [Required]
        [Column("gross_amount")]
        public decimal GrossAmount { get; set; }

        [Column("platform_fee_percentage")]
        public decimal PlatformFeePercentage { get; set; } = 0;

        [Column("platform_fee_amount")]
        public decimal PlatformFeeAmount { get; set; } = 0;

        [Required]
        [Column("net_amount")]
        public decimal NetAmount { get; set; }

        [Column("payment_proof_url")]
        public string? PaymentProofUrl { get; set; }

        [Required]
        [Column("is_accepted")]
        public bool IsAccepted { get; set; }

        [Column("gross_weight_kg")] public decimal? GrossWeightKg { get; set; }
        [Column("tare_weight_kg")] public decimal? TareWeightKg { get; set; }
        [Column("difference_percentage")] public decimal? DifferencePercentage { get; set; }
        [Column("ticket_number")] public string? TicketNumber { get; set; }
        [Column("ticket_image_url")] public string? TicketImageUrl { get; set; }
        [Column("purity_percent")] public decimal? PurityPercent { get; set; }
        [Column("moisture_percent")] public decimal? MoisturePercent { get; set; }
        [Column("contamination_percent")] public decimal? ContaminationPercent { get; set; }
        [Column("quality_note")] public string? QualityNote { get; set; }
        [Column("resolution")] public string? Resolution { get; set; }
        [Column("invoice_number")] public string? InvoiceNumber { get; set; }
        [Column("invoice_file_url")] public string? InvoiceFileUrl { get; set; }
        [Column("invoice_status")] public string? InvoiceStatus { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BatchId")]
        public InventoryBatch Batch { get; set; } = null!;

        [ForeignKey("FactoryId")]
        public Factory Factory { get; set; } = null!;
    }

    // -------------------------------------------------------
    [Table("factory_depot_reviews")]
    public class FactoryDepotReview
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("batch_id")]
        public Guid BatchId { get; set; }

        [Required]
        [Column("factory_id")]
        public Guid FactoryId { get; set; }

        [Required]
        [Column("depot_id")]
        public Guid DepotId { get; set; }

        [Column("rating")]
        [Range(1, 5)]
        public int? Rating { get; set; }

        [Column("comment")]
        public string? Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BatchId")]
        public InventoryBatch Batch { get; set; } = null!;

        [ForeignKey("FactoryId")]
        public Factory Factory { get; set; } = null!;

        [ForeignKey("DepotId")]
        public Depot Depot { get; set; } = null!;
    }

    // -------------------------------------------------------
    [Table("platform_transactions")]
    public class PlatformTransaction
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("source_type")]
        [MaxLength(50)]
        public string SourceType { get; set; } = string.Empty; // PICKUP_REQUEST, BATCH_ORDER

        [Required]
        [Column("source_id")]
        public Guid SourceId { get; set; }

        [Required]
        [Column("fee_amount")]
        public decimal FeeAmount { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

