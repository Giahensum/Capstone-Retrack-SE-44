namespace Retrack.API.DTOs.Factory;

public sealed record BatchAcceptedResponse
{
    public required Guid Id { get; init; }
    public required Guid BatchId { get; init; }
    public required string Status { get; init; }
    public required decimal AgreedPrice { get; init; }
}
