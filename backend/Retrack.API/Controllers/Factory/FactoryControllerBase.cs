using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[ApiController]
public abstract class FactoryControllerBase(AppDbContext db) : ControllerBase
{
    protected AppDbContext Db { get; } = db;

    protected Guid CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id) ? id : throw new UnauthorizedAccessException("Token không có user id hợp lệ.");
        }
    }

    protected async Task<Retrack.API.Models.Factory> CurrentFactory(CancellationToken ct = default)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.OwnerId == CurrentUserId, ct);
        if (factory is not null) return factory;

        var owner = await Db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == CurrentUserId, ct);
        if (owner is null || !string.Equals(owner.Role, "FACTORY", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Tài khoản chưa có hồ sơ nhà máy.");

        factory = new Retrack.API.Models.Factory { OwnerId = owner.Id, Name = owner.FullName, Address = "Chưa cập nhật" };
        Db.Factories.Add(factory);
        await Db.SaveChangesAsync(ct);
        return factory;
    }

    protected static object DemandView(FactoryDemand x) => new
    {
        x.Id, materialType = x.MaterialType, quantityKg = x.RequiredWeightKg,
        x.MinPricePerKg, maxPricePerKg = x.MaxPricePerKg, x.Deadline, x.IsActive, x.Note, x.CreatedAt
    };

    protected static string OrderStatus(InventoryBatch batch) => batch.Status switch
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

    protected static object OrderView(InventoryBatch batch)
    {
        var qc = batch.QualityCheck;
        var transport = batch.TransportJob;
        var status = OrderStatus(batch);
        var invoiceStatus = qc?.InvoiceStatus;
        return new
        {
            id = batch.Id,
            batchId = batch.Id,
            batchCode = batch.Id.ToString("N")[..8].ToUpperInvariant(),
            materialType = batch.MaterialType,
            estimatedWeightKg = batch.DeclaredWeightKg,
            actualWeightKg = batch.ActualWeightKg,
            depotId = batch.DepotId,
            depotName = batch.Depot.Name,
            depotAddress = batch.Depot.Address,
            depotPhone = batch.Depot.Owner?.Phone,
            agreedPrice = qc?.AgreedPricePerKg ?? 0,
            totalAmount = qc?.GrossAmount,
            status,
            receivedAt = batch.FactoryReceivedAt,
            decidedAt = batch.FactoryDecidedAt,
            rejectionReason = batch.RejectionReason,
            settledAt = batch.SettledAt,
            feeAmount = qc?.PlatformFeeAmount,
            netPayableAmount = qc?.NetAmount,
            paymentReference = batch.PaymentReference,
            createdAt = batch.CreatedAt,
            transport = transport == null ? null : new { status = transport.Status, driverId = transport.DriverId },
            weightVerification = qc == null ? null : new
            {
                depotWeightKg = batch.DeclaredWeightKg,
                factoryWeightKg = qc.ActualWeightKg,
                differencePercentage = qc.DifferencePercentage ?? 0,
                isVerified = qc.IsAccepted,
                qc.PurityPercent, qc.MoisturePercent, qc.ContaminationPercent,
                grade = qc.Grade == "PENDING" ? null : qc.Grade,
                qualityNote = qc.QualityNote,
                note = qc.QualityNote
            },
            weightTicket = qc == null ? null : new
            {
                qc.TicketNumber, qc.GrossWeightKg, qc.TareWeightKg,
                netWeightKg = qc.ActualWeightKg, ticketImageUrl = qc.TicketImageUrl
            },
            invoice = qc?.InvoiceFileUrl == null ? null : new
            {
                qc.InvoiceNumber, invoiceFileUrl = qc.InvoiceFileUrl, status = invoiceStatus
            }
        };
    }

    protected IQueryable<InventoryBatch> OrderQuery(Guid factoryId) => Db.InventoryBatches
        .AsNoTracking()
        .Where(x => x.TargetFactoryId == factoryId && x.Status != "MARKETPLACE" && x.Status != "DRAFT")
        .Include(x => x.Depot).ThenInclude(x => x.Owner)
        .Include(x => x.TransportJob)
        .Include(x => x.QualityCheck);
}
