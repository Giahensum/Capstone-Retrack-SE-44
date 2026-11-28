using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Services.Depot;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Factory;

public sealed record FactoryFeeEntry(Guid Id, string? BatchCode, decimal FeeAmount, DateTime Date);

public sealed class FactoryPlatformFeeService(AppDbContext db, FactoryAttachmentService attachments, Retrack.API.Services.Interfaces.IDepotPayOsService payos,
    IFactoryPlatformFeeRepository? fees = null, IFactoryUnitOfWork? work = null) : FactoryServiceBase(db, unitOfWork: work)
{
    private readonly IFactoryPlatformFeeRepository feeRepository = fees ?? new FactoryPlatformFeeRepository(db);
    private async Task RequireFactory(Guid userId, CancellationToken ct) => _ = await CurrentFactory(userId, ct);

    public async Task<PlatformPaymentAccountDto> PaymentAccountAsync(Guid userId, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        return await feeRepository.PaymentAccountAsync(ct);
    }

    public async Task<PagedResult<FactoryFeeEntry>> FeesAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        return await feeRepository.FeesAsync(userId, page, pageSize, ct);
    }

    public async Task<PagedResult<FeeInvoiceDto>> InvoicesAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        return await feeRepository.InvoicesAsync(userId, page, pageSize, ct);
    }

    public async Task ConfirmAsync(Guid userId, Guid id, FactoryPaymentProofDto proof, CancellationToken ct)
    {
        await RequireFactory(userId, ct);
        if (string.IsNullOrWhiteSpace(proof.PaymentProofUrl) || !attachments.IsOwnedUrl(proof.PaymentProofUrl, userId))
            throw new ArgumentException("Chứng từ phải là tệp do tài khoản Factory này tải lên.");
        if (!(await PaymentAccountAsync(userId, ct)).IsConfigured)
            throw new DepotConflictException("Admin chưa cấu hình tài khoản nhận phí nền tảng.");
        await using var tx = await Work.BeginAsync(ct);
        var invoice = await feeRepository.LockInvoiceAsync(id, ct);
        if (invoice.PayerId != userId) throw new DepotForbiddenException();
        if (invoice.Status != "PENDING" || DepotPayOsService.OrderCodeOf(invoice) != null)
            throw new DepotConflictException("Hóa đơn không còn chờ chứng từ chuyển khoản.");
        invoice.PaymentProofUrl = proof.PaymentProofUrl.Trim();
        invoice.SubmittedAt = DateTime.UtcNow;
        invoice.Status = "SUBMITTED";
        await Work.SaveAsync(ct);
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

}

public sealed class FactoryPaymentProofDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PaymentProofUrl { get; set; } = "";
}
