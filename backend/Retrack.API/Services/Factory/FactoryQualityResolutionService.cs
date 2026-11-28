using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shared;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Factory;

public sealed class FactoryQualityResolutionService(AppDbContext db, FactoryAttachmentService attachments, IFactoryOrderReadRepository? reads = null,
    IFactoryOrderRepository? orders = null, IFactoryUnitOfWork? work = null) : FactoryServiceBase(db, orders, work)
{
    private readonly IFactoryOrderReadRepository orderReads = reads ?? new FactoryOrderReadRepository(db);
    private Task<bool> HasFeeAsync(Guid id, CancellationToken ct) => Workflow.HasFeeAsync(id, ct);
    private static bool HasMoney(InventoryBatch b) => b.SettledAt != null || b.SettlementApprovedAt != null || b.DepotPaymentConfirmedAt != null ||
        !string.IsNullOrWhiteSpace(b.PaymentReference) || !string.IsNullOrWhiteSpace(b.QualityCheck?.PaymentProofUrl);
    private async Task<ServiceResult<OrderResponse>> SavedAsync(Guid id, Guid factory, string message, CancellationToken ct) =>
        ServiceResult<OrderResponse>.Success(data: FactoryOrderMapper.Map(await orderReads.GetAsync(factory, id, ct)
            ?? throw new InvalidOperationException("Không đọc lại được lô vừa cập nhật.")), message: message);
    public async Task<ServiceResult<PageResponse<Retrack.API.DTOs.Depot.BatchWorkflowEventDto>>> HistoryAsync(Guid actor, Guid id, PageQuery query, CancellationToken ct)
    {
        var factory = await CurrentFactory(actor, ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 100) return ServiceResult<PageResponse<Retrack.API.DTOs.Depot.BatchWorkflowEventDto>>.Invalid("Phân trang không hợp lệ.");
        if (!await Workflow.OwnsBatchAsync(factory.Id, id, ct)) return ServiceResult<PageResponse<Retrack.API.DTOs.Depot.BatchWorkflowEventDto>>.NotFound("Không tìm thấy lô của nhà máy.");
        var page = await Workflow.HistoryAsync(id, query.Page, query.PageSize, ct);
        return ServiceResult<PageResponse<Retrack.API.DTOs.Depot.BatchWorkflowEventDto>>.Success(data: new()
        {
            Page=query.Page, PageSize=query.PageSize, TotalCount=page.TotalCount, TotalPages=(int)Math.Ceiling(page.TotalCount/(double)query.PageSize),
            Items=page.Items
        });
    }

    public async Task<ServiceResult<OrderResponse>> ChooseAsync(Guid actor, Guid id, QualityResolutionRequest input, CancellationToken ct)
    {
        if (input.Resolution is not ("RETURN" or "RENEGOTIATE") || string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length > 2000)
            return ServiceResult<OrderResponse>.Invalid("Cần hướng xử lý và ghi chú tối đa 2.000 ký tự.");
        var factory = await CurrentFactory(actor, ct);
        await using var tx = await Work.BeginAsync(ct);
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch == null) return ServiceResult<OrderResponse>.NotFound("Không tìm thấy lô của nhà máy.");
        if (batch.QualityCheck?.IsAccepted != false || batch.QualityCheck.Grade == "PENDING" || batch.FactoryReceivedAt == null ||
            batch.Status is not ("REJECTED" or "NEGOTIATING" or "RETURN_REQUESTED" or "SETTLEMENT_DISPUTED" or "SETTLEMENT_PENDING") || HasMoney(batch) || await HasFeeAsync(id, ct))
            return ServiceResult<OrderResponse>.Conflict("Chỉ đổi hướng xử lý KCS không đạt trước khi hai bên đồng ý giá hoặc chuyển tiền.");
        var status = input.Resolution == "RETURN" ? "RETURN_REQUESTED" : "NEGOTIATING";
        if (batch.Status == status && await Workflow.HasResolutionAsync(id, status, input.Note.Trim(), ct))
            return await SavedAsync(id, factory.Id, "Hướng xử lý đã được lưu.", ct);
        var from = batch.Status;
        batch.SettlementWorkflowVersion = 1;
        batch.QualityCheck.Resolution = input.Resolution;
        batch.Status = status; batch.UpdatedAt = DateTime.UtcNow;
        Workflow.AddEvent(BatchSettlementService.Event(batch, actor, "QC_RESOLUTION", from, input.Note));
        await NotifyDepotAsync(batch.DepotId, "Nhà máy cập nhật hướng xử lý KCS", $"Lô {batch.Code}: {input.Note.Trim()}. {(status == "NEGOTIATING" ? "Nhà máy có thể đề xuất giá mua theo hiện trạng; kho cần đồng ý trước khi chuyển tiền." : "Chờ bàn giao trả và kho xác nhận nhận đủ; tồn chưa được hoàn.")}", id, ct: ct);
        await Work.SaveAsync(ct); await tx.CommitAsync(ct);
        return await SavedAsync(id, factory.Id, "Đã lưu hướng xử lý, giữ nguyên kết quả KCS không đạt.", ct);
    }

    public async Task<ServiceResult<OrderResponse>> HandoverAsync(Guid actor, Guid id, ReturnHandoverRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reference) || input.Reference.Trim().Length > 200 || string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length > 2000 ||
            string.IsNullOrWhiteSpace(input.ProofUrl) || !attachments.IsOwnedUrl(input.ProofUrl.Trim(), actor))
            return ServiceResult<OrderResponse>.Invalid("Cần tham chiếu, ghi chú bàn giao và chứng từ do chính nhà máy tải lên.");
        var factory = await CurrentFactory(actor, ct);
        await using var tx = await Work.BeginAsync(ct);
        var batch = await LockOrderAsync(id, factory.Id, ct);
        if (batch == null) return ServiceResult<OrderResponse>.NotFound("Không tìm thấy lô của nhà máy.");
        if (HasMoney(batch) || await HasFeeAsync(id, ct)) return ServiceResult<OrderResponse>.Conflict("Lô đã có xác nhận tài chính; cần đối soát riêng trước khi hoàn hàng.");
        if (batch.Status is "RETURNING" or "RETURNED")
        {
            if (batch.ReturnHandoverReference != input.Reference.Trim() || batch.ReturnHandoverProofUrl != input.ProofUrl.Trim() || batch.ReturnHandoverNote != input.Note.Trim())
                return ServiceResult<OrderResponse>.Conflict("Đã ghi bàn giao trả với dữ liệu khác; không sửa ngầm chứng từ.");
            return await SavedAsync(id, factory.Id, "Trả lại xác nhận bàn giao đã lưu.", ct);
        }
        if (batch.SettlementWorkflowVersion < 1 || batch.Status != "RETURN_REQUESTED" || batch.QualityCheck?.IsAccepted != false)
            return ServiceResult<OrderResponse>.Conflict("Cần chọn hướng trả hàng sau KCS không đạt trước khi bàn giao trả.");
        var from = batch.Status;
        batch.ReturnHandedOverAt = DateTime.UtcNow; batch.ReturnHandoverProofUrl = input.ProofUrl.Trim();
        batch.ReturnHandoverReference = input.Reference.Trim(); batch.ReturnHandoverNote = input.Note.Trim();
        batch.Status = "RETURNING"; batch.UpdatedAt = DateTime.UtcNow;
        Workflow.AddEvent(BatchSettlementService.Event(batch, actor, "RETURN_HANDED_OVER", from, input.Note));
        await NotifyDepotAsync(batch.DepotId, "Nhà máy đã bàn giao trả lô", $"Lô {batch.Code}: {input.Reference.Trim()}. Vui lòng kiểm đếm, cân thực nhận và chỉ xác nhận khi đủ toàn bộ lô về kho.", id, ct: ct);
        await Work.SaveAsync(ct); await tx.CommitAsync(ct);
        return await SavedAsync(id, factory.Id, "Đã ghi bàn giao trả; chờ kho xác nhận nhận đủ. Tồn chưa được hoàn.", ct);
    }
}
