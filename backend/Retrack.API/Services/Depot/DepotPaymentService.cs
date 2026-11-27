using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Depot;

public sealed class DepotPaymentService(IDepotOwnerRepository owners, IDepotPaymentRepository payments, IDepotUnitOfWork work,
    Retrack.API.Services.Money.PlatformMoneyHooks? money = null) : IDepotPaymentService
{
    public async Task<PickupRequest> MarkSentAsync(Guid requestId, Guid ownerId, string? paymentProofUrl, string paymentMethod = "BANK_TRANSFER", string? reference = null)
    {
        if (!await owners.IsActiveOwnerAsync(ownerId))
            throw new DepotForbiddenException();
        if (paymentMethod is not ("BANK_TRANSFER" or "CASH" or "OTHER") || reference?.Length > 255 ||
            paymentMethod != "BANK_TRANSFER" && string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Cần hình thức thanh toán và căn cứ giao tiền thực tế.");
        paymentProofUrl = string.IsNullOrWhiteSpace(paymentProofUrl) ? null : paymentProofUrl.Trim();
        if (paymentMethod == "BANK_TRANSFER" && paymentProofUrl == null || paymentProofUrl != null &&
            (!Uri.TryCreate(paymentProofUrl, UriKind.Absolute, out var proof) || (proof.Scheme != Uri.UriSchemeHttps && proof.Scheme != Uri.UriSchemeHttp)))
            throw new ArgumentException("Đường dẫn chứng từ phải là URL HTTP hoặc HTTPS hợp lệ.");

        await using var transaction = await work.BeginAsync();
        // Tuần tự hóa thanh toán giữa các tiến trình, không chỉ chặn nhấn nút lặp trên trình duyệt.
        var req = await payments.LockAsync(requestId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
        if (req.TargetDepotId is null || !await owners.OwnsAsync(ownerId, req.TargetDepotId.Value))
            throw new DepotForbiddenException();
        // Kiểm quyền trước khi trả thông tin về khoản tiền đã thu qua sàn của đơn cũ.
        if (money != null && await money.IsPlatformAsync("PICKUP", requestId))
            throw new DepotConflictException("Đơn cũ đã có khoản thu qua sàn: hãy đối soát PayOS thay vì báo trả tiền trực tiếp.");
        if (req.Status is "PAYMENT_SENT" or "DONE")
        {
            if (req.PaymentProofUrl != paymentProofUrl || (req.PaymentMethod ?? "BANK_TRANSFER") != paymentMethod || req.PaymentReference != reference?.Trim())
                throw new DepotConflictException("Đơn đã có chứng từ thanh toán khác.");
            await transaction.CommitAsync();
            return req;
        }
        if (req.Status != "AWAITING_PAYMENT")
            throw new DepotConflictException("Chỉ được thanh toán đơn đang chờ thanh toán.");
        req.PaymentProofUrl = paymentProofUrl;
        req.PaymentMethod = paymentMethod; req.PaymentReference = reference?.Trim();
        req.Status = "PAYMENT_SENT";
        req.UpdatedAt = DateTime.UtcNow;
        // Thông báo lưu cùng trạng thái thanh toán; retry không tạo thêm bản ghi.
        payments.AddNotice(Notification.ForPickup(req.SellerId, req.Id, "Kho báo đã thanh toán",
            $"Kho báo đã trả tiền cho đơn #{req.Id.ToString()[..8].ToUpper()}. Kiểm tra tiền thực nhận trước khi xác nhận.", "PAYMENT"));

        // Ghi nhận phí cùng giao dịch với trạng thái và chứng từ.
        payments.AddFee(new PlatformTransaction
        {
            SourceType = "PICKUP_REQUEST",
            SourceId = requestId,
            FeeAmount = req.PlatformFeeAmount,
            FeePolicy = req.FeePolicy,
            PayerId = ownerId,
            TransactionAmount = req.GrossAmount,
            FeePercentage = req.PlatformFeePercentage,
            Description = $"Phí thu gom phế liệu #{requestId}"
        });
        await work.SaveAsync();
        await transaction.CommitAsync();

        return req;
    }

}
