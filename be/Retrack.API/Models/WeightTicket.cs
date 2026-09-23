namespace Retrack.API.Models;

public class WeightTicket
{
    public Guid Id { get; set; }
    public Guid BatchOrderId { get; set; }
    public string? TicketNumber { get; set; }
    public decimal? GrossWeightKg { get; set; }
    public decimal? TareWeightKg { get; set; }
    public decimal? NetWeightKg { get; set; }
    public string? TicketImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BatchOrder BatchOrder { get; set; } = null!;
}
