using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("pickup_request_items")]
    public class PickupRequestItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("pickup_request_id")]
        public Guid PickupRequestId { get; set; }

        [Required]
        [Column("material_type")]
        [MaxLength(100)]
        public string MaterialType { get; set; } = string.Empty;

        [Required]
        [Column("weight_kg")]
        public decimal WeightKg { get; set; }

        [Required]
        [Column("price_per_kg")]
        public decimal PricePerKg { get; set; }

        [Required]
        [Column("sub_total")]
        public decimal SubTotal { get; set; }

        // Navigation
        [ForeignKey("PickupRequestId")]
        public PickupRequest PickupRequest { get; set; } = null!;
    }
}

