using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class PickupRequestItem
{
    public Guid Id { get; set; }
    public Guid PickupRequestId { get; set; }
    public MaterialType MaterialType { get; set; }
    public string? MaterialLabel { get; set; }
    public decimal WeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public PickupRequest PickupRequest { get; set; } = null!;
}
