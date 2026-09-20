namespace Retrack.API.Models;

public class Seller
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}


