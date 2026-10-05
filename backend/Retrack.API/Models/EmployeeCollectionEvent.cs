using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Retrack.API.Models;

// Nhật ký bất biến do nghiệp vụ Employee ghi; các role khác có thể đọc để đối soát.
[Table("employee_collection_events")]
[Index(nameof(PickupRequestId), nameof(Kind), nameof(Revision), IsUnique = true)]
public sealed class EmployeeCollectionEvent
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("pickup_request_id")] public Guid PickupRequestId { get; set; }
    [Column("employee_id")] public Guid EmployeeId { get; set; }
    [Column("kind"), MaxLength(30)] public string Kind { get; set; } = "";
    [Column("revision")] public int Revision { get; set; }
    [Column("from_status"), MaxLength(30)] public string FromStatus { get; set; } = "";
    [Column("to_status"), MaxLength(30)] public string ToStatus { get; set; } = "";
    [Column("snapshot_json")] public string SnapshotJson { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(PickupRequestId))] public PickupRequest PickupRequest { get; set; } = null!;
    [ForeignKey(nameof(EmployeeId))] public User Employee { get; set; } = null!;
}
