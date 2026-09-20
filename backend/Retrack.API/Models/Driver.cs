namespace Retrack.API.Models;

public class Driver
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DepotId { get; set; }
    public string? LicenseNumber { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleType { get; set; }
    public decimal? MaxCapacityKg { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Depot Depot { get; set; } = null!;
}


