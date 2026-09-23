namespace Retrack.API.Models;

public class DepotEmployee
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DepotId { get; set; }
    public string? EmployeeCode { get; set; }
    public int TotalPickupsCompleted { get; set; }
    public decimal TotalKgCollected { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Depot Depot { get; set; } = null!;
}
