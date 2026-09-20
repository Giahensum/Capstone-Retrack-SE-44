namespace Retrack.API.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


