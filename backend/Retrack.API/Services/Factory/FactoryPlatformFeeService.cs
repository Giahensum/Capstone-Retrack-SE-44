using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public sealed record FactoryFeeEntry(Guid Id, string? BatchCode, decimal FeeAmount, DateTime Date);

public sealed class FactoryPlatformFeeService(AppDbContext db, FactoryAttachmentService attachments, Retrack.API.Services.Interfaces.IDepotPayOsService payos) : FactoryServiceBase(db)
{
    private async Task RequireFactory(Guid userId, CancellationToken ct) => _ = await CurrentFactory(userId, ct);

    public async Task<PlatformPaymentAccountDto> PaymentAccountAsync(Guid userId, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        var keys = new[] { "PLATFORM_FEE_BANK_NAME", "PLATFORM_FEE_ACCOUNT_NUMBER", "PLATFORM_FEE_ACCOUNT_HOLDER" };
        var settings = await Db.SystemConfigs.AsNoTracking().Where(c => keys.Contains(c.ConfigKey))
            .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue, ct);
        return new(settings.GetValueOrDefault(keys[0], ""), settings.GetValueOrDefault(keys[1], ""), settings.GetValueOrDefault(keys[2], ""));
    }

    public async Task<PagedResult<FactoryFeeEntry>> FeesAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        var rows = Db.PlatformTransactions.AsNoTracking().Where(t => t.SourceType == "BATCH_ORDER" && t.PayerId == userId
            && t.FeePolicy == PlatformFees.BuyerSeparate);
        var count = await rows.CountAsync(ct);
        var items = await (from t in rows
                           join b in Db.InventoryBatches.AsNoTracking() on t.SourceId equals b.Id
                           orderby t.CreatedAt descending, t.Id descending
                           select new FactoryFeeEntry(t.Id, b.Code, t.FeeAmount, t.CreatedAt))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        var rows = Db.PlatformInvoices.AsNoTracking().Where(i => i.PayerId == userId);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth).ThenByDescending(i => i.Revision).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new FeeInvoiceDto(i.Id, new DateOnly(i.PeriodYear, i.PeriodMonth, 1), i.TotalFeeAmount,
                i.Status == "PENDING" ? "UNPAID" : i.Status, i.PaymentProofUrl, i.SubmittedAt, i.Revision, i.AdjustmentOfId, i.PayosOrderCode)).ToListAsync(ct);
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize };
    }

    public async Task ConfirmAsync(Guid userId, Guid id, FactoryPaymentProofDto proof, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        if (string.IsNullOrWhiteSpace(proof.PaymentProofUrl) || !attachments.IsOwnedUrl(proof.PaymentProofUrl, userId))
            throw new ArgumentException("Chứng từ phải là tệp do tài khoản Factory này tải lên.");
        if (!(await PaymentAccountAsync(userId, ct)).IsConfigured)
            throw new DepotConflictException("Admin chưa cấu hình tài khoản nhận phí nền tảng.");
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var invoice = await LockInvoice(id, ct);
        if (invoice.PayerId != userId) throw new DepotForbiddenException();
        if (invoice.Status != "PENDING" || DepotPayOsService.OrderCodeOf(invoice) != null)
            throw new DepotConflictException("Hóa đơn không còn chờ chứng từ chuyển khoản.");
        invoice.PaymentProofUrl = proof.PaymentProofUrl.Trim();
        invoice.SubmittedAt = DateTime.UtcNow;
        invoice.Status = "SUBMITTED";
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Tạo link thanh toán PayOS cho hóa đơn phí của Factory. Hóa đơn chỉ thành PAID khi PayOS xác nhận (webhook ký hoặc truy vấn trực tiếp).</summary>
    public async Task<PayOsLinkDto> PayOsLinkAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        return await payos.CreateLinkForPayerAsync(userId, id, PayOsRedirect.Factory);
    }

    public async Task<string> PayOsSyncAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        return await payos.SyncForPayerAsync(userId, id);
    }

    private async Task<Retrack.API.Models.PlatformInvoice> LockInvoice(Guid id, CancellationToken ct) =>
        await Db.PlatformInvoices.FromSqlInterpolated($"SELECT * FROM platform_invoices WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
}

public sealed class FactoryPaymentProofDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PaymentProofUrl { get; set; } = "";
}
