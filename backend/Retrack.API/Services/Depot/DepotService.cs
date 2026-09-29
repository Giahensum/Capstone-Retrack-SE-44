using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotService(Retrack.API.Repositories.Interfaces.IDepotOwnerRepository owners, Retrack.API.Repositories.Interfaces.IDepotPaymentReadRepository payments) : IDepotService
{
    public async Task<List<DepotSummaryDto>> GetDepotsAsync(Guid ownerId)
    {
        await RequireActiveOwnerAsync(ownerId);
        return await owners.ListAsync(ownerId);
    }

    private async Task RequireActiveOwnerAsync(Guid ownerId)
    {
        if (!await owners.IsActiveOwnerAsync(ownerId))
            throw new DepotForbiddenException();
    }

    public async Task RequireOwnerAsync(Guid ownerId, Guid depotId)
    {
        await RequireActiveOwnerAsync(ownerId);
        if (!await owners.OwnsAsync(ownerId, depotId))
            throw new DepotForbiddenException();
    }

    public async Task<PagedResult<DepotPaymentDto>> GetPaymentsAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await RequireOwnerAsync(ownerId, depotId);
        if (!string.IsNullOrEmpty(query.Status) && query.Status is not ("AWAITING_PAYMENT" or "PAYMENT_SENT" or "DONE"))
            throw new ArgumentException("Trạng thái thanh toán không hợp lệ.");
        return await payments.ListAsync(depotId, query);
    }
    public async Task<DepotPaymentDto> GetPaymentAsync(Guid ownerId, Guid depotId, Guid requestId)
    {
        await RequireOwnerAsync(ownerId, depotId);
        return await payments.FindAsync(depotId, requestId) ?? throw new KeyNotFoundException("Không tìm thấy thanh toán trong kho.");
    }
    public async Task<PaymentSummaryDto> GetPaymentSummaryAsync(Guid ownerId, Guid depotId)
    {
        await RequireOwnerAsync(ownerId, depotId);
        // Ngày nghiệp vụ Việt Nam, đổi sang UTC để so sánh timestamptz.
        return await payments.SummaryAsync(depotId, DateTime.UtcNow.AddHours(7).Date.AddHours(-7));
    }
}
