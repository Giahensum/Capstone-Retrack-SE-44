using Retrack.API.DTOs.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Interfaces;

public interface IFactoryOrderService
{
    Task<ServiceResult<PageResponse<OrderResponse>>> ListAsync(Guid userId, OrderQuery query, CancellationToken ct);
    Task<ServiceResult<OrderResponse>> GetAsync(Guid userId, Guid id, CancellationToken ct);
    Task<ServiceResult<OrderReceivedResponse>> ReceiveAsync(Guid userId, Guid id, CancellationToken ct);
    Task<ServiceResult<OrderSettlementResponse>> SettleAsync(Guid userId, Guid id, SettlementRequest request, CancellationToken ct);
    Task<ServiceResult<InvoiceUpdatedResponse>> InvoiceAsync(Guid userId, Guid id, InvoiceRequest request, CancellationToken ct);
}
