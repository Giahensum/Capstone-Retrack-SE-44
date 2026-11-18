using Retrack.API.DTOs.Depot;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class BatchReturnService(IDepotBatchRepository repository, IDepotUnitOfWork work, IDepotService scope)
{
    public async Task ReceiveAsync(Guid owner, Guid depot, Guid id, BatchReturnReceiptDto input)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (!input.Confirmed || input.ReceivedKg <= 0 || string.IsNullOrWhiteSpace(input.Reference) || input.Reference.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length > 2000)
            throw new ArgumentException("Cần cân, kiểm đủ lô thực nhận, tham chiếu và ghi chú đối soát.");
        await using var tx = await work.BeginAsync(); await repository.LockDepotAsync(depot);
        var batch = await repository.LockBatchAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy lô.");
        if (batch.DepotId != depot) throw new DepotForbiddenException();
        if (input.ReceivedKg != batch.DeclaredWeightKg)
            throw new DepotConflictException("Lượng nhận chưa khớp toàn bộ lượng đã xuất. Chưa hoàn tồn; cần đối soát sai lệch riêng, không nhập số giả để xác nhận.");
        if (batch.SettledAt != null || batch.SettlementApprovedAt != null || batch.DepotPaymentConfirmedAt != null ||
            !string.IsNullOrWhiteSpace(batch.PaymentReference) || !string.IsNullOrWhiteSpace(batch.QualityCheck?.PaymentProofUrl) || await repository.FindFeeAsync(id) != null)
            throw new DepotConflictException("Lô đã có xác nhận tiền; không hoàn tồn qua luồng trả hàng chưa thanh toán.");
        if (batch.Status == "RETURNED")
        {
            if (batch.ReturnReceiptReference != input.Reference.Trim() || batch.ReturnReceiptNote != input.Note.Trim())
                throw new DepotConflictException("Lô đã nhận trả với dữ liệu khác; không sửa lịch sử.");
            return;
        }
        if (batch.SettlementWorkflowVersion < 1 || batch.Status != "RETURNING" || batch.ReturnHandedOverAt == null || string.IsNullOrWhiteSpace(batch.ReturnHandoverProofUrl))
            throw new DepotConflictException("Cần nhà máy xác nhận bàn giao trả kèm chứng từ trước khi kho nhận đủ lô.");
        var from = batch.Status;
        batch.ReturnReceivedAt = DateTime.UtcNow; batch.ReturnReceivedBy = owner;
        batch.ReturnReceiptReference = input.Reference.Trim(); batch.ReturnReceiptNote = input.Note.Trim();
        batch.Status = "RETURNED"; batch.UpdatedAt = DateTime.UtcNow;
        repository.AddWorkflowEvent(BatchSettlementService.Event(batch, owner, "RETURN_RECEIVED", from, input.Note));
        await repository.QueueFactoryBatchNoticeAsync(batch.TargetFactoryId!.Value, batch, "Kho đã nhận đủ lô trả về", $"Lô {batch.Code}: {input.Reference.Trim()}. Đã khép việc trả hàng và hoàn nguồn tồn của kho; đây không phải giao dịch đã thanh toán.");
        await work.SaveAsync(); await tx.CommitAsync();
    }
}
