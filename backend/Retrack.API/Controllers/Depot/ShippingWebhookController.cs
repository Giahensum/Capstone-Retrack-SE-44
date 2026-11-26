using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.API.Controllers.Depot;

// Callback chỉ là tín hiệu. Không tin giá/trạng thái trong payload chưa xác thực:
// worker luôn đọc trạng thái thật từ API đã ký HMAC trước khi thay dữ liệu nghiệp vụ.
[ApiController, AllowAnonymous, Route("api/shipping/lalamove/webhook")]
public sealed class ShippingWebhookController(AppDbContext db) : ControllerBase
{
    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Receive(JsonElement payload,CancellationToken ct)
    {
        string? FindId(JsonElement item,int depth=0) {
            if(depth>5 || item.ValueKind!=JsonValueKind.Object)return null;
            foreach(var property in item.EnumerateObject()) {
                if(property.Name is "orderId" or "orderID" && property.Value.ValueKind==JsonValueKind.String)return property.Value.GetString();
                if(property.Value.ValueKind==JsonValueKind.Object && FindId(property.Value,depth+1) is {} id)return id;
            }
            return null;
        }
        var orderId=FindId(payload);
        if(orderId?.Length is >0 and <=100) {
            // Không gọi API trực tiếp theo mỗi callback; hạn mức đối soát do worker kiểm soát.
            // LastCheckedAt giữ nhịp polling, tránh callback giả gây khuếch đại request.
            await db.ExternalShipments.Where(s=>s.Provider=="LALAMOVE_SANDBOX" && s.ProviderOrderId==orderId
                && (s.LastWebhookAt==null || s.LastWebhookAt<DateTime.UtcNow.AddSeconds(-25)))
                .ExecuteUpdateAsync(update=>update.SetProperty(s=>s.LastWebhookAt,DateTime.UtcNow),ct);
        }
        return Ok(new {received=true});
    }
}
