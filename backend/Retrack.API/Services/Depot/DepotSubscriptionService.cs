using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed partial class DepotSubscriptionService(IDepotSubscriptionRepository repository, IPayOsClient payos, TimeProvider clock) : IDepotSubscriptionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<bool> IsSubscriptionOnlyAsync() => SubscriptionRules.Enabled(await repository.SettingsAsync());
    public async Task<PlatformPaymentAccountDto> PaymentAccountAsync(Guid ownerId, Guid depotId)
    {
        await OwnerAsync(ownerId, depotId);
        var settings = await repository.SettingsAsync();
        return new(settings.GetValueOrDefault("PLATFORM_FEE_BANK_NAME", ""), settings.GetValueOrDefault("PLATFORM_FEE_ACCOUNT_NUMBER", ""), settings.GetValueOrDefault("PLATFORM_FEE_ACCOUNT_HOLDER", ""));
    }
    private async Task OwnerAsync(Guid owner, Guid depot)
    {
        if (!await repository.IsActiveOwnerAsync(owner, depot)) throw new DepotForbiddenException();
    }
    private async Task AdminAsync(Guid admin)
    {
        if (!await repository.IsAdminAsync(admin)) throw new DepotForbiddenException();
    }
    public async Task<DepotSubscriptionDto> GetAsync(Guid ownerId, Guid depotId)
    {
        await OwnerAsync(ownerId, depotId);
        var settings = await repository.SettingsAsync();
        var s = await repository.FindAsync(depotId);
        var enabled = SubscriptionRules.Enabled(settings);
        var access = !enabled || SubscriptionRules.HasAccess(s, Now);
        return new(depotId, !enabled ? "DISABLED" : s?.PaidUntil > Now ? "ACTIVE" : s?.TrialEndsAt > Now ? "TRIAL" : s?.TrialStartedAt == null && s?.PaidUntil == null ? "NOT_STARTED" : "EXPIRED",
            access, enabled && s?.TrialStartedAt == null && s?.PaidUntil == null, s?.TrialEndsAt, s?.PaidUntil, SubscriptionRules.Price(settings), SubscriptionRules.TrialDays, payos.IsConfigured);
    }
    public async Task RequireAccessAsync(Guid depotId)
    {
        if (SubscriptionRules.Enabled(await repository.SettingsAsync()) && !SubscriptionRules.HasAccess(await repository.FindAsync(depotId), Now))
            throw new DepotSubscriptionRequiredException();
    }
    public async Task<DepotSubscriptionDto> StartTrialAsync(Guid ownerId, Guid depotId)
    {
        await OwnerAsync(ownerId, depotId);
        await using var tx = await repository.BeginAsync();
        var depot = await repository.LockDepotAsync(depotId);
        if (depot.VerificationStatus != "APPROVED") throw new DepotConflictException("Kho cần được xác minh trước khi bắt đầu dùng thử.");
        var s = await repository.FindAsync(depotId);
        if (s?.TrialStartedAt == null && s?.PaidUntil == null)
        {
            if (s == null) { s = new() { DepotId = depotId }; repository.Add(s); }
            s.TrialStartedAt = Now; s.TrialEndsAt = Now.AddDays(SubscriptionRules.TrialDays); s.UpdatedAt = Now;
            repository.Audit(ownerId, "SUBSCRIPTION_TRIAL_STARTED", depotId, "Dùng thử 14 ngày một lần cho kho.");
            await repository.SaveAsync();
        }
        await tx.CommitAsync();
        return await GetAsync(ownerId, depotId);
    }
    public async Task<PagedResult<SubscriptionOrderDto>> OrdersAsync(Guid userId, Guid? depotId, int page, int pageSize, string? status = null)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue || status is not (null or "" or "PENDING" or "PAID" or "CANCELLED"))
            throw new ArgumentException("Phân trang hoặc trạng thái đơn gói không hợp lệ.");
        if (depotId.HasValue) await OwnerAsync(userId, depotId.Value); else await AdminAsync(userId);
        return await repository.ListAsync(depotId, page, pageSize, status);
    }
    public async Task<SubscriptionOrderDto> CreateOrderAsync(Guid ownerId, Guid depotId, Guid operationId)
    {
        await OwnerAsync(ownerId, depotId);
        if (operationId == Guid.Empty) throw new ArgumentException("Cần mã thao tác mua/gia hạn.");
        await using var tx = await repository.BeginAsync(); await repository.LockDepotAsync(depotId);
        var old = await repository.OrderAsync(operationId);
        if (old != null)
        {
            if (old.DepotId != depotId || old.PayerId != ownerId) throw new DepotConflictException("Mã thao tác đã dùng cho đơn khác.");
            return Map(old);
        }
        var pending = await repository.PendingAsync(depotId);
        if (pending != null)
        {
            if (pending.PayerId != ownerId) throw new DepotConflictException("Kho còn đơn gói của người thanh toán trước; cần Admin đối soát.");
            return Map(pending);
        }
        var settings = await repository.SettingsAsync();
        if (!SubscriptionRules.Enabled(settings)) throw new DepotConflictException("Gói quản lý chưa được mở bán.");
        var order = new DepotSubscriptionOrder { Id = operationId, DepotId = depotId, PayerId = ownerId,
            Amount = SubscriptionRules.Price(settings), OrderCode = await repository.NextCodeAsync(), CreatedAt = Now };
        repository.Add(order); repository.Audit(ownerId, "SUBSCRIPTION_ORDER_CREATED", order.Id, $"Mua một tháng cho kho {depotId}, giá {order.Amount:0}đ.");
        await repository.SaveAsync(); await tx.CommitAsync(); return Map(order);
    }
    public async Task SubmitProofAsync(Guid ownerId, Guid depotId, Guid orderId, SubscriptionProofDto dto)
    {
        await OwnerAsync(ownerId, depotId);
        if (!Uri.TryCreate(dto.ProofUrl, UriKind.Absolute, out var url) || url.Scheme != "https" || dto.ProofUrl.Length > 2048 || string.IsNullOrWhiteSpace(dto.Reference) || dto.Reference.Length > 255)
            throw new ArgumentException("Cần ảnh chứng từ HTTPS và mã tham chiếu thanh toán gói.");
        await using var tx = await repository.BeginAsync(); await repository.LockDepotAsync(depotId);
        var order = await OwnedOrderAsync(ownerId, depotId, orderId);
        if (order.Status != "PENDING") throw new DepotConflictException("Đơn gói không còn chờ thanh toán.");
        if (order.ProviderStartedAt != null) throw new DepotConflictException("Đơn đã tạo thanh toán PayOS. Kiểm tra trạng thái, không chuyển thêm tiền bằng cách khác.");
        order.ProofUrl = dto.ProofUrl; order.PaymentReference = dto.Reference.Trim();
        order.ReviewNote = null;
        repository.Audit(ownerId, "SUBSCRIPTION_PROOF_SUBMITTED", orderId, "Đã gửi chứng từ, chưa kích hoạt gói.");
        await repository.SaveAsync(); await tx.CommitAsync();
    }
    public async Task ApproveAsync(Guid adminId, Guid orderId, string reference)
    {
        await AdminAsync(adminId);
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 255) throw new ArgumentException("Cần mã giao dịch/căn cứ đối soát đã nhận tiền thực tế.");
        var found = await repository.OrderAsync(orderId) ?? throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        await using var tx = await repository.BeginAsync(); await repository.LockDepotAsync(found.DepotId);
        var order = await repository.OrderAsync(orderId) ?? throw new KeyNotFoundException();
        if (order.Status == "PAID") return;
        if (order.Status != "PENDING" || order.ProofUrl == null) throw new DepotConflictException("Cần đơn đang chờ và chứng từ trước khi Admin đối soát.");
        // Link đã phát hành phải được đồng bộ PayOS trước; không duyệt tay rồi lại thu tiền trên link đang mở.
        if (order.ProviderStartedAt != null) throw new DepotConflictException("Đơn đã dùng PayOS; hãy đồng bộ trạng thái thanh toán thay vì duyệt tay.");
        order.PaymentReference = reference.Trim(); order.ApprovedBy = adminId;
        await ActivateAsync(order); await repository.SaveAsync(); await tx.CommitAsync();
    }
    private async Task ActivateAsync(DepotSubscriptionOrder order)
    {
        if (order.Status == "PAID") return;
        if (order.Status != "PENDING") throw new DepotConflictException("Đơn gói đã đóng; cần đối soát riêng nếu tiền về muộn.");
        var s = await repository.FindAsync(order.DepotId);
        if (s == null) { s = new() { DepotId = order.DepotId }; repository.Add(s); }
        order.PeriodStart = SubscriptionRules.RenewalStart(s, Now);
        order.PeriodEnd = SubscriptionRules.AddMonth(order.PeriodStart.Value);
        s.PaidUntil = order.PeriodEnd; s.UpdatedAt = Now;
        order.Status = "PAID"; order.PaidAt = Now;
        order.ReviewNote = null;
        repository.Audit(order.ApprovedBy ?? order.PayerId, "SUBSCRIPTION_ACTIVATED", order.Id, $"Kho {order.DepotId}; hạn mới {s.PaidUntil:O}.");
    }
    public async Task<SubscriptionAdminSummaryDto> AdminSummaryAsync(Guid adminId) { await AdminAsync(adminId); return await repository.SummaryAsync(); }
    public async Task RejectProofAsync(Guid adminId, Guid orderId, string reason)
    {
        await AdminAsync(adminId);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 1000) throw new ArgumentException("Cần lý do bổ sung chứng từ từ 5 đến 1.000 ký tự.");
        var found = await repository.OrderAsync(orderId) ?? throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        await using var tx = await repository.BeginAsync(); await repository.LockDepotAsync(found.DepotId);
        var order = (await repository.OrderAsync(orderId))!;
        if (order.Status != "PENDING" || order.ProviderStartedAt != null) throw new DepotConflictException("Chỉ yêu cầu bổ sung cho chứng từ chuyển khoản của đơn chưa kích hoạt.");
        if (order.ProofUrl == null && order.ReviewNote == reason.Trim()) return;
        if (order.ProofUrl == null) throw new DepotConflictException("Chưa có chứng từ để đối soát.");
        repository.Audit(adminId, "SUBSCRIPTION_PROOF_REJECTED", order.Id, System.Text.Json.JsonSerializer.Serialize(new { order.ProofUrl, order.PaymentReference, Reason = reason.Trim() }));
        order.ProofUrl = null; order.PaymentReference = null; order.ReviewNote = reason.Trim();
        await repository.SaveAsync(); await tx.CommitAsync();
    }
    public async Task SetPriceAsync(Guid adminId, decimal price)
    {
        await AdminAsync(adminId);
        if (price is < 1000 or > 10000000 || decimal.Truncate(price) != price) throw new ArgumentException("Giá tháng phải là số nguyên đồng từ 1.000 đến 10.000.000đ.");
        await using var tx = await repository.BeginAsync(); await repository.SetPriceAsync(price, adminId); await repository.SaveAsync(); await tx.CommitAsync();
    }
    private async Task<DepotSubscriptionOrder> OwnedOrderAsync(Guid owner, Guid depot, Guid id)
    {
        var order = await repository.OrderAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy đơn gói.");
        if (order.DepotId != depot || order.PayerId != owner) throw new DepotForbiddenException();
        return order;
    }
    private static SubscriptionOrderDto Map(DepotSubscriptionOrder o) => new(o.Id, o.DepotId, "", o.Amount, o.Status, o.CreatedAt, o.PaidAt, o.PeriodStart, o.PeriodEnd, o.CheckoutUrl, o.ProofUrl, o.PaymentReference, o.ProviderStartedAt != null, o.ReviewNote);
}

public sealed class DepotSubscriptionRequiredException() : Exception("Gói quản lý kho chưa bắt đầu hoặc đã hết hạn. Vào Gói dịch vụ để dùng thử, mua hoặc gia hạn; dữ liệu và giao dịch đang xử lý vẫn được giữ.");
