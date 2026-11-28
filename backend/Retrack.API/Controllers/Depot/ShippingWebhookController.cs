using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.Services.Shipping;

namespace Retrack.API.Controllers.Depot;

// Callback chỉ là tín hiệu. Không tin giá/trạng thái trong payload chưa xác thực:
// worker luôn đọc trạng thái thật từ API đã ký HMAC trước khi thay dữ liệu nghiệp vụ.
[ApiController, AllowAnonymous, Route("api/shipping/lalamove/webhook")]
public sealed class ShippingWebhookController(ShippingWebhookService service) : ControllerBase
{
    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Receive(JsonElement payload,CancellationToken ct)
    {
        await service.ReceiveAsync(payload,ct);
        return Ok(new {received=true});
    }
}
