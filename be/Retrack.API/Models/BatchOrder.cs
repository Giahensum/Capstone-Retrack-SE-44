using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class BatchOrder
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid FactoryId { get; set; }
    public decimal AgreedPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTime? ReceivedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SettledAt { get; set; }
    public decimal? FeeAmount { get; set; }
    public decimal? NetPayableAmount { get; set; }
    public string? PaymentReference { get; set; }
    public BatchStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public InventoryBatch Batch { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public TransportJob? BatchOrderTransport { get; set; }
    public WeightVerification? WeightVerification { get; set; }
    public WeightTicket? WeightTicket { get; set; }
    public Invoice? Invoice { get; set; }
}
