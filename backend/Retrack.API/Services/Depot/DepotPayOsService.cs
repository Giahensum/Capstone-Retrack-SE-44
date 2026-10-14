using System.Text.Json;
using Retrack.API.DTOs.Depot;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

/// <summary>Thanh toán hóa đơn phí nền tảng qua PayOS. Hóa đơn PAID chỉ được đặt khi PayOS xác nhận (webhook ký hoặc truy vấn trực tiếp), không tin dữ liệu từ trình duyệt.</summary>
public sealed class DepotPayOsService(IDepotUnitOfWork work, IDepotReportRepository reports, IDepotService scope, IPayOsClient payos, ILogger<DepotPayOsService> logger) : IDepotPayOsService
{
    public async Task<PayOsLinkDto> CreateLinkAsync(Guid ownerId, Guid depotId, Guid invoiceId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        if (!payos.IsConfigured) throw new InvalidOperationException("PayOS chưa được cấu hình trên máy chủ.");
        await using var tx = await work.BeginAsync();
        var invoice = await reports.LockInvoiceAsync(invoiceId) ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.PayerId != ownerId) throw new DepotForbiddenException();
        if (invoice.Status != "PENDING") throw new DepotConflictException("Hóa đơn không còn ở trạng thái chờ thanh toán.");
        if (invoice.TotalFeeAmount <= 0 || invoice.TotalFeeAmount != decimal.Truncate(invoice.TotalFeeAmount))
            throw new InvalidOperationException("Số tiền hóa đơn phải là số nguyên VND lớn hơn 0.");

        // Dùng lại link cũ nếu PayOS vẫn coi là đang chờ; nếu đã hết hạn/hủy thì tạo đơn mới.
        if (OrderCodeOf(invoice) is long old)
        {
            var current = await payos.GetPaymentAsync(old);
            if (current.Status == "PAID")
            {
                Apply(invoice, current); await work.SaveAsync(); await tx.CommitAsync();
                throw new DepotConflictException("Hóa đơn đã được thanh toán.");
            }
            if (current.Status is "PENDING" or "PROCESSING") return new(invoice.PaymentProofUrl!, old);
        }

        var orderCode = NewOrderCode();
        var link = await payos.CreatePaymentLinkAsync(orderCode, (long)invoice.TotalFeeAmount, $"RETRACK {invoice.PeriodMonth:00}/{invoice.PeriodYear}");
        // DB hiện có không có cột PayOS: lưu link + mã đơn vào payment_proof_url (hóa đơn vẫn PENDING).
        invoice.PaymentProofUrl = WithOrderCode(link.CheckoutUrl, orderCode);
        await work.SaveAsync(); await tx.CommitAsync();
        return new(invoice.PaymentProofUrl, orderCode);
    }

    public async Task<string> SyncAsync(Guid ownerId, Guid depotId, Guid invoiceId)
    {
        await scope.RequireOwnerAsync(ownerId, depotId);
        await using var tx = await work.BeginAsync();
        var invoice = await reports.LockInvoiceAsync(invoiceId) ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        if (invoice.PayerId != ownerId) throw new DepotForbiddenException();
        if (invoice.Status == "PAID") return "PAID";
        if (OrderCodeOf(invoice) is not long code) throw new DepotConflictException("Hóa đơn chưa tạo link thanh toán PayOS.");
        var payment = await payos.GetPaymentAsync(code);
        if (payment.Status != "PAID") return payment.Status;
        if (payment.Amount != (long)invoice.TotalFeeAmount) throw new DepotConflictException("Số tiền PayOS ghi nhận không khớp hóa đơn; chờ Admin đối soát.");
        Apply(invoice, payment); await work.SaveAsync(); await tx.CommitAsync();
        return "PAID";
    }

    public async Task<bool> HandleWebhookAsync(JsonElement body)
    {
        if (!body.TryGetProperty("data", out var data) || !body.TryGetProperty("signature", out var sig) || sig.ValueKind != JsonValueKind.String) return false;
        if (!payos.VerifyWebhookData(data, sig.GetString()!)) return false;
        // Webhook thanh toán thành công: success=true và data.code="00"
        var ok = body.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True
                 && data.TryGetProperty("code", out var c) && c.GetString() == "00";
        if (!ok || !data.TryGetProperty("orderCode", out var oc) || !oc.TryGetInt64(out var orderCode)) return true;
        await using var tx = await work.BeginAsync();
        var invoice = await reports.LockInvoiceByPayosOrderAsync(orderCode);
        if (invoice == null) return true; // đơn lạ / ping kiểm tra webhook của PayOS
        if (invoice.Status == "PAID") return true; // PayOS retry: idempotent
        var amount = data.TryGetProperty("amount", out var a) && a.TryGetInt64(out var v) ? v : -1;
        if (amount != (long)invoice.TotalFeeAmount)
        {
            logger.LogWarning("PayOS webhook sai số tiền cho hóa đơn {InvoiceId}: {Amount}", invoice.Id, amount);
            return true; // giữ nguyên để Admin đối soát thủ công
        }
        var reference = data.TryGetProperty("reference", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        Apply(invoice, new("PAID", amount, reference, DateTime.UtcNow));
        await work.SaveAsync(); await tx.CommitAsync();
        return true;
    }

    private static void Apply(Models.PlatformInvoice invoice, PayOsPayment payment)
    {
        invoice.Status = "PAID"; invoice.PaidAt = payment.PaidAt ?? DateTime.UtcNow;
        invoice.SubmittedAt ??= invoice.PaidAt;
    }

    public static string WithOrderCode(string checkoutUrl, long orderCode) => $"{checkoutUrl}{(checkoutUrl.Contains('?') ? '&' : '?')}orderCode={orderCode}";

    /// <summary>Mã đơn PayOS nằm cuối payment_proof_url của hóa đơn PENDING; chứng từ do người dùng gửi không có dạng này.</summary>
    public static long? OrderCodeOf(Models.PlatformInvoice invoice)
    {
        var url = invoice.PaymentProofUrl;
        var at = url?.LastIndexOf("orderCode=", StringComparison.Ordinal) ?? -1;
        return at >= 0 && long.TryParse(url![(at + 10)..], out var code) ? code : null;
    }

    // orderCode PayOS là số nguyên dương <= 2^53-1; chỉ mục UNIQUE trong DB chặn trùng.
    private static long NewOrderCode() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(0, 1000);
}
