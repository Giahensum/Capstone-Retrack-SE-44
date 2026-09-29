using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Depot;

public sealed class DepotPaymentService(IDepotOwnerRepository owners, IDepotPaymentRepository payments, IDepotUnitOfWork work) : IDepotPaymentService
{
    public async Task<PickupRequest> MarkSentAsync(Guid requestId, Guid ownerId, string paymentProofUrl)
    {
        if (!await owners.IsActiveOwnerAsync(ownerId))
            throw new DepotForbiddenException();
        if (string.IsNullOrWhiteSpace(paymentProofUrl) ||
            !Uri.TryCreate(paymentProofUrl, UriKind.Absolute, out var proof) ||
            (proof.Scheme != Uri.UriSchemeHttps && proof.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Đường dẫn chứng từ phải là URL HTTP hoặc HTTPS hợp lệ.");

        await using var transaction = await work.BeginAsync();
        // Tuần tự hóa thanh toán giữa các tiến trình, không chỉ chặn nhấn nút lặp trên trình duyệt.
        var req = await payments.LockAsync(requestId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
        if (req.TargetDepotId is null || !await owners.OwnsAsync(ownerId, req.TargetDepotId.Value))
            throw new DepotForbiddenException();
        if (req.Status is "PAYMENT_SENT" or "DONE")
        {
            if (req.PaymentProofUrl != paymentProofUrl)
                throw new DepotConflictException("Đơn đã có chứng từ thanh toán khác.");
            await transaction.CommitAsync();
            return req;
        }
        if (req.Status != "AWAITING_PAYMENT")
            throw new DepotConflictException("Chỉ được thanh toán đơn đang chờ thanh toán.");
        req.PaymentProofUrl = paymentProofUrl;
        req.Status = "PAYMENT_SENT";
        req.UpdatedAt = DateTime.UtcNow;

        // Ghi nhận phí cùng giao dịch với trạng thái và chứng từ.
        payments.AddFee(new PlatformTransaction
        {
            SourceType = "PICKUP_REQUEST",
            SourceId = requestId,
            FeeAmount = req.PlatformFeeAmount,
            Description = $"Phí thu gom phế liệu #{requestId}"
        });
        await work.SaveAsync();
        await transaction.CommitAsync();

        return req;
    }

}
