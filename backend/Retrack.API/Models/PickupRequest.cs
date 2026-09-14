using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class PickupRequest
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public Guid? DepotId { get; set; }
    public Guid? AssignedEmployeeId { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? TimeSlot { get; set; }
    public bool IsBroadcast { get; set; }
    public PickupRequestStatus Status { get; set; } = PickupRequestStatus.PENDING;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Seller Seller { get; set; } = null!;
    public Depot? Depot { get; set; }
    public DepotEmployee? AssignedEmployee { get; set; }
    public ICollection<PickupRequestItem> Items { get; set; } = new List<PickupRequestItem>();
    public ICollection<PickupRequestImage> Images { get; set; } = new List<PickupRequestImage>();
}
