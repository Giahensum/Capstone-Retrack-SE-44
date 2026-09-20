namespace Retrack.API.Models;

public class BatchImage
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public InventoryBatch Batch { get; set; } = null!;
}


