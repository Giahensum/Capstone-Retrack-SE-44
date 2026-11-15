using System.ComponentModel.DataAnnotations.Schema;
namespace Retrack.API.Models;
[Table("depot_incident_followups")]
public sealed class DepotIncidentFollowup
{
    [Column("id")] public Guid Id { get; set; }
    [Column("event_id")] public Guid EventId { get; set; }
    public DriverDeliveryEvent Event { get; set; } = null!;
    [Column("status")] public string Status { get; set; } = "NOTED";
    [Column("note")] public string Note { get; set; } = "";
    [Column("created_by")] public Guid CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
