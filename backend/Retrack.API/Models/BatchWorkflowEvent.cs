using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("batch_workflow_events")]
public sealed class BatchWorkflowEvent
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("batch_id")] public Guid BatchId { get; set; }
    [Column("actor_id")] public Guid ActorId { get; set; }
    [Column("action"), MaxLength(50)] public string Action { get; set; } = "";
    [Column("from_status"), MaxLength(50)] public string FromStatus { get; set; } = "";
    [Column("to_status"), MaxLength(50)] public string ToStatus { get; set; } = "";
    [Column("revision")] public int Revision { get; set; }
    [Column("price_per_kg")] public decimal? PricePerKg { get; set; }
    [Column("net_amount")] public decimal? NetAmount { get; set; }
    [Column("note"), MaxLength(2000)] public string? Note { get; set; }
    [Column("snapshot_json")] public string SnapshotJson { get; set; } = "{}";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public InventoryBatch Batch { get; set; } = null!;
}
