using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Factory;
using Retrack.API.Services.Shared;

namespace Retrack.API.Repositories;

public sealed class FactoryPlatformFeeRepository(AppDbContext db) : IFactoryPlatformFeeRepository
{
    public async Task<PlatformPaymentAccountDto> PaymentAccountAsync(CancellationToken ct)
    {
        var keys = new[] { "PLATFORM_FEE_BANK_NAME", "PLATFORM_FEE_ACCOUNT_NUMBER", "PLATFORM_FEE_ACCOUNT_HOLDER" };
        var settings = await db.SystemConfigs.AsNoTracking().Where(c => keys.Contains(c.ConfigKey))
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, ct);
        return new(settings.GetValueOrDefault(keys[0], ""), settings.GetValueOrDefault(keys[1], ""), settings.GetValueOrDefault(keys[2], ""));
    }

    public async Task<PagedResult<FactoryFeeEntry>> FeesAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var rows = db.PlatformTransactions.AsNoTracking().Where(t => t.FeeAmount > 0 && t.SourceType == "BATCH_ORDER" && t.PayerId == userId
            && t.FeePolicy == PlatformFees.BuyerSeparate);
        var count = await rows.CountAsync(ct);
        var items = await (from t in rows
                           join b in db.InventoryBatches.AsNoTracking() on t.SourceId equals b.Id
                           orderby t.CreatedAt descending, t.Id descending
                           select new FactoryFeeEntry(t.Id, b.Code, t.FeeAmount, t.CreatedAt))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var rows = db.PlatformInvoices.AsNoTracking().Where(i => i.PayerId == userId);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth).ThenByDescending(i => i.Revision).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new FeeInvoiceDto(i.Id, new DateOnly(i.PeriodYear, i.PeriodMonth, 1), i.TotalFeeAmount,
                i.Status == "PENDING" ? "UNPAID" : i.Status, i.PaymentProofUrl, i.SubmittedAt, i.Revision, i.AdjustmentOfId, i.PayosOrderCode)).ToListAsync(ct);
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize };
    }

    public async Task<PlatformInvoice> LockInvoiceAsync(Guid id, CancellationToken ct) =>
        await db.PlatformInvoices.FromSqlInterpolated($"SELECT * FROM platform_invoices WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
}
