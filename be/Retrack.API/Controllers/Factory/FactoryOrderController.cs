using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/orders")]
[Authorize(Roles = "FACTORY")]
public class FactoryOrderController(AppDbContext db, IConfiguration configuration) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var source = OrderQuery(factory.Id);
        if (query.Status.HasValue) source = source.Where(x => x.Status == query.Status);
        var count = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return Ok(new { success = true, data = new { items = items.Select(OrderView), totalCount = count, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)count / query.PageSize) } });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var order = await OrderQuery(factory.Id).SingleOrDefaultAsync(x => x.Id == id, ct);
        return order is null ? NotFound(new { success = false, message = "Không tìm thấy đơn hàng." }) : Ok(new { success = true, data = OrderView(order) });
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var order = await Db.BatchOrders.Include(x => x.Batch).ThenInclude(x => x.Depot)
            .Include(x => x.BatchOrderTransport)
            .SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (order.Status != BatchStatus.DELIVERED && order.BatchOrderTransport?.Status != TransportStatus.DELIVERED)
            return Conflict(new { success = false, message = "Chỉ xác nhận nhận hàng sau khi vận chuyển báo đã giao." });
        order.Status = BatchStatus.RECEIVED; order.ReceivedAt = DateTime.UtcNow;
        Db.WeightVerifications.Add(new WeightVerification { Id = Guid.NewGuid(), BatchOrderId = order.Id, DepotWeightKg = order.Batch.ActualWeightKg ?? order.Batch.EstimatedWeightKg, FactoryWeightKg = 0, DifferencePercentage = 0, IsVerified = false });
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã xác nhận nhận hàng.", data = new { order.Id, status = order.Status.ToString(), order.ReceivedAt } });
    }

    [HttpPost("{id:guid}/settle")]
    public async Task<IActionResult> Settle(Guid id, SettlementRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var order = await Db.BatchOrders.Include(x => x.WeightVerification).Include(x => x.Batch)
            .SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (order.Status != BatchStatus.VERIFIED || order.WeightVerification?.IsVerified != true)
            return Conflict(new { success = false, message = "Chỉ quyết toán lô đã được KCS chấp nhận." });
        var net = order.WeightVerification.FactoryWeightKg;
        var total = decimal.Round(net * request.AgreedPricePerKg, 0, MidpointRounding.AwayFromZero);
        var feePercent = configuration.GetValue<decimal?>("Factory:FeePercentage") ?? 5m;
        var fee = decimal.Round(total * feePercent / 100m, 0, MidpointRounding.AwayFromZero);
        order.AgreedPrice = request.AgreedPricePerKg;
        order.TotalAmount = total;
        order.FeeAmount = fee;
        order.NetPayableAmount = total - fee;
        order.PaymentReference = request.PaymentReference.Trim();
        order.SettledAt = DateTime.UtcNow;
        order.Status = BatchStatus.PAID;
        var payerId = await Db.Factories.Where(x => x.Id == factory.Id).Select(x => x.UserId).SingleAsync(ct);
        Db.PlatformFeeLogs.Add(new PlatformFeeLog { Id = Guid.NewGuid(), BatchOrderId = order.Id, PayerId = payerId, TransactionAmount = total, FeePercentage = feePercent, FeeAmount = fee });
        var invoice = await Db.Invoices.SingleOrDefaultAsync(x => x.BatchOrderId == order.Id, ct);
        if (invoice is null) Db.Invoices.Add(new Invoice { Id = Guid.NewGuid(), BatchOrderId = order.Id, Subtotal = total, VatAmount = 0, TotalAmount = total, Status = InvoiceStatus.PENDING });
        else { invoice.Subtotal = total; invoice.TotalAmount = total; }
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã ghi nhận quyết toán.", data = new { order.Id, order.TotalAmount, order.FeeAmount, order.NetPayableAmount, order.PaymentReference, order.SettledAt, feePercentage = feePercent } });
    }

    [HttpPut("{id:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid id, InvoiceRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var order = await Db.BatchOrders.SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (order.Status != BatchStatus.VERIFIED && order.Status != BatchStatus.PAID && order.Status != BatchStatus.REJECTED)
            return Conflict(new { success = false, message = "Chỉ đính kèm hóa đơn sau khi chốt KCS." });
        var invoice = await Db.Invoices.SingleOrDefaultAsync(x => x.BatchOrderId == order.Id, ct);
        if (invoice is null) { invoice = new Invoice { Id = Guid.NewGuid(), BatchOrderId = order.Id }; Db.Invoices.Add(invoice); }
        invoice.InvoiceNumber = request.InvoiceNumber?.Trim();
        invoice.InvoiceFileUrl = request.InvoiceFileUrl.Trim();
        invoice.Status = InvoiceStatus.UPLOADED;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã đính kèm hóa đơn.", data = new { invoice.Id, invoice.InvoiceNumber, invoice.InvoiceFileUrl, status = invoice.Status.ToString() } });
    }
}
