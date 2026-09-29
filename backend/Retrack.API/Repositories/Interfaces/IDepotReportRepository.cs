using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;

namespace Retrack.API.Repositories.Interfaces;

public record DepotDailyAmount(DateTime Date, decimal Amount);
public record DepotDashboardCounts(int NewRequests, int OpenBatches, int AwaitingPayment);

public interface IDepotReportRepository
{
    Task<(List<DepotDailyAmount> Revenue, List<DepotDailyAmount> Costs)> DailyAmountsAsync(Guid depotId, DateTime start, DateTime end);
    Task<DepotDashboardCounts> DashboardCountsAsync(Guid depotId, DateTime day);
    Task<PagedResult<StaffPerformanceDto>> PerformanceAsync(Guid depotId, DateTime start, DateTime end, DepotQuery query);
    Task<PagedResult<StaffHistoryDto>> HistoryAsync(Guid depotId, DepotStaff staff, DateTime start, DateTime end, DepotQuery query);
    Task<PagedResult<FeeEntryDto>> FeesAsync(Guid depotId, DateTime start, DateTime end, DepotQuery query);
    Task<FeeSummaryDto> FeeSummaryAsync(Guid ownerId, Guid depotId, DateTime start, DateTime end);
    Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid ownerId, DepotQuery query);
    Task<PlatformInvoice?> LockInvoiceAsync(Guid id);
}
