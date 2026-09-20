namespace Retrack.API.Models;

public class PickupRequestImage
{
    public Guid Id { get; set; }
    public Guid PickupRequestId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public PickupRequest PickupRequest { get; set; } = null!;
}


