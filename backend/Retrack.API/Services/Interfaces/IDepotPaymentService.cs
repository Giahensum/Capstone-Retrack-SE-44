using Retrack.API.Models;

namespace Retrack.API.Services.Interfaces;

public interface IDepotPaymentService
{
    // PickupService dùng entity đã nạp để giữ DTO/API cũ cho các consumer hiện tại.
    Task<PickupRequest> MarkSentAsync(Guid requestId, Guid ownerId, string paymentProofUrl);
}
