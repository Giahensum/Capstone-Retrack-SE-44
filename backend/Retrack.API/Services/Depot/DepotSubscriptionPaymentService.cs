using System.Text.Json;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotSubscriptionService
{
    public async Task<PayOsLinkDto> CreateLinkAsync(Guid ownerId, Guid depotId, Guid orderId)
    {
        await OwnerAsync(ownerId, depotId);
        if (!payos.IsConfigured) throw new DepotConflictException("PayOS chưa sẵn sàng. Có thể gửi chứng từ để Admin đối soát thanh toán gói.");
        long code; long amount; string? checkout; bool recovery;
        await using (var tx = await repository.BeginAsync())
        {
            await repository.LockDepotAsync(depotId);
            var order = await OwnedOrderAsync(ownerId, depotId, orderId);
            if (order.Status != "PENDING") throw new DepotConflictException("Đơn gói không còn chờ thanh toán.");
            if (order.ProofUrl != null) throw new DepotConflictException("Đã gửi chứng từ chuyển khoản. Chờ Admin đối soát, không thanh toán thêm qua PayOS.");
            code = order.OrderCode; amount = (long)order.Amount; checkout = order.CheckoutUrl;
            recovery = order.ProviderStartedAt != null;
            if (recovery && checkout == null && order.ProviderStartedAt > Now.AddSeconds(-30))
                throw new DepotConflictException("Đang tạo link. Chờ một lát rồi kiểm tra thanh toán; không trả tiền hai lần.");
            if (!recovery) { order.ProviderStartedAt = Now; await repository.SaveAsync(); }
            await tx.CommitAsync();
        }
        if (checkout != null) return new(checkout, code);
        if (recovery)
        {
            // Không cấp mã khác sau timeout; tìm lại đúng khoản thu để tránh hai lần thanh toán.
            await SyncAsync(ownerId, depotId, orderId);
            var recovered = await repository.OrderAsync(orderId);
            if (recovered?.Status == "PAID") throw new DepotConflictException("Gói đã được kích hoạt.");
            if (recovered?.Status != "PENDING" || recovered.CheckoutUrl == null)
                throw new DepotConflictException("Chưa khôi phục được link. Liên hệ Admin đối soát mã đơn, không tạo thêm khoản thanh toán.");
            return new(recovered.CheckoutUrl, code);
        }
        var link = await payos.CreatePaymentLinkAsync(code, amount, "RT GOI", PayOsRedirect.Subscription);
        if (!Uri.TryCreate(link.CheckoutUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "pay.payos.vn")
            throw new PayOsException("PayOS trả về đường dẫn thanh toán không hợp lệ.");
        await using (var tx = await repository.BeginAsync())
        {
            await repository.LockDepotAsync(depotId);
            var order = await OwnedOrderAsync(ownerId, depotId, orderId);
            if (order.Status != "PENDING") throw new DepotConflictException("Trạng thái đơn đã đổi. Kiểm tra lịch sử trước khi thanh toán.");
            order.CheckoutUrl = link.CheckoutUrl; await repository.SaveAsync(); await tx.CommitAsync();
        }
        return new(link.CheckoutUrl, code);
    }
    public async Task<string> SyncAsync(Guid ownerId, Guid depotId, Guid orderId)
    {
        if (depotId == Guid.Empty) await AdminAsync(ownerId); else await OwnerAsync(ownerId, depotId);
        var found = await repository.OrderAsync(orderId) ?? throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        if (depotId != Guid.Empty && (found.DepotId != depotId || found.PayerId != ownerId)) throw new DepotForbiddenException();
        return await SyncCoreAsync(orderId);
    }
    public Task<List<Guid>> PendingPayOsAsync() => repository.PendingPayOsAsync();
    public async Task ReconcileAsync(Guid orderId) { await SyncCoreAsync(orderId); }
    private async Task<string> SyncCoreAsync(Guid orderId)
    {
        var found = await repository.OrderAsync(orderId) ?? throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        if (found.Status == "PAID") return "PAID";
        if (found.ProviderStartedAt == null) throw new DepotConflictException("Đơn gói chưa thanh toán qua PayOS.");
        // Luân phiên các đơn kể cả khi provider lỗi; một đơn kẹt không chặn đối soát các đơn mới.
        await repository.MarkCheckedAsync(orderId, Now);
        var payment = await payos.GetPaymentAsync(found.OrderCode);
        await using var tx = await repository.BeginAsync(); await repository.LockDepotAsync(found.DepotId);
        var order = (await repository.OrderAsync(orderId))!;
        if (order.Status == "PAID") return "PAID";
        if (payment.Status == "PAID")
        {
            if (payment.Amount != order.Amount) throw new DepotConflictException("Số tiền PayOS không khớp đơn gói. Chờ Admin đối soát, chưa kích hoạt.");
            order.PaymentReference = payment.Reference; await ActivateAsync(order);
        }
        else if (payment.Amount > 0)
            throw new DepotConflictException("Có tiền về nhưng chưa khớp trạng thái/số tiền. Chờ đối soát, không thanh toán thêm.");
        else if (payment.Status is "CANCELLED" or "EXPIRED") order.Status = "CANCELLED";
        else if (order.CheckoutUrl == null && payment.LinkId is { Length: 32 } linkId && linkId.All(Uri.IsHexDigit))
            order.CheckoutUrl = "https://pay.payos.vn/web/" + linkId;
        await repository.SaveAsync(); await tx.CommitAsync(); return order.Status;
    }
    public async Task<bool> WebhookAsync(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) return false;
        if (!body.TryGetProperty("data", out var data) || !body.TryGetProperty("signature", out var sig) || sig.ValueKind != JsonValueKind.String || !payos.VerifyWebhookData(data, sig.GetString()!)) return false;
        if (!data.TryGetProperty("orderCode", out var c) || !c.TryGetInt64(out var code)) return false;
        var found = await repository.OrderByCodeAsync(code);
        if (found == null) return false;
        if (!body.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True || !data.TryGetProperty("code", out var status) || status.GetString() != "00") return true;
        var amount = data.TryGetProperty("amount", out var a) && a.TryGetInt64(out var value) ? value : -1;
        if (amount != found.Amount || found.ProviderStartedAt == null) return true;
        await using var tx = await repository.BeginAsync(); await repository.LockDepotAsync(found.DepotId);
        var order = (await repository.OrderByCodeAsync(code))!;
        if (order.Status == "PAID") return true;
        if (order.Status != "PENDING")
        {
            repository.Audit(order.PayerId, "SUBSCRIPTION_LATE_PAYMENT", order.Id, "Tiền về cho đơn đã đóng; cần Admin đối soát/hoàn riêng.");
            await repository.SaveAsync(); await tx.CommitAsync(); return true;
        }
        order.PaymentReference = data.TryGetProperty("reference", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        await ActivateAsync(order); await repository.SaveAsync(); await tx.CommitAsync(); return true;
    }
}
