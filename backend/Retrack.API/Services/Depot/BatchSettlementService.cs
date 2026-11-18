using System.Text.Json;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class BatchSettlementService(IDepotBatchRepository repository, IDepotUnitOfWork work, IDepotService scope)
{
    public async Task<Retrack.API.DTOs.PagedResult<BatchWorkflowEventDto>> HistoryAsync(Guid owner, Guid depot, Guid id, DepotQuery query)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (await repository.DetailAsync(depot, id) == null) throw new KeyNotFoundException("Không tìm thấy lô trong kho.");
        return await repository.WorkflowEventsAsync(id, query);
    }
    public async Task DecideAsync(Guid owner, Guid depot, Guid id, SettlementDecisionDto dto)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (dto.Revision < 1 || dto.Reason?.Length > 2000 || (!dto.Accept && string.IsNullOrWhiteSpace(dto.Reason)))
            throw new ArgumentException("Cần phiên bản giá và lý do khi yêu cầu nhà máy điều chỉnh.");
        await using var tx = await work.BeginAsync(); await repository.LockDepotAsync(depot);
        var batch = await repository.LockBatchAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        if (batch.DepotId != depot) throw new DepotForbiddenException();
        if (batch.SettlementWorkflowVersion < 1 || batch.SettlementRevision != dto.Revision || batch.NetAmount != dto.Amount)
            throw new DepotConflictException("Đề xuất đã thay đổi. Hãy tải lại và đối chiếu số tiền, phiên bản mới.");
        if ((dto.Accept && batch.SettlementApprovedAt != null && batch.Status is "SETTLEMENT_ACCEPTED" or "COMPLETED" or "PAID") ||
            (!dto.Accept && batch.Status == "SETTLEMENT_DISPUTED" && batch.SettlementDecisionReason == dto.Reason?.Trim())) return;
        if (batch.Status != "SETTLEMENT_PENDING") throw new DepotConflictException("Chỉ phản hồi đề xuất giá đang chờ kho xác nhận.");
        if (dto.Accept && batch.VatTreatment != "NOT_APPLICABLE" && string.IsNullOrWhiteSpace(batch.QualityCheck?.InvoiceFileUrl))
            throw new DepotConflictException("Đề xuất có VAT cần tệp hóa đơn để kho đối chiếu trước khi đồng ý.");
        var from = batch.Status;
        batch.Status = dto.Accept ? "SETTLEMENT_ACCEPTED" : "SETTLEMENT_DISPUTED";
        batch.SettlementApprovedAt = dto.Accept ? DateTime.UtcNow : null;
        batch.SettlementApprovedBy = dto.Accept ? owner : null;
        // Đề xuất/đồng ý giá chưa phải khoản tiền đã nhận để ghi nhận doanh thu.
        batch.SettledAt = null;
        batch.SettlementDecisionReason = dto.Reason?.Trim(); batch.UpdatedAt = DateTime.UtcNow;
        repository.AddWorkflowEvent(Event(batch, owner, dto.Accept ? "PRICE_ACCEPTED" : "PRICE_DISPUTED", from, dto.Reason));
        await repository.QueueFactoryBatchNoticeAsync(batch.TargetFactoryId!.Value, batch,
            dto.Accept ? "Vựa đã đồng ý giá và số tiền" : "Vựa yêu cầu điều chỉnh giá",
            dto.Accept ? $"Lô {batch.Code}: vựa đã xác nhận đề xuất phiên bản {dto.Revision}. Nhà máy có thể chuyển tiền và gửi chứng từ."
                : $"Lô {batch.Code}: {dto.Reason?.Trim()}. Vui lòng gửi lại đề xuất để vựa xem xét.");
        await work.SaveAsync(); await tx.CommitAsync();
    }
    public async Task ConfirmPaymentAsync(Guid owner, Guid depot, Guid id, decimal amount, string reference)
    {
        await scope.RequireOwnerAsync(owner, depot);
        await using var tx = await work.BeginAsync(); await repository.LockDepotAsync(depot);
        var batch = await repository.LockBatchAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        if (batch.DepotId != depot) throw new DepotForbiddenException();
        if (batch.Status is not ("COMPLETED" or "PAID") || (batch.SettlementWorkflowVersion == 0 && batch.SettledAt == null) ||
            string.IsNullOrWhiteSpace(batch.QualityCheck?.PaymentProofUrl) ||
            (batch.SettlementWorkflowVersion > 0 && batch.SettlementApprovedAt == null))
            throw new DepotConflictException("Cần giá đã được đồng ý và chứng từ chuyển khoản trước khi xác nhận nhận tiền.");
        if (amount <= 0 || batch.NetAmount != amount || string.IsNullOrWhiteSpace(reference) || reference.Trim() != batch.PaymentReference)
            throw new DepotConflictException("Số tiền hoặc mã tham chiếu không khớp. Hãy đối chiếu tài khoản ngân hàng.");
        if (batch.Status == "PAID") return;
        var from = batch.Status;
        if (batch.SettlementWorkflowVersion > 0 && await repository.FindFeeAsync(id) == null)
        {
            var factoryOwner = await repository.FactoryOwnerAsync(batch.TargetFactoryId!.Value)
                ?? throw new DepotConflictException("Không tìm thấy nhà máy nhận lô.");
            repository.AddFee(new() { SourceType = "BATCH_ORDER", SourceId = id, FeeAmount = batch.PlatformFeeAmount ?? 0,
                PayerId = factoryOwner, TransactionAmount = batch.GrossAmount, FeePercentage = batch.QualityCheck!.PlatformFeePercentage,
                FeePolicy = batch.FeePolicy, Description = "Phí lô đã được kho xác nhận nhận tiền" });
        }
        batch.Status = "PAID"; batch.DepotPaymentConfirmedAt = DateTime.UtcNow;
        if (batch.SettlementWorkflowVersion > 0) batch.SettledAt = batch.DepotPaymentConfirmedAt;
        batch.DepotPaymentConfirmedBy = owner; batch.UpdatedAt = DateTime.UtcNow;
        if (batch.SettlementWorkflowVersion > 0)
            repository.AddWorkflowEvent(Event(batch, owner, "PAYMENT_CONFIRMED", from, "Kho đã đối chiếu và nhận đủ tiền thực tế."));
        await repository.QueuePaymentConfirmedNoticeAsync(batch);
        await work.SaveAsync(); await tx.CommitAsync();
    }
    public static BatchWorkflowEvent Event(InventoryBatch batch, Guid actor, string action, string from, string? note) => new()
    {
        BatchId = batch.Id, ActorId = actor, Action = action, FromStatus = from, ToStatus = batch.Status,
        Revision = batch.SettlementRevision, Note = note?.Trim(), PricePerKg = batch.AgreedPricePerKg, NetAmount = batch.NetAmount,
        SnapshotJson = JsonSerializer.Serialize(new
        {
            batch.AgreedPricePerKg, batch.ActualWeightKg, batch.GrossAmount, batch.VatTreatment, batch.VatPercentage,
            batch.VatAmount, batch.NetAmount, batch.PlatformFeeAmount, batch.FeePolicy, batch.PaymentReference,
            InvoiceNumber = batch.QualityCheck?.InvoiceNumber, InvoiceDate = batch.QualityCheck?.InvoiceDate,
            SellerTaxCode = batch.QualityCheck?.SellerTaxCode, VatLegalBasis = batch.QualityCheck?.VatLegalBasis
            , Quality = batch.QualityCheck == null ? null : new { batch.QualityCheck.IsAccepted, batch.QualityCheck.Grade,
                batch.QualityCheck.PurityPercent, batch.QualityCheck.MoisturePercent, batch.QualityCheck.ContaminationPercent,
                batch.QualityCheck.ActualWeightKg, batch.QualityCheck.QualityNote, batch.QualityCheck.Resolution },
            batch.ReturnHandedOverAt, batch.ReturnHandoverProofUrl, batch.ReturnHandoverReference, batch.ReturnHandoverNote,
            batch.ReturnReceivedAt, batch.ReturnReceivedBy, batch.ReturnReceiptReference, batch.ReturnReceiptNote
        })
    };
}
