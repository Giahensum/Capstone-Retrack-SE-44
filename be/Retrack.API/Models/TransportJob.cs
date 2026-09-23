using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class TransportJob
{
    public Guid Id { get; set; }
    public Guid BatchOrderId { get; set; }
    public Guid? DriverId { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal? TransportFee { get; set; }
    public TransportStatus Status { get; set; } = TransportStatus.PENDING;
    public DateTime? PickupTime { get; set; }
    public DateTime? DeliveredTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BatchOrder BatchOrder { get; set; } = null!;
    public Driver? Driver { get; set; }
}
