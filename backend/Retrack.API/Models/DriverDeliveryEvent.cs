using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

// Nhật ký riêng của vận chuyển; giữ bằng chứng kể cả sau khi tài xế trả chuyến.
[Table("driver_delivery_events")]
public sealed class DriverDeliveryEvent
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("job_id")] public Guid JobId { get; set; }
    [Column("driver_id")] public Guid DriverId { get; set; }
    [Column("operation_id")] public Guid OperationId { get; set; }
    [Column("action"), MaxLength(30)] public string Action { get; set; } = "";
    [Column("reason"), MaxLength(1000)] public string? Reason { get; set; }
    [Column("image_url")] public string? ImageUrl { get; set; }
    [Column("latitude")] public double? Latitude { get; set; }
    [Column("longitude")] public double? Longitude { get; set; }
    [Column("accuracy_meters")] public double? AccuracyMeters { get; set; }
    [Column("distance_meters")] public double? DistanceMeters { get; set; }
    [Column("location_recorded_at")] public DateTime? LocationRecordedAt { get; set; }
    [Column("photo_taken_at")] public DateTime? PhotoTakenAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TransportJob Job { get; set; } = null!;
    public User Driver { get; set; } = null!;
}
