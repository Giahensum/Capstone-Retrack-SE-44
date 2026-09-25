namespace Retrack.API.DTOs.Factory;

public sealed record OrderResponse
{
    public required Guid Id { get; init; }
    public required Guid BatchId { get; init; }
    public required string BatchCode { get; init; }
    public required string MaterialType { get; init; }
    public required decimal EstimatedWeightKg { get; init; }
    public required decimal? ActualWeightKg { get; init; }
    public required Guid DepotId { get; init; }
    public required string DepotName { get; init; }
    public required string DepotAddress { get; init; }
    public required string? DepotPhone { get; init; }
    public required decimal AgreedPrice { get; init; }
    public required decimal? TotalAmount { get; init; }
    public required string Status { get; init; }
    public required DateTime? ReceivedAt { get; init; }
    public required DateTime? DecidedAt { get; init; }
    public required string? RejectionReason { get; init; }
    public required DateTime? SettledAt { get; init; }
    public required decimal? FeeAmount { get; init; }
    public required decimal? NetPayableAmount { get; init; }
    public required string? PaymentReference { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required OrderTransportResponse? Transport { get; init; }
    public required OrderWeightVerificationResponse? WeightVerification { get; init; }
    public required OrderWeightTicketResponse? WeightTicket { get; init; }
    public required OrderInvoiceResponse? Invoice { get; init; }
}
