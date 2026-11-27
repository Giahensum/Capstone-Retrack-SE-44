using System.Text.Json;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Services.Depot;

/// <summary>
/// Hoàn tiền gói quản lý Depot. Tách ba mốc: yêu cầu → Admin quyết định (duyệt/từ chối) → Admin ghi nhận đã chuyển tiền hoàn thật.
/// Đổi trạng thái không đồng nghĩa tiền đã về tài khoản khách. Chỉ áp dụng cho tiền gói; tiền hàng do hai bên tự xử lý.
/// </summary>
public sealed partial class DepotSubscriptionService
{
    public static readonly string[] RefundKinds = ["DUPLICATE_OR_EXCESS", "SHORT_PAYMENT", "UNUSED_PERIOD", "SERVICE_ERROR", "LATE_PAYMENT"];
    private const decimal MaxRefund = 10_000_000m;

    public async Task<SubscriptionRefundDto> RequestRefundAsync(Guid userId, Guid? depotId, SubscriptionRefundRequestDto dto)
    {
        var isAdmin = !depotId.HasValue;
        if (isAdmin) await AdminAsync(userId); else await OwnerAsync(userId, depotId!.Value);
        if (dto == null || dto.OperationId == Guid.Empty) throw new ArgumentException("Cần mã thao tác yêu cầu hoàn tiền.");
        if (!RefundKinds.Contains(dto.Kind)) throw new ArgumentException("Loại hoàn tiền không hợp lệ.");
        CheckRefundAmount(dto.Amount);
        var reason = dto.Reason?.Trim() ?? "";
        if (reason.Length is < 10 or > 1000) throw new ArgumentException("Cần mô tả lý do hoàn tiền từ 10 đến 1.000 ký tự.");
        var evidenceUrl = string.IsNullOrWhiteSpace(dto.EvidenceUrl) ? null : dto.EvidenceUrl.Trim();
        if (evidenceUrl != null && (!Uri.TryCreate(evidenceUrl, UriKind.Absolute, out var u) || u.Scheme != "https" || evidenceUrl.Length > 2048))
            throw new ArgumentException("Chứng từ phải là đường dẫn HTTPS.");
        var evidenceRef = string.IsNullOrWhiteSpace(dto.EvidenceReference) ? null : dto.EvidenceReference.Trim();
        if (evidenceRef?.Length > 255) throw new ArgumentException("Mã tham chiếu chứng từ tối đa 255 ký tự.");
        if (dto.Kind is "DUPLICATE_OR_EXCESS" or "SHORT_PAYMENT" or "LATE_PAYMENT" && evidenceUrl == null && evidenceRef == null)
            throw new ArgumentException("Hoàn tiền chuyển trùng/thừa/thiếu hoặc tiền về muộn cần chứng từ hoặc mã giao dịch ngân hàng để đối soát.");

        var found = await repository.OrderAsync(dto.OrderId) ?? throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        if (!isAdmin && found.DepotId != depotId) throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        await using var tx = await repository.BeginAsync();
        await repository.LockDepotAsync(found.DepotId);
        var existing = await repository.RefundAsync(dto.OperationId);
        if (existing != null)
        {
            if (existing.OrderId != dto.OrderId || existing.RequestedBy != userId || existing.Kind != dto.Kind || existing.RequestedAmount != dto.Amount)
                throw new DepotConflictException("Mã thao tác đã dùng cho yêu cầu hoàn tiền khác.");
            await tx.CommitAsync();
            return await RefundDtoAsync(existing.Id, existing.DepotId);
        }
        var order = (await repository.OrderAsync(dto.OrderId))!;
        var refunds = await repository.OrderRefundsAsync(order.Id);
        if (refunds.Any(x => x.Status == "REQUESTED")) throw new DepotConflictException("Đơn gói đang có yêu cầu hoàn tiền chờ Admin xử lý.");
        CheckRefundEligibility(order, dto.Kind, dto.Amount, refunds, await repository.FindAsync(order.DepotId));
        var refund = new DepotSubscriptionRefund
        {
            Id = dto.OperationId, OrderId = order.Id, DepotId = order.DepotId, Kind = dto.Kind, RequestedBy = userId,
            RequestedRole = isAdmin ? "ADMIN" : "OWNER", RequestedAt = Now, RequestedAmount = dto.Amount, Reason = reason,
            EvidenceUrl = evidenceUrl, EvidenceReference = evidenceRef,
        };
        repository.Add(refund);
        repository.Audit(userId, "SUBSCRIPTION_REFUND_REQUESTED", refund.Id,
            JsonSerializer.Serialize(new { order.Id, order.OrderCode, refund.Kind, refund.RequestedAmount, refund.Reason, refund.EvidenceUrl, refund.EvidenceReference }));
        await repository.SaveAsync(); await tx.CommitAsync();
        return await RefundDtoAsync(refund.Id, refund.DepotId);
    }

    public async Task<PagedResult<SubscriptionRefundDto>> RefundsAsync(Guid userId, Guid? depotId, int page, int pageSize, string? status = null)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue || status is not (null or "" or "REQUESTED" or "APPROVED" or "REJECTED" or "REFUNDED"))
            throw new ArgumentException("Phân trang hoặc trạng thái hoàn tiền không hợp lệ.");
        if (depotId.HasValue) await OwnerAsync(userId, depotId.Value); else await AdminAsync(userId);
        return await repository.RefundsAsync(depotId, page, pageSize, status);
    }

    public async Task<SubscriptionRefundDto> DecideRefundAsync(Guid adminId, Guid refundId, SubscriptionRefundDecisionDto dto)
    {
        await AdminAsync(adminId);
        var note = dto?.Note?.Trim() ?? "";
        if (note.Length is < 5 or > 1000) throw new ArgumentException("Cần ghi chú quyết định từ 5 đến 1.000 ký tự.");
        var found = await repository.RefundAsync(refundId) ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hoàn tiền.");
        await using var tx = await repository.BeginAsync();
        var depot = await repository.LockDepotAsync(found.DepotId);
        var refund = (await repository.RefundAsync(refundId))!;
        var target = dto!.Approve ? "APPROVED" : "REJECTED";
        if (refund.Status != "REQUESTED")
        {
            // Gửi lại đúng quyết định đã ghi: trả kết quả cũ, không đổi gì.
            if (refund.Status == target || refund.Status == "REFUNDED" && target == "APPROVED") { await tx.CommitAsync(); return await RefundDtoAsync(refund.Id, refund.DepotId); }
            throw new DepotConflictException("Yêu cầu hoàn tiền đã được xử lý.");
        }
        var order = (await repository.OrderAsync(refund.OrderId))!;
        var now = Now;
        refund.DecidedBy = adminId; refund.DecidedAt = now; refund.DecisionNote = note;
        if (!dto.Approve)
        {
            refund.Status = "REJECTED";
            repository.Notify(refund.DepotId, depot.OwnerId, "Yêu cầu hoàn tiền gói bị từ chối", $"Đơn gói #{order.OrderCode}: {note}");
        }
        else
        {
            var amount = dto.Amount ?? refund.RequestedAmount;
            CheckRefundAmount(amount);
            if (amount > refund.RequestedAmount) throw new ArgumentException("Số tiền duyệt không được vượt số tiền yêu cầu.");
            var others = (await repository.OrderRefundsAsync(order.Id)).Where(x => x.Id != refund.Id).ToList();
            var subscription = await repository.FindAsync(order.DepotId);
            CheckRefundEligibility(order, refund.Kind, amount, others, subscription);
            refund.Status = "APPROVED"; refund.ApprovedAmount = amount;
            if (refund.Kind == "UNUSED_PERIOD")
            {
                // Rút đúng kỳ chưa bắt đầu: hạn trả phí lùi về hạn của các kỳ còn lại, kỳ khác giữ nguyên.
                order.PeriodRevokedAt = now; refund.PeriodRevoked = true;
                subscription!.PaidUntil = await repository.PreviousPaidEndAsync(order.DepotId, order.Id);
                subscription.UpdatedAt = now;
            }
            else if (refund.Kind == "SHORT_PAYMENT" && order.Status == "PENDING")
            {
                // Hoàn khoản chuyển thiếu thì đóng đơn để kho tạo đơn mới đúng giá; không kích hoạt gói từ khoản thiếu.
                if (order.ProviderStartedAt != null) throw new DepotConflictException("Đơn đã dùng PayOS; đồng bộ trạng thái PayOS trước khi hoàn khoản thiếu.");
                order.Status = "CANCELLED"; order.ReviewNote = "Đóng đơn do hoàn khoản chuyển thiếu.";
            }
            repository.Notify(refund.DepotId, depot.OwnerId, "Yêu cầu hoàn tiền gói đã được duyệt",
                $"Đơn gói #{order.OrderCode}: duyệt hoàn {amount:N0} đ. RETRACK sẽ chuyển tiền và cập nhật khi đã chuyển thật." +
                (refund.PeriodRevoked ? " Kỳ chưa bắt đầu của đơn này đã được rút khỏi hạn dùng." : ""));
        }
        repository.Audit(adminId, "SUBSCRIPTION_REFUND_" + refund.Status, refund.Id,
            JsonSerializer.Serialize(new { refund.OrderId, refund.Kind, refund.ApprovedAmount, Note = note, refund.PeriodRevoked }));
        await repository.SaveAsync(); await tx.CommitAsync();
        return await RefundDtoAsync(refund.Id, refund.DepotId);
    }

    public async Task<SubscriptionRefundDto> MarkRefundTransferredAsync(Guid adminId, Guid refundId, SubscriptionRefundTransferDto dto)
    {
        await AdminAsync(adminId);
        var reference = dto?.Reference?.Trim() ?? "";
        if (reference.Length is < 3 or > 255) throw new ArgumentException("Cần mã giao dịch chuyển tiền hoàn thật (3–255 ký tự).");
        var proof = string.IsNullOrWhiteSpace(dto!.ProofUrl) ? null : dto.ProofUrl.Trim();
        if (proof != null && (!Uri.TryCreate(proof, UriKind.Absolute, out var u) || u.Scheme != "https" || proof.Length > 2048))
            throw new ArgumentException("Chứng từ chuyển tiền phải là đường dẫn HTTPS.");
        var found = await repository.RefundAsync(refundId) ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hoàn tiền.");
        await using var tx = await repository.BeginAsync();
        var depot = await repository.LockDepotAsync(found.DepotId);
        var refund = (await repository.RefundAsync(refundId))!;
        if (refund.Status == "REFUNDED")
        {
            if (refund.RefundReference == reference) { await tx.CommitAsync(); return await RefundDtoAsync(refund.Id, refund.DepotId); }
            throw new DepotConflictException("Đã ghi nhận chuyển tiền hoàn với mã khác; không sửa ngầm.");
        }
        if (refund.Status != "APPROVED") throw new DepotConflictException("Chỉ ghi nhận chuyển tiền cho yêu cầu đã được duyệt.");
        refund.Status = "REFUNDED"; refund.RefundedBy = adminId; refund.RefundedAt = Now; refund.RefundReference = reference; refund.RefundProofUrl = proof;
        var order = (await repository.OrderAsync(refund.OrderId))!;
        repository.Notify(refund.DepotId, depot.OwnerId, "Đã chuyển tiền hoàn gói",
            $"Đơn gói #{order.OrderCode}: đã chuyển hoàn {refund.ApprovedAmount:N0} đ, mã giao dịch {reference}. Kiểm tra tài khoản và liên hệ Admin nếu chưa nhận được.");
        repository.Audit(adminId, "SUBSCRIPTION_REFUND_TRANSFERRED", refund.Id, JsonSerializer.Serialize(new { refund.OrderId, refund.ApprovedAmount, reference, proof }));
        await repository.SaveAsync(); await tx.CommitAsync();
        return await RefundDtoAsync(refund.Id, refund.DepotId);
    }

    private static void CheckRefundAmount(decimal amount)
    {
        if (amount <= 0 || amount > MaxRefund || decimal.Truncate(amount) != amount) throw new ArgumentException("Số tiền hoàn phải là số nguyên đồng từ 1 đến 10.000.000đ.");
    }

    /// <summary>Điều kiện theo loại: kỳ đang dùng không hoàn; hoàn theo giá gói không vượt số đã thu của đơn.</summary>
    private void CheckRefundEligibility(DepotSubscriptionOrder order, string kind, decimal amount, List<DepotSubscriptionRefund> others, DepotSubscription? subscription)
    {
        var committed = others.Where(x => x.Status is "APPROVED" or "REFUNDED" && x.Kind is "UNUSED_PERIOD" or "SERVICE_ERROR").Sum(x => x.ApprovedAmount ?? 0);
        switch (kind)
        {
            case "UNUSED_PERIOD":
                if (order.Status != "PAID" || order.PeriodRevokedAt != null) throw new DepotConflictException("Chỉ hoàn kỳ của đơn đã thanh toán và chưa được hoàn.");
                if (order.PeriodStart <= Now) throw new DepotConflictException("Kỳ đang sử dụng được dùng đến hết hạn; không hoàn vì ngừng sử dụng.");
                if (subscription?.PaidUntil != order.PeriodEnd) throw new DepotConflictException("Chỉ hoàn kỳ gia hạn cuối cùng; hoàn các kỳ sau trước để không xóa nhầm kỳ khác.");
                if (amount != order.Amount || committed > 0) throw new ArgumentException("Hoàn kỳ chưa bắt đầu là hoàn đủ giá đơn và chưa có khoản hoàn nào khác theo giá gói.");
                break;
            case "SERVICE_ERROR":
                if (order.Status != "PAID") throw new DepotConflictException("Lỗi dịch vụ chỉ xét cho đơn đã thanh toán.");
                if (amount + committed > order.Amount) throw new ArgumentException($"Tổng hoàn theo giá gói không vượt {order.Amount:N0} đ đã thu của đơn.");
                break;
            case "DUPLICATE_OR_EXCESS":
                if (order.Status != "PAID") throw new DepotConflictException("Hoàn chuyển trùng/thừa áp dụng cho đơn đã ghi nhận thanh toán.");
                break;
            case "SHORT_PAYMENT":
                if (order.Status is not ("PENDING" or "CANCELLED")) throw new DepotConflictException("Hoàn khoản chuyển thiếu chỉ áp dụng cho đơn chưa kích hoạt.");
                if (amount >= order.Amount) throw new ArgumentException("Khoản chuyển thiếu phải nhỏ hơn giá đơn.");
                break;
            case "LATE_PAYMENT":
                if (order.Status != "CANCELLED") throw new DepotConflictException("Tiền về muộn chỉ áp dụng cho đơn đã hủy; đơn đang chờ thì Admin đối soát để kích hoạt.");
                break;
        }
    }

    private async Task<SubscriptionRefundDto> RefundDtoAsync(Guid refundId, Guid depotId) =>
        (await repository.RefundsAsync(depotId, 1, 1, null, refundId)).Items.Single();
}
