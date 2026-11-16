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
            "PENDING_APPROVAL" => source.Where(x => x.Status == "PENDING_APPROVAL" || x.Status == "PENDING_FACTORY"),
            "ACCEPTED" => source.Where(x =>
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") &&
                 (x.TransportJob == null || (x.TransportJob.Status != "PICKED_UP" && x.TransportJob.Status != "IN_TRANSIT" && x.TransportJob.Status != "ON_THE_WAY" && x.TransportJob.Status != "IN_PROGRESS" && x.TransportJob.Status != "DELIVERED")))),
            "IN_TRANSIT" => source.Where(x => ((x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && (x.TransportJob == null || x.TransportJob.Status != "DELIVERED")) ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP") && x.TransportJob != null &&
                 (x.TransportJob.Status == "PICKED_UP" || x.TransportJob.Status == "IN_TRANSIT" || x.TransportJob.Status == "ON_THE_WAY" || x.TransportJob.Status == "IN_PROGRESS"))),
            "DELIVERED" => source.Where(x => x.Status == "DELIVERED" ||
                ((x.Status == "ACCEPTED" || x.Status == "TRANSPORT_READY" || x.Status == "READY_FOR_PICKUP" || x.Status == "IN_PROGRESS" || x.Status == "IN_TRANSIT") && x.TransportJob != null && x.TransportJob.Status == "DELIVERED")),
            "PAID" => source.Where(x => x.Status == "PAID"),
            "PAYMENT_REPORTED" => source.Where(x => x.Status == "COMPLETED" && x.QualityCheck != null && x.QualityCheck.PaymentProofUrl != null),
            "SETTLEMENT_RECORDED" => source.Where(x => x.Status == "COMPLETED" && (x.QualityCheck == null || x.QualityCheck.PaymentProofUrl == null)),
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
        var depotId = await Db.InventoryBatches.AsNoTracking().Where(x => x.Id == id && x.TargetFactoryId == factory.Id)
            .Select(x => (Guid?)x.DepotId).SingleOrDefaultAsync(ct);
        if (depotId is null) return ServiceResult<OrderReceivedResponse>.NotFound("Không tìm thấy đơn hàng.");
        await using var transaction = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        if (Db.Database.IsRelational())
            await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").LoadAsync(ct);
        var source = Db.Database.IsRelational()
            ? Db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} AND target_factory_id = {factory.Id} FOR UPDATE")
            : Db.InventoryBatches.Where(x => x.Id == id && x.TargetFactoryId == factory.Id);
        var batch = await source.Include(x => x.TransportJob).SingleOrDefaultAsync(ct);
        if (batch is null) return ServiceResult<OrderReceivedResponse>.NotFound("Không tìm thấy đơn hàng.");
        await Db.Entry(batch).ReloadAsync(ct);
        if (batch.TransportJob is not null) await Db.Entry(batch.TransportJob).ReloadAsync(ct);
        // Không cho nhận lại để mở lại cân/QC hoặc hạ trạng thái lô đã quyết toán.
        if (batch.FactoryReceivedAt != null || batch.SettledAt != null || batch.Status is not
            ("DELIVERED" or "ACCEPTED" or "TRANSPORT_READY" or "READY_FOR_PICKUP" or "IN_PROGRESS" or "IN_TRANSIT"))
            return ServiceResult<OrderReceivedResponse>.Conflict("Lô đã nhận, kiểm định hoặc kết thúc; không thể xác nhận nhận hàng lại.");
        if (batch.TransportJob?.Status != "DELIVERED")
            return ServiceResult<OrderReceivedResponse>.Conflict("Chỉ xác nhận nhận hàng sau khi vận chuyển báo đã giao.");
        batch.Status = "RECEIVED";
        await NotifyDepotAsync(batch.DepotId, "Nhà máy xác nhận hàng đã đến", $"{factory.Name} đã xác nhận nhận hàng lô {batch.Code ?? batch.Id.ToString()}; chờ cân và KCS.", batch.Id, ct: ct);
        batch.FactoryReceivedAt = DateTime.UtcNow;
        var recipients = new List<Guid>();
        var depotOwnerId = await Db.Depots.Where(x => x.Id == batch.DepotId).Select(x => x.OwnerId).SingleAsync(ct);
        recipients.Add(depotOwnerId);
        if (batch.TransportJob?.DriverId is Guid driverId) recipients.Add(driverId);
        var uniqueRecipients = recipients.Distinct().ToArray();
        var existingRecipients = await Db.Notifications.AsNoTracking()
            .Where(x => uniqueRecipients.Contains(x.UserId) && x.TransportJobId == batch.TransportJob!.Id && x.Title == "Nhà máy đã nhận hàng")
            .Select(x => x.UserId)
            .ToListAsync(ct);
        var recipientsAlreadyNotified = existingRecipients.ToHashSet();
        foreach (var recipient in uniqueRecipients.Where(x => !recipientsAlreadyNotified.Contains(x)))
            Db.Notifications.Add(new Notification
            {
                UserId = recipient,
                TransportJobId = batch.TransportJob!.Id,
                Title = "Nhà máy đã nhận hàng",
                Message = $"Lô {batch.Code ?? batch.Id.ToString()} đã được nhà máy xác nhận nhận hàng."
            });
        await Db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
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
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch is null) return ServiceResult<OrderSettlementResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.Status is "PAID" or "COMPLETED")
        {
            var existing = await Db.PlatformTransactions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourceType == "BATCH_ORDER" && x.SourceId == batch.Id, ct);
            var savedQc = batch.QualityCheck;
            var sameRequest = savedQc is not null && existing is not null &&
                batch.AgreedPricePerKg == request.AgreedPricePerKg &&
                string.Equals(batch.PaymentReference, request.PaymentReference.Trim(), StringComparison.Ordinal) &&
                string.Equals(savedQc.PaymentProofUrl, request.PaymentProofUrl.Trim(), StringComparison.Ordinal) &&
                string.Equals(batch.VatTreatment, request.VatTreatment.Trim().ToUpperInvariant(), StringComparison.Ordinal) &&
                batch.VatPercentage == request.VatPercentage && batch.PriceIncludesVat == request.PriceIncludesVat;
            if (!sameRequest) return ServiceResult<OrderSettlementResponse>.Conflict("Đơn hàng đã được quyết toán với dữ liệu khác.");
            return ServiceResult<OrderSettlementResponse>.Success(data: new OrderSettlementResponse
            {
                Id = batch.Id, Status = FactoryOrderMapper.Status(batch), TotalAmount = batch.GrossAmount ?? 0,
                PriceIncludesVat = batch.PriceIncludesVat, VatTreatment = batch.VatTreatment ?? "NOT_APPLICABLE",
                VatPercentage = batch.VatPercentage ?? 0, VatAmount = batch.VatAmount ?? 0,
                InvoiceTotalAmount = batch.InvoiceTotalAmount ?? 0, FeeAmount = batch.PlatformFeeAmount ?? 0,
                NetPayableAmount = batch.NetAmount ?? 0, PaymentReference = batch.PaymentReference,
                SettledAt = batch.SettledAt, FeePercentage = existing!.FeePercentage ?? 0,
                FeePolicy = batch.FeePolicy, BuyerTotalAmount = (batch.NetAmount ?? 0) + (batch.PlatformFeeAmount ?? 0)
            }, message: "Đơn hàng đã được quyết toán; trả lại kết quả đã lưu.");
        }
        var qc = batch.QualityCheck;
        if (batch.Status != "VERIFIED" || qc?.IsAccepted != true)
            return ServiceResult<OrderSettlementResponse>.Conflict("Chỉ quyết toán lô đã được KCS chấp nhận.");
        if (request.AgreedPricePerKg <= 0) return ServiceResult<OrderSettlementResponse>.Invalid("Đơn giá phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(request.PaymentProofUrl) ||
            (attachments is not null && !attachments.IsOwnedUrl(request.PaymentProofUrl, userId)))
            return ServiceResult<OrderSettlementResponse>.Invalid("Cần tải chứng từ chuyển khoản hợp lệ của nhà máy.");
        var vatTreatment = request.VatTreatment.Trim().ToUpperInvariant();
        if (vatTreatment is not ("NOT_APPLICABLE" or "ZERO_RATE" or "TAXABLE"))
            return ServiceResult<OrderSettlementResponse>.Invalid("Cách áp dụng VAT không hợp lệ.");
        if (vatTreatment == "NOT_APPLICABLE" && request.VatPercentage != 0)
            return ServiceResult<OrderSettlementResponse>.Invalid("Giao dịch không áp dụng VAT phải có thuế suất 0%.");
        if (vatTreatment == "ZERO_RATE" && request.VatPercentage != 0)
            return ServiceResult<OrderSettlementResponse>.Invalid("Giao dịch VAT 0% phải có thuế suất 0%.");
        if (vatTreatment == "TAXABLE" && request.VatPercentage <= 0)
            return ServiceResult<OrderSettlementResponse>.Invalid("Giao dịch chịu VAT phải có thuế suất lớn hơn 0%.");
        if (vatTreatment == "TAXABLE" && request.VatPercentage is not (5m or 8m or 10m))
            return ServiceResult<OrderSettlementResponse>.Invalid("Thuế suất VAT chỉ được chọn 5%, 8% hoặc 10% theo hóa đơn hợp lệ.");
        if (vatTreatment != "NOT_APPLICABLE" && (string.IsNullOrWhiteSpace(request.InvoiceNumber) ||
            request.InvoiceDate is null || string.IsNullOrWhiteSpace(request.SellerTaxCode)))
            return ServiceResult<OrderSettlementResponse>.Invalid("Giao dịch có VAT phải có số hóa đơn, ngày hóa đơn và mã số thuế bên bán.");
        if (request.InvoiceDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            return ServiceResult<OrderSettlementResponse>.Invalid("Ngày hóa đơn không được ở tương lai.");
        if ((vatTreatment == "NOT_APPLICABLE" || request.VatPercentage == 8m) && string.IsNullOrWhiteSpace(request.VatLegalBasis))
            return ServiceResult<OrderSettlementResponse>.Invalid("Cần ghi căn cứ áp dụng không chịu VAT hoặc mức giảm 8%.");
        var feeText = await Db.SystemConfigs.AsNoTracking().Where(x => x.ConfigKey == "PLATFORM_FEE_PERCENTAGE")
            .Select(x => x.ConfigValue).FirstOrDefaultAsync(ct);
        var feePercent = batch.FeePolicy == PlatformFees.BuyerSeparate
            ? batch.FeePercentageSnapshot ?? throw new InvalidOperationException("Lô mới thiếu snapshot tỷ lệ phí.")
            : PlatformFees.Rate(feeText);
        var quotedTotal = decimal.Round(qc.ActualWeightKg * request.AgreedPricePerKg, 0, MidpointRounding.AwayFromZero);
        var total = request.PriceIncludesVat && request.VatPercentage > 0
            ? decimal.Round(quotedTotal / (1m + request.VatPercentage / 100m), 0, MidpointRounding.AwayFromZero)
            : quotedTotal;
        var invoiceTotal = request.PriceIncludesVat ? quotedTotal :
            total + decimal.Round(total * request.VatPercentage / 100m, 0, MidpointRounding.AwayFromZero);
        var vat = invoiceTotal - total;
        var money = PlatformFees.Settlement(total, invoiceTotal, feePercent, batch.FeePolicy);
        var fee = money.Fee;
        qc.AgreedPricePerKg = request.AgreedPricePerKg;
        qc.GrossAmount = total;
        qc.VatTreatment = vatTreatment;
        qc.VatPercentage = request.VatPercentage;
        qc.VatAmount = vat;
        qc.InvoiceTotalAmount = invoiceTotal;
        qc.PriceIncludesVat = request.PriceIncludesVat;
        qc.InvoiceNumber = request.InvoiceNumber?.Trim();
        qc.InvoiceDate = request.InvoiceDate;
        qc.SellerTaxCode = request.SellerTaxCode?.Trim();
        qc.VatLegalBasis = request.VatLegalBasis?.Trim();
        qc.PlatformFeePercentage = feePercent;
        qc.PlatformFeeAmount = fee;
        qc.NetAmount = money.SellerReceives;
        qc.PaymentProofUrl = request.PaymentProofUrl.Trim();
        batch.AgreedPricePerKg = request.AgreedPricePerKg;
        batch.GrossAmount = total;
        batch.VatTreatment = vatTreatment;
        batch.VatPercentage = request.VatPercentage;
        batch.VatAmount = vat;
        batch.InvoiceTotalAmount = invoiceTotal;
        batch.PriceIncludesVat = request.PriceIncludesVat;
        batch.PlatformFeeAmount = fee;
        batch.NetAmount = money.SellerReceives;
        batch.QualityCheck = qc;
        batch.PaymentReference = request.PaymentReference.Trim();
        batch.SettledAt = DateTime.UtcNow;
        batch.Status = "COMPLETED";
        await NotifyDepotAsync(batch.DepotId, "Nhà máy báo đã chuyển khoản", $"Lô {batch.Code ?? batch.Id.ToString()} đã được {factory.Name} ghi nhận quyết toán và gửi chứng từ chuyển khoản. Vui lòng đối chiếu với tài khoản ngân hàng trước khi xác nhận nhận tiền.", batch.Id, ct: ct);
        Db.PlatformTransactions.Add(new Retrack.API.Models.PlatformTransaction
        {
            SourceType = "BATCH_ORDER", SourceId = batch.Id, FeeAmount = fee,
            PayerId = factory.OwnerId, TransactionAmount = total, FeePercentage = feePercent, FeePolicy = batch.FeePolicy,
            Description = $"Factory settlement at {feePercent}%"
        });
        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<OrderSettlementResponse>.Success(data: new OrderSettlementResponse
        {
            Id = batch.Id,
            Status = "PAYMENT_REPORTED",
            TotalAmount = total,
            PriceIncludesVat = request.PriceIncludesVat,
            VatTreatment = vatTreatment,
            VatPercentage = request.VatPercentage,
            VatAmount = vat,
            InvoiceTotalAmount = invoiceTotal,
            FeeAmount = fee,
            NetPayableAmount = money.SellerReceives,
            BuyerTotalAmount = money.BuyerTotal, FeePolicy = batch.FeePolicy,
            PaymentReference = batch.PaymentReference,
            SettledAt = batch.SettledAt,
            FeePercentage = feePercent
        }, message: "Đã gửi chứng từ chuyển khoản; chờ vựa xác nhận đã nhận tiền.");
    }

    public async Task<ServiceResult<OrderResponse>> ReportPaymentAsync(Guid userId, Guid id, BatchPaymentProofRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        if (string.IsNullOrWhiteSpace(request.PaymentReference) || string.IsNullOrWhiteSpace(request.PaymentProofUrl) ||
            (attachments is not null && !attachments.IsOwnedUrl(request.PaymentProofUrl, userId)))
            return ServiceResult<OrderResponse>.Invalid("Cần mã tham chiếu và chứng từ chuyển khoản hợp lệ.");
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch is null) return ServiceResult<OrderResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.Status != "COMPLETED" || batch.QualityCheck is null || batch.SettledAt is null)
            return ServiceResult<OrderResponse>.Conflict("Chỉ bổ sung chứng từ cho lô đã quyết toán và đang chờ vựa xác nhận.");
        if (!string.IsNullOrWhiteSpace(batch.QualityCheck.PaymentProofUrl))
            return ServiceResult<OrderResponse>.Conflict("Lô đã có chứng từ; liên hệ vựa để đối chiếu trước khi thay đổi.");
        batch.PaymentReference = request.PaymentReference.Trim();
        batch.QualityCheck.PaymentProofUrl = request.PaymentProofUrl.Trim();
        batch.UpdatedAt = DateTime.UtcNow;
        await NotifyDepotAsync(batch.DepotId, "Nhà máy bổ sung chứng từ chuyển khoản", $"Lô {batch.Code ?? batch.Id.ToString()} đã có chứng từ chuyển khoản. Vui lòng đối chiếu tài khoản trước khi xác nhận nhận tiền.", batch.Id, ct: ct);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var saved = await FactoryOrderQueries.ForFactory(Db, factory.Id).SingleAsync(x => x.Id == id, ct);
        return ServiceResult<OrderResponse>.Success(data: FactoryOrderMapper.Map(saved), message: "Đã gửi chứng từ; chờ vựa xác nhận nhận tiền.");
    }

    public async Task<ServiceResult<InvoiceUpdatedResponse>> InvoiceAsync(Guid userId, Guid id, InvoiceRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        await using var tx = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch is null) return ServiceResult<InvoiceUpdatedResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.QualityCheck is null || batch.Status is not ("VERIFIED" or "COMPLETED" or "PAID"))
            return ServiceResult<InvoiceUpdatedResponse>.Conflict("Chỉ đính kèm hóa đơn sau khi chốt KCS.");
        if (string.IsNullOrWhiteSpace(request.InvoiceFileUrl))
            return ServiceResult<InvoiceUpdatedResponse>.Invalid("Chọn tệp hóa đơn trước khi lưu.");
        if (attachments is not null && request.InvoiceFileUrl != batch.QualityCheck.InvoiceFileUrl && !attachments.IsOwnedUrl(request.InvoiceFileUrl, userId))
            return ServiceResult<InvoiceUpdatedResponse>.Invalid("Hóa đơn phải được tải lên qua chức năng đính kèm của nhà máy.");
        // Sau quyết toán, giữ số hóa đơn đã được đối soát; tên tệp tải lên không được ghi đè.
        var invoiceNumber = batch.QualityCheck.InvoiceNumber ?? request.InvoiceNumber?.Trim();
        var changed = batch.QualityCheck.InvoiceNumber != invoiceNumber || batch.QualityCheck.InvoiceFileUrl != request.InvoiceFileUrl.Trim();
        batch.QualityCheck.InvoiceNumber = invoiceNumber;
        batch.QualityCheck.InvoiceFileUrl = request.InvoiceFileUrl.Trim();
        batch.QualityCheck.InvoiceStatus = "UPLOADED";
        if (changed) await NotifyDepotAsync(batch.DepotId, "Nhà máy đã bổ sung hóa đơn", $"{factory.Name} đã cập nhật hóa đơn lô {batch.Code ?? batch.Id.ToString()}. Kho có thể tải chứng từ trong chi tiết lô.", batch.Id, ct: ct);
        await Db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return ServiceResult<InvoiceUpdatedResponse>.Success(data: new InvoiceUpdatedResponse
        {
            Id = batch.Id,
            InvoiceNumber = batch.QualityCheck.InvoiceNumber,
            Status = batch.QualityCheck.InvoiceStatus
        }, message: "Đã đính kèm hóa đơn.");
    }
}
