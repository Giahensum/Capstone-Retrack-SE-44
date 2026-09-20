using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class InventoryBatch
{
    public Guid Id { get; set; }
    public Guid DepotId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public decimal EstimatedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.DRAFT;
    public TransportType? TransportType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Depot Depot { get; set; } = null!;
    public ICollection<BatchImage> Images { get; set; } = new List<BatchImage>();
}
