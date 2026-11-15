namespace Retrack.API.Models;

public class PlatformFeeLog
{
    public Guid Id { get; set; }
    public Guid? PickupRequestId { get; set; }
    public Guid? BatchOrderId { get; set; }
    public Guid PayerId { get; set; }
    public decimal TransactionAmount { get; set; }
    public decimal FeePercentage { get; set; } = 5;
    public decimal FeeAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


