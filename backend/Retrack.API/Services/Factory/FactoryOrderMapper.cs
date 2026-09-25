using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Services.Factory;

public static class FactoryOrderMapper
{
    public static string Status(InventoryBatch batch) => batch.Status switch
    {
        "PENDING_APPROVAL" or "PENDING_FACTORY" => "ACCEPTED",
        "TRANSPORT_READY" or "ACCEPTED" => batch.TransportJob?.Status switch
        {
            "PICKED_UP" or "IN_TRANSIT" or "ON_THE_WAY" => "IN_TRANSIT",
            "DELIVERED" => "DELIVERED",
            _ => "ACCEPTED"
        },
        "READY_FOR_PICKUP" => batch.TransportJob?.Status switch
        {
            "PICKED_UP" or "IN_TRANSIT" or "ON_THE_WAY" or "IN_PROGRESS" => "IN_TRANSIT",
            "DELIVERED" => "DELIVERED",
            _ => "ACCEPTED"
        },
        "RECEIVED" => "RECEIVED",
        "WEIGHED" => "WEIGHED",
        "VERIFIED" => "VERIFIED",
        "PAID" or "COMPLETED" => "PAID",
        "REJECTED" or "CANCELLED" => "REJECTED",
        _ => batch.Status
    };

    public static OrderResponse Map(InventoryBatch batch)
    {
        var qc = batch.QualityCheck;
        var transport = batch.TransportJob;
        var status = Status(batch);
        var invoiceStatus = qc?.InvoiceStatus;
        return new OrderResponse
        {
            Id = batch.Id,
            BatchId = batch.Id,
            BatchCode = batch.Id.ToString("N")[..8].ToUpperInvariant(),
            MaterialType = batch.MaterialType,
            EstimatedWeightKg = batch.DeclaredWeightKg,
            ActualWeightKg = batch.ActualWeightKg,
            DepotId = batch.DepotId,
            DepotName = batch.Depot.Name,
            DepotAddress = batch.Depot.Address,
            DepotPhone = batch.Depot.Owner?.Phone,
            AgreedPrice = qc?.AgreedPricePerKg ?? 0,
            TotalAmount = qc?.GrossAmount,
            Status = status,
            ReceivedAt = batch.FactoryReceivedAt,
            DecidedAt = batch.FactoryDecidedAt,
            RejectionReason = batch.RejectionReason,
            SettledAt = batch.SettledAt,
            FeeAmount = qc?.PlatformFeeAmount,
            NetPayableAmount = qc?.NetAmount,
            PaymentReference = batch.PaymentReference,
            CreatedAt = batch.CreatedAt,
            Transport = transport == null ? null : new OrderTransportResponse
            {
                Status = transport.Status,
                DriverId = transport.DriverId
            },
            WeightVerification = qc == null ? null : new OrderWeightVerificationResponse
            {
                DepotWeightKg = batch.DeclaredWeightKg,
                FactoryWeightKg = qc.ActualWeightKg,
                DifferencePercentage = qc.DifferencePercentage ?? 0,
                IsVerified = qc.IsAccepted,
                PurityPercent = qc.PurityPercent,
                MoisturePercent = qc.MoisturePercent,
                ContaminationPercent = qc.ContaminationPercent,
                Grade = qc.Grade == "PENDING" ? null : qc.Grade,
                QualityNote = qc.QualityNote,
                Note = qc.QualityNote
            },
            WeightTicket = qc == null ? null : new OrderWeightTicketResponse
            {
                TicketNumber = qc.TicketNumber,
                GrossWeightKg = qc.GrossWeightKg,
                TareWeightKg = qc.TareWeightKg,
                NetWeightKg = qc.ActualWeightKg,
                TicketImageUrl = qc.TicketImageUrl
            },
            Invoice = qc?.InvoiceFileUrl == null ? null : new OrderInvoiceResponse
            {
                InvoiceNumber = qc.InvoiceNumber,
                InvoiceFileUrl = qc.InvoiceFileUrl,
                Status = invoiceStatus
            }
        };
    }
}
