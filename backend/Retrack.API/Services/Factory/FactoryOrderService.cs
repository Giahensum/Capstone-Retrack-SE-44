using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public class FactoryOrderService(AppDbContext db, FactoryAttachmentService? attachments = null) : FactoryServiceBase(db), IFactoryOrderService
{
    public async Task<ServiceResult<PageResponse<OrderResponse>>> ListAsync(Guid userId, OrderQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        IQueryable<InventoryBatch> source = FactoryOrderQueries.ForFactory(Db, factory.Id);
        var status = query.DisplayStatus ?? (query.Status == BatchStatus.IN_PROGRESS ? "IN_TRANSIT" : query.Status?.ToString());
        if (query.Stage == "qc")
            source = source.Where(x => x.Status == "RECEIVED" || x.Status == "WEIGHED" || x.Status == "VERIFIED" || x.Status == "REJECTED" ||
                x.Status == "DELIVERED" || ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP" || x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED"));
        else if (query.Stage == "settlements")
            source = source.Where(x => x.Status == "VERIFIED" || x.Status == "COMPLETED" || x.Status == "PAID");
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.Id.ToString().Contains(search) || (x.Code != null && x.Code.Contains(search)) || x.Depot.Name.Contains(search));
        }
        source = status switch
        {
            "ACCEPTED" => source.Where(x => x.Status == "PENDING_APPROVAL" || x.Status == "PENDING_FACTORY" ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") &&
                 (x.TransportJob == null || (x.TransportJob.Status != "PICKED_UP" && x.TransportJob.Status != "IN_TRANSIT" && x.TransportJob.Status != "ON_THE_WAY" && x.TransportJob.Status != "IN_PROGRESS" && x.TransportJob.Status != "DELIVERED")))),
            "IN_TRANSIT" => source.Where(x => ((x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && (x.TransportJob == null || x.TransportJob.Status != "DELIVERED")) ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") && x.TransportJob != null &&
                 (x.TransportJob.Status == "PICKED_UP" || x.TransportJob.Status == "IN_TRANSIT" || x.TransportJob.Status == "ON_THE_WAY" || x.TransportJob.Status == "IN_PROGRESS"))),
            "DELIVERED" => source.Where(x => x.Status == "DELIVERED" ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP" || x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED")),
            "PAID" => source.Where(x => x.Status == "PAID" || x.Status == "COMPLETED"),
            "REJECTED" => source.Where(x => x.Status == "REJECTED" || x.Status == "CANCELLED"),
            "RECEIVED" or "WEIGHED" or "VERIFIED" => source.Where(x => x.Status == status),
            _ => source
        };
        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return ServiceResult<PageResponse<OrderResponse>>.Success(data: new PageResponse<OrderResponse>
        {
            Items = rows.Select(FactoryOrderMapper.Map).ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)total / query.PageSize)
        });
    }

    public async Task<ServiceResult<OrderResponse>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var batch = await FactoryOrderQueries.ForFactory(Db, factory.Id).SingleOrDefaultAsync(x => x.Id == id, ct);
        return batch is null ? ServiceResult<OrderResponse>.NotFound("Không tìm thấy đơn hàng.") : ServiceResult<OrderResponse>.Success(data: FactoryOrderMapper.Map(batch));
    }

    public async Task<ServiceResult<OrderReceivedResponse>> ReceiveAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var batch = await Db.InventoryBatches.Include(x => x.TransportJob)
            .SingleOrDefaultAsync(x => x.Id == id && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return ServiceResult<OrderReceivedResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.Status == "RECEIVED") return ServiceResult<OrderReceivedResponse>.Conflict("Đơn hàng đã được xác nhận nhận hàng.");
        if (batch.TransportJob?.Status != "DELIVERED")
            return ServiceResult<OrderReceivedResponse>.Conflict("Chỉ xác nhận nhận hàng sau khi vận chuyển báo đã giao.");
        batch.Status = "RECEIVED";
        batch.FactoryReceivedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);
        return ServiceResult<OrderReceivedResponse>.Success(data: new OrderReceivedResponse
        {
            Id = batch.Id,
            Status = batch.Status,
            ReceivedAt = batch.FactoryReceivedAt
        }, message: "Đã xác nhận nhận hàng.");
    }

    public async Task<ServiceResult<OrderSettlementResponse>> SettleAsync(Guid userId, Guid id, SettlementRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        await using var transaction = await Db.Database.BeginTransactionAsync(ct);
        var batch = await Db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} AND target_factory_id = {factory.Id} FOR UPDATE")
            .Include(x => x.QualityCheck).SingleOrDefaultAsync(ct);
        if (batch is null) return ServiceResult<OrderSettlementResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.Status is "PAID" or "COMPLETED") return ServiceResult<OrderSettlementResponse>.Conflict("Đơn hàng đã được quyết toán.");
        var qc = batch.QualityCheck;
        if (batch.Status != "VERIFIED" || qc?.IsAccepted != true)
            return ServiceResult<OrderSettlementResponse>.Conflict("Chỉ quyết toán lô đã được KCS chấp nhận.");
        if (request.AgreedPricePerKg <= 0) return ServiceResult<OrderSettlementResponse>.Invalid("Đơn giá phải lớn hơn 0.");
        var feeText = await Db.SystemConfigs.AsNoTracking().Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE")
            .Select(x => x.ConfigValue).FirstOrDefaultAsync(ct);
        var feePercent = decimal.TryParse(feeText, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var configuredFee) ? configuredFee : 5m;
        if (feePercent is < 0 or > 100) feePercent = 5m;
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
        return ServiceResult<OrderSettlementResponse>.Success(data: new OrderSettlementResponse
        {
            Id = batch.Id,
            Status = "PAID",
            TotalAmount = total,
            FeeAmount = fee,
            NetPayableAmount = total - fee,
            PaymentReference = batch.PaymentReference,
            SettledAt = batch.SettledAt,
            FeePercentage = feePercent
        }, message: "Đã ghi nhận quyết toán.");
    }

    public async Task<ServiceResult<InvoiceUpdatedResponse>> InvoiceAsync(Guid userId, Guid id, InvoiceRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var batch = await Db.InventoryBatches.Include(x => x.QualityCheck)
            .SingleOrDefaultAsync(x => x.Id == id && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return ServiceResult<InvoiceUpdatedResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.QualityCheck is null || batch.Status is not ("VERIFIED" or "COMPLETED"))
            return ServiceResult<InvoiceUpdatedResponse>.Conflict("Chỉ đính kèm hóa đơn sau khi chốt KCS.");
        if (string.IsNullOrWhiteSpace(request.InvoiceFileUrl))
            return ServiceResult<InvoiceUpdatedResponse>.Invalid("Chọn tệp hóa đơn trước khi lưu.");
        if (attachments is not null && request.InvoiceFileUrl != batch.QualityCheck.InvoiceFileUrl && !attachments.IsOwnedUrl(request.InvoiceFileUrl, userId))
            return ServiceResult<InvoiceUpdatedResponse>.Invalid("Hóa đơn phải được tải lên qua chức năng đính kèm của nhà máy.");
        batch.QualityCheck.InvoiceNumber = request.InvoiceNumber?.Trim();
        batch.QualityCheck.InvoiceFileUrl = request.InvoiceFileUrl.Trim();
        batch.QualityCheck.InvoiceStatus = "UPLOADED";
        if (request.VatAmount.HasValue) batch.QualityCheck.QualityNote = $"VAT:{request.VatAmount.Value}";
        await Db.SaveChangesAsync(ct);
        return ServiceResult<InvoiceUpdatedResponse>.Success(data: new InvoiceUpdatedResponse
        {
            Id = batch.Id,
            InvoiceNumber = batch.QualityCheck.InvoiceNumber,
            Status = batch.QualityCheck.InvoiceStatus
        }, message: "Đã đính kèm hóa đơn.");
    }
}
