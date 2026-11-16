using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Shared;

namespace Retrack.API.Services.Factory;

public sealed record FactoryFeeEntry(Guid Id, string? BatchCode, decimal FeeAmount, DateTime Date);

public sealed class FactoryPlatformFeeService(AppDbContext db, IPayOsClient payos, IConfiguration configuration) : FactoryServiceBase(db)
{
    private async Task RequireFactory(Guid userId, CancellationToken ct) => _ = await CurrentFactory(userId, ct);

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
        var items = await rows.OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new FeeInvoiceDto(i.Id, new DateOnly(i.PeriodYear, i.PeriodMonth, 1), i.TotalFeeAmount,
                i.Status == "PENDING" ? "UNPAID" : i.Status, i.PaymentProofUrl, i.SubmittedAt)).ToListAsync(ct);
        return new() { Items = items, TotalCount = count, Page = page, PageSize = pageSize };
    }

    public async Task ConfirmAsync(Guid userId, Guid id, PaymentProofDto proof, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        if (!Uri.TryCreate(proof.PaymentProofUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            throw new ArgumentException("Chứng từ phải là đường dẫn HTTP/HTTPS hợp lệ.");
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

    public async Task<PayOsLinkDto> CreatePayOsLinkAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        if (!payos.IsConfigured) throw new DepotConflictException("PayOS chưa được cấu hình. Hãy liên hệ Admin để nhận thông tin chuyển khoản và gửi chứng từ.");
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var invoice = await LockInvoice(id, ct);
        if (invoice.PayerId != userId) throw new DepotForbiddenException();
        if (invoice.Status != "PENDING") throw new DepotConflictException("Hóa đơn không còn chờ thanh toán.");
        if (invoice.TotalFeeAmount <= 0 || invoice.TotalFeeAmount != decimal.Truncate(invoice.TotalFeeAmount))
            throw new DepotConflictException("Số tiền hóa đơn chưa hợp lệ để thanh toán PayOS.");
        if (DepotPayOsService.OrderCodeOf(invoice) is long existingCode)
        {
            var current = await payos.GetPaymentAsync(existingCode, ct);
            if (current.Status == "PAID")
            {
                MarkPaid(invoice, current); await Db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                throw new DepotConflictException("Hóa đơn đã được thanh toán.");
            }
            if (current.Status is "PENDING" or "PROCESSING") return new(invoice.PaymentProofUrl!, existingCode);
        }
        var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(0, 1000);
        var frontend = (configuration["Frontend:Url"] ?? "http://localhost:5173").TrimEnd('/');
        var link = await payos.CreatePaymentLinkAsync(orderCode, (long)invoice.TotalFeeAmount,
            $"RETRACK {invoice.PeriodMonth:00}/{invoice.PeriodYear}", ct,
            $"{frontend}/factory?payos=return", $"{frontend}/factory?payos=cancel");
        invoice.PaymentProofUrl = DepotPayOsService.WithOrderCode(link.CheckoutUrl, orderCode);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(invoice.PaymentProofUrl, orderCode);
    }

    public async Task<string> SyncPayOsAsync(Guid userId, Guid id, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var invoice = await LockInvoice(id, ct);
        if (invoice.PayerId != userId) throw new DepotForbiddenException();
        if (invoice.Status == "PAID") return "PAID";
        if (DepotPayOsService.OrderCodeOf(invoice) is not long code)
            throw new DepotConflictException("Hóa đơn chưa có giao dịch PayOS.");
        var payment = await payos.GetPaymentAsync(code, ct);
        if (payment.Status != "PAID") return payment.Status;
        if (payment.Amount != (long)invoice.TotalFeeAmount)
            throw new DepotConflictException("Số tiền PayOS không khớp hóa đơn; chờ Admin đối soát.");
        MarkPaid(invoice, payment);
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return "PAID";
    }

    private async Task<Retrack.API.Models.PlatformInvoice> LockInvoice(Guid id, CancellationToken ct) =>
        await Db.PlatformInvoices.FromSqlInterpolated($"SELECT * FROM platform_invoices WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

    private static void MarkPaid(Retrack.API.Models.PlatformInvoice invoice, PayOsPayment payment)
    {
        invoice.Status = "PAID";
        invoice.PaidAt = payment.PaidAt ?? DateTime.UtcNow;
        invoice.SubmittedAt ??= invoice.PaidAt;
    }
}
