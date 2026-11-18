using System.Text.Json;
using Retrack.API.DTOs.Depot;
namespace Retrack.API.Services.Interfaces;

public interface IDepotPayOsService
{
    Task<PayOsLinkDto> CreateLinkAsync(Guid ownerId, Guid depotId, Guid invoiceId);
    /// <summary>Tạo link PayOS cho hóa đơn của người nộp phí (không kiểm tra kho); người gọi phải kiểm tra vai trò trước.</summary>
    Task<PayOsLinkDto> CreateLinkForPayerAsync(Guid payerId, Guid invoiceId, Retrack.API.Services.Depot.PayOsRedirect redirect);
    Task<string> SyncForPayerAsync(Guid payerId, Guid invoiceId);
    /// <summary>Đối soát chủ động với PayOS (sau khi quay lại từ trang thanh toán hoặc khi webhook trễ).</summary>
    Task<string> SyncAsync(Guid ownerId, Guid depotId, Guid invoiceId);
    /// <summary>Tác vụ nền: hỏi PayOS và ghi nhận thanh toán, không cần webhook. Trả true khi hóa đơn vừa chuyển PAID.</summary>
    Task<bool> ReconcileAsync(Guid invoiceId);
    Task<List<Guid>> PendingInvoiceIdsAsync(int take);
    /// <summary>Xử lý webhook: trả false khi chữ ký sai.</summary>
    Task<bool> HandleWebhookAsync(JsonElement body);
}
