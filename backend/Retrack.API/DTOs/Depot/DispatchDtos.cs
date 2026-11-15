namespace Retrack.API.DTOs.Depot;

public record DispatchAreaDto(Guid Id, string Name, double Latitude, double Longitude, double RadiusMeters, bool IsActive, Guid DepotId);
public record DispatchAreaInput(string Name, double Latitude, double Longitude, double RadiusMeters, bool IsActive);
public record DispatchShiftInput(Guid AreaId, Guid EmployeeId, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string Vehicle, decimal CapacityKg, int MaxPendingOrders, Guid? OperationId = null);
public record DispatchShiftDto(Guid Id, Guid AreaId, string AreaName, Guid EmployeeId, string EmployeeName,
    DateTime StartsAt, DateTime EndsAt, string Availability, string? Reason, string Vehicle, decimal CapacityKg, int MaxPendingOrders, int AssignedCount);
public record DispatchPickupDto(Guid Id, string SellerName, string SellerPhone, string Address, string? Description,
    decimal? Latitude, decimal? Longitude, DateTime? PreferredDatetime, string Status, Guid? EmployeeId,
    string? EmployeeName, string? QueueReason, bool HasCheckIn, DateTime CreatedAt);
public record DispatchHistoryDto(Guid Id, Guid? FromEmployeeId, string? FromEmployeeName, Guid? ToEmployeeId,
    string? ToEmployeeName, string Reason, DateTime CreatedAt);
public record DispatchAssignInput(Guid OperationId, Guid ShiftId, string Reason);
public record DispatchStopInput(string Reason);
public record DispatchSummaryDto(int AreaCount, int ActiveShiftCount, int WaitingCount);
public record DispatchEventCompatDto(Guid Id, Guid PickupId, Guid? FromEmployeeId, Guid? ToEmployeeId, Guid? ShiftId, string Reason, DateTime CreatedAt);
