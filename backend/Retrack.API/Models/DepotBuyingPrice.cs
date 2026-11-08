using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("depot_buying_prices")]
public class DepotBuyingPrice
{
    [Column("depot_id")] public Guid DepotId { get; set; }
    public Depot Depot { get; set; } = null!;
    [Column("material_type"), MaxLength(100)] public string MaterialType { get; set; } = "";
    [Column("price_per_kg", TypeName = "numeric(18,0)")] public decimal PricePerKg { get; set; }
    [Column("condition_note"), MaxLength(500)] public string ConditionNote { get; set; } = "";
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
