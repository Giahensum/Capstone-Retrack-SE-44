namespace Retrack.API.DTOs.Depot;

public record DepotBatchDetailDto(DepotBatchDto Batch, string? TransportStatus, string? DriverName,
    DateTime? ReceivedAt, DateTime? DecidedAt, string? RejectionReason,
    DepotBatchQualityDto? Quality, DepotBatchSettlementDto? Settlement);
public record DepotBatchQualityDto(decimal ActualWeightKg, decimal? GrossWeightKg, decimal? TareWeightKg,
    decimal? DifferencePercentage, string Grade, bool? IsAccepted, decimal? PurityPercent,
    decimal? MoisturePercent, decimal? ContaminationPercent, string? Note, string? Resolution,
    string? TicketNumber, string? TicketImageUrl, string? InvoiceNumber, string? InvoiceFileUrl);
public record DepotBatchSettlementDto(decimal? PricePerKg, decimal? GrossAmount, decimal? FeeAmount,
    decimal? NetAmount, string? PaymentReference, DateTime SettledAt, string? PaymentProofUrl);
