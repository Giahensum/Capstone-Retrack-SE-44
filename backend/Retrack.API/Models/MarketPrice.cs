using Retrack.API.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace Retrack.API.Models;

[Table("market_prices")]
public class MarketPrice
{
    [Key, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Column("material_type")]
    public MaterialType MaterialType { get; set; }
    [Column("price_per_kg")]
    public decimal PricePerKg { get; set; }
    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }
    [Column("source")]
    public string? Source { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


