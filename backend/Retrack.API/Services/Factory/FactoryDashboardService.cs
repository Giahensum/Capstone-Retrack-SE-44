using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public class FactoryDashboardService(AppDbContext db, IFactoryReportRepository? reports = null) : FactoryServiceBase(db), IFactoryDashboardService
{
    private readonly IFactoryReportRepository reportReads = reports ?? new FactoryReportRepository(db, new FactoryOrderReadRepository(db));

    public async Task<ServiceResult<DashboardResponse>> GetAsync(Guid userId, CancellationToken ct, string period = "month")
    {
        if (period is not ("day" or "month" or "year"))
            return ServiceResult<DashboardResponse>.Invalid("Kỳ thống kê phải là ngày, tháng hoặc năm.");
        var factory = await CurrentFactory(userId, ct);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodStart = period switch
        {
            "day" => now.Date,
            "year" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => monthStart
        };
        return ServiceResult<DashboardResponse>.Success(data: await reportReads.DashboardAsync(factory.Id, periodStart, monthStart, ct));
    }
}
