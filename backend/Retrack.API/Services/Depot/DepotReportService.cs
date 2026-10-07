using RevenueReportDto = Retrack.API.DTOs.Depot.RevenueReportDto;
using RevenuePointDto = Retrack.API.DTOs.Depot.RevenuePointDto;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class DepotReportService(IDepotUnitOfWork work, IDepotReportRepository reports, IDepotStaffRepository staffRepository, IDepotService scope, IInventoryService inventory) : IDepotReportService
{
    public async Task<RevenueReportDto> RevenueAsync(Guid ownerId, Guid depotId, PeriodQuery period)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        var (revenue, costs) = await reports.DailyAmountsAsync(depotId, start, end);
        DateOnly Bucket(DateTime day) => period.GroupBy switch {
            "month" => new(day.Year, day.Month, 1),
            "week" => DateOnly.FromDateTime(day.AddDays(-((int)day.DayOfWeek + 6) % 7)),
            _ => DateOnly.FromDateTime(day)
        };
        var points = revenue.Select(r => new RevenuePointDto(Bucket(r.Date), r.Amount, 0))
            .Concat(costs.Select(r => new RevenuePointDto(Bucket(r.Date), 0, r.Amount)))
            .GroupBy(r => r.Date).OrderBy(g => g.Key).Select(g => new RevenuePointDto(g.Key, g.Sum(r => r.Revenue), g.Sum(r => r.PurchaseCost))).ToList();
        return new(revenue.Sum(r => r.Amount), costs.Sum(r => r.Amount), points);
    }
    public async Task<DashboardDto> DashboardAsync(Guid ownerId, Guid depotId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var day = DateTime.UtcNow.AddHours(7).Date.AddHours(-7);
        var report = await RevenueAsync(ownerId, depotId, new());
        var counts = await reports.DashboardCountsAsync(depotId, day);
        return new(counts.NewRequests, (await inventory.GetAsync(ownerId, depotId)).Sum(i => i.AvailableKg),
            counts.OpenBatches, counts.AwaitingPayment, report.Revenue, report.PurchaseCost, counts.RejectedQualityBatches);
    }
    public async Task<PagedResult<StaffPerformanceDto>> PerformanceAsync(Guid ownerId, Guid depotId, PeriodQuery period, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        return await reports.PerformanceAsync(depotId, start, end, query);
    }
    public async Task<PagedResult<StaffHistoryDto>> HistoryAsync(Guid ownerId, Guid depotId, Guid staffId, PeriodQuery period, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        var staff = await staffRepository.FindAsync(depotId, staffId) ?? throw new KeyNotFoundException("Không tìm thấy nhân viên trong kho.");
        return await reports.HistoryAsync(depotId, staff, start, end, query);
    }
    public async Task<PagedResult<FeeEntryDto>> FeesAsync(Guid ownerId, Guid depotId, PeriodQuery period, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        return await reports.FeesAsync(depotId, start, end, query);
    }
    public async Task<FeeSummaryDto> FeeSummaryAsync(Guid ownerId, Guid depotId, PeriodQuery period)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        var (start, end) = period.Bounds();
        return await reports.FeeSummaryAsync(ownerId, depotId, start, end);
    }
    public async Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid ownerId, Guid depotId, DepotQuery query)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        return await reports.InvoicesAsync(ownerId, query);
    }
    public async Task ConfirmInvoiceAsync(Guid ownerId, Guid depotId, Guid id, PaymentProofDto dto)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!Uri.TryCreate(dto.PaymentProofUrl, UriKind.Absolute, out var url) || (url.Scheme != "https" && url.Scheme != "http")) throw new ArgumentException("Chứng từ phải là URL HTTP/HTTPS.");
        await using var tx = await work.BeginAsync();
        var invoice = await reports.LockInvoiceAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.PayerId != ownerId) throw new DepotForbiddenException();
        if (invoice.Status != "PENDING")
        {
            if (invoice.PaymentProofUrl == dto.PaymentProofUrl) return;
            throw new DepotConflictException("Hóa đơn đã có chứng từ thanh toán.");
        }
        invoice.Status = "SUBMITTED"; invoice.PaymentProofUrl = dto.PaymentProofUrl; invoice.SubmittedAt = DateTime.UtcNow;
        await work.SaveAsync(); await tx.CommitAsync();
    }

    public Task SimulatePaymentAsync(Guid ownerId, Guid depotId, Guid id)
    {
        // Tên miền .invalid đánh dấu chứng từ giả; controller chỉ mở endpoint trong Development.
        var reference = $"SIM-PAYOS-{id:N}".ToUpperInvariant();
        var receiptUrl = $"https://payos-mock.invalid/receipts/{id:D}?reference={reference}";
        return ConfirmInvoiceAsync(ownerId, depotId, id, new PaymentProofDto { PaymentProofUrl = receiptUrl });
    }
}
