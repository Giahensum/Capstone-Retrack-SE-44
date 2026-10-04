using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("pickup_checkins")]
public sealed class PickupCheckIn
{
    [Key, Column("pickup_request_id")] public Guid PickupRequestId { get; set; }
    [Column("employee_id")] public Guid EmployeeId { get; set; }
    [Column("latitude")] public double Latitude { get; set; }
    [Column("longitude")] public double Longitude { get; set; }
    [Column("accuracy_meters")] public double AccuracyMeters { get; set; }
    [Column("distance_meters")] public double DistanceMeters { get; set; }
    [Column("location_recorded_at")] public DateTime LocationRecordedAt { get; set; }
    [Column("photo_taken_at")] public DateTime PhotoTakenAt { get; set; }
    [Column("checked_in_at")] public DateTime CheckedInAt { get; set; }
    [Column("image_url")] public string ImageUrl { get; set; } = "";
    [Column("revision")] public int Revision { get; set; }
    [ForeignKey(nameof(PickupRequestId))] public PickupRequest PickupRequest { get; set; } = null!;
    [ForeignKey(nameof(EmployeeId))] public User Employee { get; set; } = null!;
}
