using System.Text.Json;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Shipping;

// Callback chỉ ghi tín hiệu; không lấy giá hoặc trạng thái chưa xác thực làm dữ liệu nghiệp vụ.
public sealed class ShippingWebhookService(IShippingRepository repository)
{
    public async Task ReceiveAsync(JsonElement payload,CancellationToken ct)
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
            await repository.TouchSandboxWebhookAsync(orderId,ct);
        }
    }
}
