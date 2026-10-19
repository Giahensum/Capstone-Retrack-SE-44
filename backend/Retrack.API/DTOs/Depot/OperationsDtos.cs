namespace Retrack.API.DTOs.Depot;
public record DepotNoticeDto(Guid Id, string Title, string? Message, bool IsRead, DateTime CreatedAt, Guid? BatchId, Guid? PickupId);
public record DepotDriverEventDto(Guid Id, string Action, string DriverName, string? Phone, string? Reason, string? ImageUrl, DateTime CreatedAt, string? FollowupStatus);
public record IncidentFollowupDto(Guid Id, string Status, string Note, Guid CreatedBy, DateTime CreatedAt);
public record CreateIncidentFollowupDto(Guid OperationId, string Status, string Note);
