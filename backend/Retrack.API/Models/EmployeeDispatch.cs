using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("employee_collection_areas")]
public sealed class EmployeeCollectionArea
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("name"), MaxLength(120)] public string Name { get; set; } = "";
    [Column("latitude")] public double Latitude { get; set; }
    [Column("longitude")] public double Longitude { get; set; }
    [Column("radius_meters")] public double RadiusMeters { get; set; } = 3000;
    [Column("is_active")] public bool IsActive { get; set; } = true;
}

[Table("employee_dispatch_shifts")]
public sealed class EmployeeDispatchShift
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("area_id")] public Guid AreaId { get; set; }
    [Column("employee_id")] public Guid EmployeeId { get; set; }
    [Column("starts_at")] public DateTime StartsAt { get; set; }
    [Column("ends_at")] public DateTime EndsAt { get; set; }
    [Column("availability"), MaxLength(20)] public string Availability { get; set; } = "AVAILABLE";
    [Column("reason"), MaxLength(1000)] public string? Reason { get; set; }
    [Column("vehicle"), MaxLength(20)] public string Vehicle { get; set; } = "motorcycle";
    [Column("capacity_kg")] public decimal CapacityKg { get; set; } = 100;
    [Column("max_pending_orders")] public int MaxPendingOrders { get; set; } = 10;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Một bản ghi/đơn giữ lý do đang chờ; lịch sử phân công nằm trong bảng sự kiện riêng.
[Table("employee_dispatch_queue")]
public sealed class EmployeeDispatchQueue
{
    [Key, Column("pickup_id")] public Guid PickupId { get; set; }
    [Column("reason")] public string Reason { get; set; } = "";
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("employee_dispatch_events")]
public sealed class EmployeeDispatchEvent
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("pickup_id")] public Guid PickupId { get; set; }
    [Column("from_employee_id")] public Guid? FromEmployeeId { get; set; }
    [Column("to_employee_id")] public Guid? ToEmployeeId { get; set; }
    [Column("shift_id")] public Guid? ShiftId { get; set; }
    [Column("reason")] public string Reason { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
