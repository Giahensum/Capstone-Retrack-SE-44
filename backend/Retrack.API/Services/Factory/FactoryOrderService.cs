using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Factory;

public class FactoryOrderService(AppDbContext db, FactoryAttachmentService? attachments = null, IFactoryOrderReadRepository? reads = null) : FactoryServiceBase(db), IFactoryOrderService
{
    private readonly IFactoryOrderReadRepository orderReads = reads ?? new FactoryOrderReadRepository(db);
    public async Task<ServiceResult<PageResponse<OrderResponse>>> ListAsync(Guid userId, OrderQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var status = query.DisplayStatus ?? (query.Status == BatchStatus.IN_PROGRESS ? "IN_TRANSIT" : query.Status?.ToString());
        var page = await orderReads.ListAsync(factory.Id, query, status, ct);
        return ServiceResult<PageResponse<OrderResponse>>.Success(data: new PageResponse<OrderResponse>
        {
            Items = page.Items.Select(FactoryOrderMapper.Map).ToList(),
            TotalCount = page.TotalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = (int)Math.Ceiling((double)page.TotalCount / query.PageSize)
        });
    }

    public async Task<ServiceResult<OrderResponse>> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        var batch = await orderReads.GetAsync(factory.Id, id, ct);
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
        if (batch.Status is "RETURN_REQUESTED" or "RETURNING" or "RETURNED")
            return ServiceResult<OrderSettlementResponse>.Conflict("Lô đang trả hoặc đã nhận trả; không đề xuất quyết toán.");
        if (batch.SettlementWorkflowVersion > 0 && batch.SettlementProposedAt != null &&
            batch.Status is "SETTLEMENT_PENDING" or "SETTLEMENT_ACCEPTED" or "COMPLETED" or "PAID")
        {
            var q = batch.QualityCheck;
            var same = q != null && batch.AgreedPricePerKg == request.AgreedPricePerKg && batch.VatTreatment == request.VatTreatment.Trim().ToUpperInvariant()
                && batch.VatPercentage == request.VatPercentage && batch.PriceIncludesVat == request.PriceIncludesVat
                && (request.InvoiceNumber == null || q.InvoiceNumber == request.InvoiceNumber.Trim()) && q.InvoiceDate == request.InvoiceDate
                && q.SellerTaxCode == request.SellerTaxCode?.Trim() && q.VatLegalBasis == request.VatLegalBasis?.Trim()
                && string.IsNullOrWhiteSpace(request.PaymentReference) && string.IsNullOrWhiteSpace(request.PaymentProofUrl);
            if (!same) return ServiceResult<OrderSettlementResponse>.Conflict("Đề xuất đang chờ phản hồi hoặc đã được đồng ý; không được sửa giá âm thầm.");
            return ServiceResult<OrderSettlementResponse>.Success(data: SettlementResponse(batch), message: "Trả lại đề xuất đã lưu.");
        }
        if (batch.Status is "PAID" or "COMPLETED")
        {
            var existing = await Db.PlatformTransactions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourceType == "BATCH_ORDER" && x.SourceId == batch.Id, ct);
            var savedQc = batch.QualityCheck;
            var sameRequest = savedQc is not null && existing is not null &&
                batch.AgreedPricePerKg == request.AgreedPricePerKg &&
                string.Equals(batch.PaymentReference, request.PaymentReference?.Trim(), StringComparison.Ordinal) &&
                string.Equals(savedQc.PaymentProofUrl, request.PaymentProofUrl?.Trim(), StringComparison.Ordinal) &&
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
        if ((batch.Status != "VERIFIED" || qc?.IsAccepted != true) && !(batch.SettlementWorkflowVersion > 0 && (batch.Status is "SETTLEMENT_DISPUTED" or "NEGOTIATING") && qc != null))
            return ServiceResult<OrderSettlementResponse>.Conflict("Chỉ quyết toán lô đã được KCS chấp nhận.");
        if (!string.IsNullOrWhiteSpace(request.PaymentReference) || !string.IsNullOrWhiteSpace(request.PaymentProofUrl))
            return ServiceResult<OrderSettlementResponse>.Invalid("Gửi đề xuất giá trước; chỉ chuyển tiền sau khi vựa đồng ý.");
        if (request.AgreedPricePerKg <= 0) return ServiceResult<OrderSettlementResponse>.Invalid("Đơn giá phải lớn hơn 0.");
        if (!string.IsNullOrWhiteSpace(request.PaymentProofUrl) &&
            (string.IsNullOrWhiteSpace(request.PaymentReference) ||
             (attachments is not null && !attachments.IsOwnedUrl(request.PaymentProofUrl, userId))))
            return ServiceResult<OrderSettlementResponse>.Invalid("Chứng từ chuyển khoản cần mã tham chiếu hợp lệ và tệp do nhà máy tải lên.");
        if (string.IsNullOrWhiteSpace(request.PaymentProofUrl) && !string.IsNullOrWhiteSpace(request.PaymentReference))
            return ServiceResult<OrderSettlementResponse>.Invalid("Chưa có chứng từ chuyển khoản; hãy gửi ở bước xác nhận đã chuyển tiền.");
        var vatTreatment = request.VatTreatment.Trim().ToUpperInvariant();
        if (vatTreatment != "NOT_CALCULATED" || request.VatPercentage != 0 || request.PriceIncludesVat)
            return ServiceResult<OrderSettlementResponse>.Invalid("Quyết toán Factory hiện chỉ ghi nhận khối lượng nhân đơn giá, không cộng VAT hoặc phí nền tảng.");
        if (request.InvoiceDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            return ServiceResult<OrderSettlementResponse>.Invalid("Ngày hóa đơn không được ở tương lai.");
        // Lô quyết toán từ phiên bản này chỉ ghi nhận tiền hàng; dữ liệu lô cũ giữ nguyên.
        var feePercent = 0m;
        var quotedTotal = decimal.Round(qc.ActualWeightKg * request.AgreedPricePerKg, 0, MidpointRounding.AwayFromZero);
        var total = quotedTotal;
        var invoiceTotal = quotedTotal;
        var vat = invoiceTotal - total;
        if (invoiceTotal > 1_000_000_000_000m)
            return ServiceResult<OrderSettlementResponse>.Invalid("Tổng giá trị lô tối đa 1.000 tỷ đồng. Hãy kiểm tra lại đơn giá và khối lượng.");
        var money = PlatformFees.Settlement(total, invoiceTotal, feePercent, batch.FeePolicy);
        var fee = money.Fee;
        qc.AgreedPricePerKg = request.AgreedPricePerKg;
        qc.GrossAmount = total;
        qc.VatTreatment = vatTreatment;
        qc.VatPercentage = request.VatPercentage;
        qc.VatAmount = vat;
        qc.InvoiceTotalAmount = invoiceTotal;
        qc.PriceIncludesVat = request.PriceIncludesVat;
        if (!string.IsNullOrWhiteSpace(request.InvoiceNumber)) qc.InvoiceNumber = request.InvoiceNumber.Trim();
        qc.InvoiceDate = request.InvoiceDate;
        qc.SellerTaxCode = request.SellerTaxCode?.Trim();
        qc.VatLegalBasis = request.VatLegalBasis?.Trim();
        qc.PlatformFeePercentage = feePercent;
        qc.PlatformFeeAmount = fee;
        qc.NetAmount = money.SellerReceives;
        qc.PaymentProofUrl = request.PaymentProofUrl?.Trim();
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
        batch.PaymentReference = request.PaymentReference?.Trim();
        var from = batch.Status;
        batch.SettlementWorkflowVersion = 1;
        batch.Status = "SETTLEMENT_PENDING"; batch.SettlementRevision++;
        batch.SettlementProposedAt = DateTime.UtcNow; batch.SettlementApprovedAt = null; batch.SettlementApprovedBy = null;
        batch.SettlementDecisionReason = null; batch.SettledAt = null; batch.UpdatedAt = DateTime.UtcNow;
        Db.BatchWorkflowEvents.Add(Retrack.API.Services.Depot.BatchSettlementService.Event(batch, userId, "PRICE_PROPOSED", from, "Nhà máy gửi đề xuất giá sau KCS."));
        await NotifyDepotAsync(batch.DepotId, "Nhà máy đề xuất giá sau KCS", $"Lô {batch.Code}: đề xuất phiên bản {batch.SettlementRevision}, số tiền {batch.NetAmount:N0}đ. Vui lòng xem chi tiết để đồng ý hoặc yêu cầu điều chỉnh.", batch.Id, ct: ct);
        await Db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return ServiceResult<OrderSettlementResponse>.Success(data: SettlementResponse(batch), message: "Đã gửi đề xuất; chờ vựa đồng ý giá.");
    }

    public async Task<ServiceResult<OrderResponse>> ReportPaymentAsync(Guid userId, Guid id, BatchPaymentProofRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        // PLATFORM (money.mode): Factory trả tiền cho lô qua PayOS vào sàn, không nộp chứng từ chuyển khoản tay. LEGACY: không đổi.
        if (await new Retrack.API.Services.Money.MoneyConfigReader(Db).UsesPlatformAsync("BATCH", id, ct))
            return ServiceResult<OrderResponse>.Conflict("Thanh toán qua sàn đang bật: vui lòng thanh toán lô bằng PayOS thay vì tải chứng từ chuyển khoản.");
        var proofUrl = string.IsNullOrWhiteSpace(request.PaymentProofUrl) ? null : request.PaymentProofUrl.Trim();
        if (request.PaymentMethod is not ("BANK_TRANSFER" or "CASH" or "OTHER") || string.IsNullOrWhiteSpace(request.PaymentReference) ||
            request.PaymentMethod == "BANK_TRANSFER" && proofUrl == null || proofUrl != null && attachments is not null && !attachments.IsOwnedUrl(proofUrl, userId))
            return ServiceResult<OrderResponse>.Invalid("Cần hình thức và căn cứ thanh toán; chuyển khoản cần chứng từ hợp lệ.");
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch is null) return ServiceResult<OrderResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.QualityCheck != null && batch.Status is "COMPLETED" or "PAID" &&
            batch.PaymentReference == request.PaymentReference.Trim() && batch.QualityCheck.PaymentProofUrl == proofUrl && (batch.PaymentMethod ?? "BANK_TRANSFER") == request.PaymentMethod)
            return ServiceResult<OrderResponse>.Success(data: FactoryOrderMapper.Map(await orderReads.GetAsync(factory.Id, id, ct)
                ?? throw new InvalidOperationException("Không đọc lại được đơn hàng vừa cập nhật.")), message: "Chứng từ đã được lưu.");
        if ((batch.Status != "COMPLETED" && batch.Status != "SETTLEMENT_ACCEPTED") || batch.QualityCheck is null ||
            (batch.SettlementWorkflowVersion == 0 && batch.SettledAt is null) ||
            (batch.SettlementWorkflowVersion > 0 && batch.SettlementApprovedAt == null))
            return ServiceResult<OrderResponse>.Conflict("Chỉ bổ sung chứng từ cho lô đã quyết toán và đang chờ vựa xác nhận.");
        if (!string.IsNullOrWhiteSpace(batch.QualityCheck.PaymentProofUrl) || batch.Status == "COMPLETED" && batch.PaymentMethod is "CASH" or "OTHER")
            return ServiceResult<OrderResponse>.Conflict("Lô đã có chứng từ; liên hệ vựa để đối chiếu trước khi thay đổi.");
        var previousStatus = batch.Status;
        batch.PaymentReference = request.PaymentReference.Trim();
        batch.QualityCheck.PaymentProofUrl = proofUrl;
        batch.PaymentMethod = request.PaymentMethod;
        batch.Status = "COMPLETED";
        if (batch.SettlementWorkflowVersion > 0) Db.BatchWorkflowEvents.Add(Retrack.API.Services.Depot.BatchSettlementService.Event(batch, userId, "PAYMENT_REPORTED", previousStatus, "Nhà máy báo đã thanh toán trực tiếp; chờ kho xác nhận tiền thực nhận."));
        batch.UpdatedAt = DateTime.UtcNow;
        await NotifyDepotAsync(batch.DepotId, request.PaymentMethod == "BANK_TRANSFER" ? "Nhà máy bổ sung chứng từ chuyển khoản" : "Nhà máy báo đã thanh toán", $"Lô {batch.Code ?? batch.Id.ToString()}: nhà máy báo đã trả tiền. Kiểm tra tiền thực nhận trước khi xác nhận.", batch.Id, ct: ct);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var saved = await orderReads.GetAsync(factory.Id, id, ct)
            ?? throw new InvalidOperationException("Không đọc lại được đơn hàng vừa cập nhật.");
        return ServiceResult<OrderResponse>.Success(data: FactoryOrderMapper.Map(saved), message: "Đã gửi chứng từ; chờ vựa xác nhận nhận tiền.");
    }

    public async Task<ServiceResult<InvoiceUpdatedResponse>> InvoiceAsync(Guid userId, Guid id, InvoiceRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(userId, ct);
        await using var tx = Db.Database.IsRelational() ? await Db.Database.BeginTransactionAsync(ct) : null;
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch is null) return ServiceResult<InvoiceUpdatedResponse>.NotFound("Không tìm thấy đơn hàng.");
        if (batch.QualityCheck is null || batch.Status is not ("VERIFIED" or "NEGOTIATING" or "COMPLETED" or "PAID" or "SETTLEMENT_PENDING" or "SETTLEMENT_ACCEPTED" or "SETTLEMENT_DISPUTED"))
            return ServiceResult<InvoiceUpdatedResponse>.Conflict("Chỉ đính kèm hóa đơn sau khi chốt KCS.");
        if (string.IsNullOrWhiteSpace(request.InvoiceFileUrl))
            return ServiceResult<InvoiceUpdatedResponse>.Invalid("Chọn tệp hóa đơn trước khi lưu.");
        if (attachments is not null && request.InvoiceFileUrl != batch.QualityCheck.InvoiceFileUrl && !attachments.IsOwnedUrl(request.InvoiceFileUrl, userId))
            return ServiceResult<InvoiceUpdatedResponse>.Invalid("Hóa đơn phải được tải lên qua chức năng đính kèm của nhà máy.");
        if (batch.SettlementWorkflowVersion > 0 && batch.SettlementApprovedAt != null &&
            !string.IsNullOrWhiteSpace(batch.QualityCheck.InvoiceFileUrl) && request.InvoiceFileUrl.Trim() != batch.QualityCheck.InvoiceFileUrl)
            return ServiceResult<InvoiceUpdatedResponse>.Conflict("Hóa đơn đã được vựa đối chiếu khi đồng ý giá; không được thay tệp sau đó.");
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
    private static OrderSettlementResponse SettlementResponse(InventoryBatch batch) => new()
    {
        Id = batch.Id, Status = FactoryOrderMapper.Status(batch), TotalAmount = batch.GrossAmount ?? 0, PriceIncludesVat = batch.PriceIncludesVat,
        VatTreatment = batch.VatTreatment ?? "NOT_APPLICABLE", VatPercentage = batch.VatPercentage ?? 0,
        VatAmount = batch.VatAmount ?? 0, InvoiceTotalAmount = batch.InvoiceTotalAmount ?? 0, FeeAmount = batch.PlatformFeeAmount ?? 0,
        NetPayableAmount = batch.NetAmount ?? 0, PaymentReference = batch.PaymentReference, SettledAt = batch.SettledAt,
        FeePercentage = batch.QualityCheck?.PlatformFeePercentage ?? 0, FeePolicy = batch.FeePolicy,
        BuyerTotalAmount = (batch.NetAmount ?? 0) + (batch.PlatformFeeAmount ?? 0)
    };

}
