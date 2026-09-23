using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using FactoryEntity = Retrack.API.Models.Factory;

namespace Retrack.API.Controllers.Factory;

[ApiController]
public abstract class FactoryControllerBase : ControllerBase
{
    protected readonly AppDbContext Db;
    protected FactoryControllerBase(AppDbContext db) => Db = db;

    protected Guid CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Token không có user id hợp lệ.");
        }
    }

    protected async Task<FactoryEntity> CurrentFactory(CancellationToken ct = default)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.UserId == CurrentUserId, ct);
        return factory ?? throw new KeyNotFoundException("Tài khoản chưa có hồ sơ nhà máy.");
    }

    protected static object DemandView(FactoryDemand x) => new
    {
        x.Id, materialType = x.MaterialType.ToString(), quantityKg = x.QuantityKg,
        minPricePerKg = x.MinPricePerKg, maxPricePerKg = x.PricePerKg,
        x.Deadline, x.IsActive, x.Note, x.CreatedAt
    };

    protected static object OrderView(BatchOrder x) => new
    {
        x.Id, x.BatchId, batchCode = x.Batch.BatchCode,
        materialType = x.Batch.MaterialType.ToString(), estimatedWeightKg = x.Batch.EstimatedWeightKg,
        actualWeightKg = x.Batch.ActualWeightKg, depotId = x.Batch.DepotId,
        depotName = x.Batch.Depot.CompanyName, depotAddress = x.Batch.Depot.Address,
        depotPhone = x.Batch.Depot.ContactPhone, x.AgreedPrice, x.TotalAmount,
        status = x.Status.ToString(), x.ReceivedAt, x.DecidedAt, x.RejectionReason,
        x.SettledAt, x.FeeAmount, x.NetPayableAmount, x.PaymentReference, x.CreatedAt,
        transport = x.BatchOrderTransport == null ? null : new
        {
            status = x.BatchOrderTransport.Status.ToString(), x.BatchOrderTransport.PickupTime,
            x.BatchOrderTransport.DeliveredTime, x.BatchOrderTransport.DriverId
        },
        weightVerification = x.WeightVerification == null ? null : new
        {
            x.WeightVerification.DepotWeightKg, x.WeightVerification.FactoryWeightKg,
            x.WeightVerification.DifferencePercentage, x.WeightVerification.IsVerified,
            x.WeightVerification.PurityPercent, x.WeightVerification.MoisturePercent,
            x.WeightVerification.ContaminationPercent, x.WeightVerification.Grade,
            x.WeightVerification.QualityNote, x.WeightVerification.Note
        },
        weightTicket = x.WeightTicket == null ? null : new
        {
            x.WeightTicket.TicketNumber, x.WeightTicket.GrossWeightKg, x.WeightTicket.TareWeightKg,
            x.WeightTicket.NetWeightKg, x.WeightTicket.TicketImageUrl
        },
        invoice = x.Invoice == null ? null : new
        {
            x.Invoice.InvoiceNumber, x.Invoice.InvoiceFileUrl, x.Invoice.Subtotal,
            x.Invoice.VatAmount, x.Invoice.TotalAmount, status = x.Invoice.Status.ToString()
        }
    };

    protected IQueryable<BatchOrder> OrderQuery(Guid factoryId) => Db.BatchOrders
        .AsNoTracking()
        .Where(x => x.FactoryId == factoryId)
        .Include(x => x.Batch).ThenInclude(x => x.Depot)
        .Include(x => x.BatchOrderTransport)
        .Include(x => x.WeightVerification)
        .Include(x => x.WeightTicket)
        .Include(x => x.Invoice);
}
