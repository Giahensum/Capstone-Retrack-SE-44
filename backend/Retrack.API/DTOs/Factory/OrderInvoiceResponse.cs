namespace Retrack.API.DTOs.Factory;

public sealed record OrderInvoiceResponse
{
    public required string? InvoiceNumber { get; init; }
    public required string InvoiceFileUrl { get; init; }
    public required string? Status { get; init; }
}
