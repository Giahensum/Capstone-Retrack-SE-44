using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class Partnership
{
    public Guid Id { get; set; }
    public Guid DepotId { get; set; }
    public Guid FactoryId { get; set; }
    public PartnershipStatus Status { get; set; } = PartnershipStatus.PENDING;
    public decimal? Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Depot Depot { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
}
