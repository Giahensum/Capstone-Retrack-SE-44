using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class MarketPrice
{
    public Guid Id { get; set; }
    public MaterialType MaterialType { get; set; }
    public decimal PricePerKg { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
