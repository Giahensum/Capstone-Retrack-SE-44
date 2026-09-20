using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class BatchOrder
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid FactoryId { get; set; }
    public decimal AgreedPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public BatchStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public InventoryBatch Batch { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
}
