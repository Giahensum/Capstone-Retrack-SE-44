using System.Text.Json;
using Retrack.API.DTOs.Depot;
namespace Retrack.API.Services.Interfaces;

public interface IDepotPayOsService
{
    Task<PayOsLinkDto> CreateLinkAsync(Guid ownerId, Guid depotId, Guid invoiceId);
    /// <summary>Đối soát chủ động với PayOS (sau khi quay lại từ trang thanh toán hoặc khi webhook trễ).</summary>
    Task<string> SyncAsync(Guid ownerId, Guid depotId, Guid invoiceId);
    /// <summary>Xử lý webhook: trả false khi chữ ký sai.</summary>
    Task<bool> HandleWebhookAsync(JsonElement body);
}
