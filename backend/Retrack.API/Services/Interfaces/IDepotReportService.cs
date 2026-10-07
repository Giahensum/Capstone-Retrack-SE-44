using Retrack.API.DTOs;
using RevenueReportDto = Retrack.API.DTOs.Depot.RevenueReportDto;
using Retrack.API.DTOs.Depot;
namespace Retrack.API.Services.Interfaces;
public interface IDepotReportService
{
    Task<DashboardDto> DashboardAsync(Guid ownerId, Guid depotId);
    Task<RevenueReportDto> RevenueAsync(Guid ownerId, Guid depotId, PeriodQuery period);
    Task<PagedResult<StaffPerformanceDto>> PerformanceAsync(Guid ownerId, Guid depotId, PeriodQuery period, DepotQuery query);
    Task<PagedResult<StaffHistoryDto>> HistoryAsync(Guid ownerId, Guid depotId, Guid staffId, PeriodQuery period, DepotQuery query);
    Task<PagedResult<FeeEntryDto>> FeesAsync(Guid ownerId, Guid depotId, PeriodQuery period, DepotQuery query);
    Task<FeeSummaryDto> FeeSummaryAsync(Guid ownerId, Guid depotId, PeriodQuery period);
    Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid ownerId, Guid depotId, DepotQuery query);
    Task ConfirmInvoiceAsync(Guid ownerId, Guid depotId, Guid id, PaymentProofDto dto);
    Task SimulatePaymentAsync(Guid ownerId, Guid depotId, Guid id);
}
