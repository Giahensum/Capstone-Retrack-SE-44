namespace Retrack.API.DTOs.Factory;

public sealed record OrderReceivedResponse
{
    public required Guid Id { get; init; }
    public required string Status { get; init; }
    public required DateTime? ReceivedAt { get; init; }
}
