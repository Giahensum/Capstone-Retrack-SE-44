namespace Retrack.API.DTOs.Factory;

public sealed record FactoryNotificationResponse(Guid Id, string Title, string Message, string Category,
    Guid? BatchId, bool IsRead, DateTime CreatedAt);
