using Retrack.API.DTOs.Employee;

namespace Retrack.API.DTOs.Driver;

public sealed record DriverActionRequest(Guid OperationId, string? Reason);
public sealed record DeliveryEventDto(string Action, string? Reason, string? ImageUrl,
    double? DistanceMeters, double? AccuracyMeters, DateTime CreatedAt);
public sealed record DriverDeliveryDto(DriverJobDto Job, CollectionPolicyDto Policy, IReadOnlyList<DeliveryEventDto> Events);
