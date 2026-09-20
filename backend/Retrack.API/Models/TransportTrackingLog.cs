namespace Retrack.API.Models;

public class TransportTrackingLog
{
    public Guid Id { get; set; }
    public Guid TransportJobId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TransportJob TransportJob { get; set; } = null!;
}


