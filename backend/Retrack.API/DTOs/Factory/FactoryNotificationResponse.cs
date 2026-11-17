namespace Retrack.API.DTOs.Factory;

public sealed record FactoryNotificationResponse(Guid Id, string Title, string Message, string Category,
    Guid? BatchId, Guid? DepotId, string? TargetType, bool IsRead, DateTime CreatedAt);
