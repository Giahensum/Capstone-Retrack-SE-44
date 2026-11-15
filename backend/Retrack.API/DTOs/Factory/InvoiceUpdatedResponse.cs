namespace Retrack.API.DTOs.Factory;

public sealed record InvoiceUpdatedResponse
{
    public required Guid Id { get; init; }
    public required string? InvoiceNumber { get; init; }
    public required string? Status { get; init; }
}
