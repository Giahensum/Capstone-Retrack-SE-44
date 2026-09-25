namespace Retrack.API.DTOs.Factory;

public sealed record OrderSettlementResponse
{
    public required Guid Id { get; init; }
    public required string Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal FeeAmount { get; init; }
    public required decimal NetPayableAmount { get; init; }
    public required string? PaymentReference { get; init; }
    public required DateTime? SettledAt { get; init; }
    public required decimal FeePercentage { get; init; }
}
