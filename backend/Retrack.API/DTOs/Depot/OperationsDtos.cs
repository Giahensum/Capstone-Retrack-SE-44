namespace Retrack.API.DTOs.Depot;
public class DepotNoticeQuery : DepotQuery { public string? Category { get; set; } }
public record DepotNoticeDto(Guid Id, string Title, string? Message, bool IsRead, DateTime CreatedAt, Guid? BatchId, Guid? PickupId,
    string Category = "SYSTEM", string? TargetType = null, Guid? TargetId = null);
public record DepotDriverEventDto(Guid Id, string Action, string DriverName, string? Phone, string? Reason, string? ImageUrl, DateTime CreatedAt, string? FollowupStatus);
public record IncidentFollowupDto(Guid Id, string Status, string Note, Guid CreatedBy, DateTime CreatedAt);
public record CreateIncidentFollowupDto(Guid OperationId, string Status, string Note);
