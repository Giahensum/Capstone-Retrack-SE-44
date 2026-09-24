using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("market_prices")]
    public class MarketPrice
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("material_type")]
        [MaxLength(100)]
        public string MaterialType { get; set; } = string.Empty;

        [Required]
        [Column("price_per_kg")]
        public decimal PricePerKg { get; set; }

        [Required]
        [Column("effective_date")]
        public DateTime EffectiveDate { get; set; }

        [Column("source")]
        [MaxLength(255)]
        public string? Source { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
