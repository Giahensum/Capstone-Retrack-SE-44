using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class FactoryDemand
{
    public Guid Id { get; set; }
    public Guid FactoryId { get; set; }
    public MaterialType MaterialType { get; set; }
    public decimal QuantityKg { get; set; }
    public decimal? PricePerKg { get; set; }
    public decimal? MinPricePerKg { get; set; }
    public string? Note { get; set; }
    public DateTime? Deadline { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Factory Factory { get; set; } = null!;
}
