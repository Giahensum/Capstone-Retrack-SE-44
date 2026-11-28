using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Factory;

namespace Retrack.API.Repositories.Interfaces;

public interface IFactoryPlatformFeeRepository
{
    Task<PlatformPaymentAccountDto> PaymentAccountAsync(CancellationToken ct);
    Task<PagedResult<FactoryFeeEntry>> FeesAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid userId, int page, int pageSize, CancellationToken ct);
    Task<PlatformInvoice> LockInvoiceAsync(Guid id, CancellationToken ct);
}
