using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Factory;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/orders")]
[Authorize(Roles = "FACTORY")]
public class FactoryOrderController(Retrack.API.Data.AppDbContext db) : FactoryControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var source = OrderQuery(factory.Id);
        if (query.Status == Retrack.API.Models.Enums.BatchStatus.ACCEPTED)
            source = source.Where(x => x.Status == "ACCEPTED" || x.Status == "PENDING_APPROVAL" || x.Status == "PENDING_FACTORY" || x.Status == "READY_FOR_PICKUP" || x.Status == "TRANSPORT_READY");
        else if (query.Status == Retrack.API.Models.Enums.BatchStatus.IN_PROGRESS)
            source = source.Where(x => x.Status == "IN_PROGRESS" || x.Status == "TRANSPORT_READY" || x.Status == "ACCEPTED" || x.Status == "READY_FOR_PICKUP");
        else if (query.Status == Retrack.API.Models.Enums.BatchStatus.DELIVERED)
            source = source.Where(x => x.Status == "DELIVERED" || x.Status == "IN_PROGRESS");
        var all = await source.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var mappedStatus = query.Status == Retrack.API.Models.Enums.BatchStatus.IN_PROGRESS ? "IN_TRANSIT" : query.Status?.ToString();
        var matches = mappedStatus is not null ? all.Where(x => OrderStatus(x) == mappedStatus).ToList() : all;
        var items = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(OrderView);
        return Ok(new { success = true, data = new { items, totalCount = matches.Count, query.Page, query.PageSize, totalPages = (int)Math.Ceiling((double)matches.Count / query.PageSize) } });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batch = await OrderQuery(factory.Id).SingleOrDefaultAsync(x => x.Id == id, ct);
        return batch is null ? NotFound(new { success = false, message = "Không tìm thấy đơn hàng." }) : Ok(new { success = true, data = OrderView(batch) });
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.Include(x => x.TransportJob)
            .SingleOrDefaultAsync(x => x.Id == id && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (batch.Status == "RECEIVED") return Conflict(new { success = false, message = "Đơn hàng đã được xác nhận nhận hàng." });
        if (batch.TransportJob?.Status != "DELIVERED" && batch.Status is not ("DELIVERED" or "IN_PROGRESS"))
            return Conflict(new { success = false, message = "Chỉ xác nhận nhận hàng sau khi vận chuyển báo đã giao." });
        batch.Status = "RECEIVED";
        batch.FactoryReceivedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã xác nhận nhận hàng.", data = new { id = batch.Id, status = batch.Status, receivedAt = batch.FactoryReceivedAt } });
    }

    [HttpPost("{id:guid}/settle")]
    public async Task<IActionResult> Settle(Guid id, SettlementRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        await using var transaction = await Db.Database.BeginTransactionAsync(ct);
        var batch = await Db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} AND target_factory_id = {factory.Id} FOR UPDATE")
            .Include(x => x.QualityCheck).SingleOrDefaultAsync(ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (batch.Status is "PAID" or "COMPLETED") return Conflict(new { success = false, message = "Đơn hàng đã được quyết toán." });
        var qc = batch.QualityCheck;
        if (batch.Status != "VERIFIED" || qc?.IsAccepted != true)
            return Conflict(new { success = false, message = "Chỉ quyết toán lô đã được KCS chấp nhận." });
        if (request.AgreedPricePerKg <= 0) return BadRequest(new { success = false, message = "Đơn giá phải lớn hơn 0." });
        var feeText = await Db.SystemConfigs.AsNoTracking().Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE")
            .Select(x => x.ConfigValue).FirstOrDefaultAsync(ct);
        var feePercent = decimal.TryParse(feeText, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var configuredFee) ? configuredFee : 1m;
        if (feePercent is < 0 or > 100) feePercent = 1m;
        var total = decimal.Round(qc.ActualWeightKg * request.AgreedPricePerKg, 0, MidpointRounding.AwayFromZero);
        var fee = decimal.Round(total * feePercent / 100m, 0, MidpointRounding.AwayFromZero);
        qc.AgreedPricePerKg = request.AgreedPricePerKg;
        qc.GrossAmount = total;
        qc.PlatformFeePercentage = feePercent;
        qc.PlatformFeeAmount = fee;
        qc.NetAmount = total - fee;
        batch.AgreedPricePerKg = request.AgreedPricePerKg;
        batch.GrossAmount = total;
        batch.PlatformFeeAmount = fee;
        batch.NetAmount = total - fee;
        batch.QualityCheck = qc;
        batch.PaymentReference = request.PaymentReference.Trim();
        batch.SettledAt = DateTime.UtcNow;
        batch.Status = "COMPLETED";
        Db.PlatformTransactions.Add(new Retrack.API.Models.PlatformTransaction
        {
            SourceType = "BATCH_ORDER", SourceId = batch.Id, FeeAmount = fee,
            Description = $"Factory settlement at {feePercent}%"
        });
        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, message = "Đã ghi nhận quyết toán.", data = new { id = batch.Id, status = "PAID", totalAmount = total, feeAmount = fee, netPayableAmount = total - fee, batch.PaymentReference, settledAt = batch.SettledAt, feePercentage = feePercent } });
    }

    [HttpPut("{id:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid id, InvoiceRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.Include(x => x.QualityCheck)
            .SingleOrDefaultAsync(x => x.Id == id && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (batch.QualityCheck is null || batch.Status is not ("VERIFIED" or "COMPLETED"))
            return Conflict(new { success = false, message = "Chỉ đính kèm hóa đơn sau khi chốt KCS." });
        if (string.IsNullOrWhiteSpace(request.InvoiceFileUrl))
            return BadRequest(new { success = false, message = "Chọn tệp hóa đơn trước khi lưu." });
        batch.QualityCheck.InvoiceNumber = request.InvoiceNumber?.Trim();
        batch.QualityCheck.InvoiceFileUrl = request.InvoiceFileUrl.Trim();
        batch.QualityCheck.InvoiceStatus = "UPLOADED";
        if (request.VatAmount.HasValue) batch.QualityCheck.QualityNote = $"VAT:{request.VatAmount.Value}";
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã đính kèm hóa đơn.", data = new { id = batch.Id, invoiceNumber = batch.QualityCheck.InvoiceNumber, status = batch.QualityCheck.InvoiceStatus } });
    }
}
