namespace Retrack.API.DTOs.Factory;

public sealed record OrderTransportResponse
{
    public required string Status { get; init; }
    public required Guid? DriverId { get; init; }
}
