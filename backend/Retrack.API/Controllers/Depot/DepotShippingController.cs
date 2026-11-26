using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.Services.Shipping;

namespace Retrack.API.Controllers.Depot;

[ApiController, Route("api/depot/shipping"), Authorize(Roles="DEPOT_OWNER")]
public sealed class DepotShippingController(ShippingService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("services")]
    public async Task<IActionResult> Services([FromQuery] Guid depotId,CancellationToken ct)=>
        Ok(ApiResponse<object>.Ok(await service.ServicesAsync(Owner,depotId,ct)));
    [HttpPost("quotes")]
    public async Task<IActionResult> Quote([FromQuery] Guid depotId,QuoteRequest input,CancellationToken ct)=>
        Ok(ApiResponse<object>.Ok(ShippingService.QuoteDto(await service.QuoteAsync(Owner,depotId,input,ct))));
    [HttpGet("batches/{id:guid}")]
    public async Task<IActionResult> Get(Guid id,[FromQuery] Guid depotId,CancellationToken ct)=>
        Ok(ApiResponse<object>.Ok(await service.GetAsync(Owner,depotId,id,ct)));
    [HttpPost("batches/{id:guid}/refresh")]
    public async Task<IActionResult> Refresh(Guid id,[FromQuery] Guid depotId,CancellationToken ct) {
        await service.RefreshForOwnerAsync(Owner,depotId,id,ct);
        return Ok(ApiResponse<string>.Ok("Đã kiểm tra trạng thái từ nhà vận chuyển."));
    }
    public sealed record MethodRequest(string Method,Guid? QuoteId);
    [HttpPut("batches/{id:guid}/method")]
    public async Task<IActionResult> Method(Guid id,[FromQuery] Guid depotId,MethodRequest input,CancellationToken ct) {
        await service.SetMethodAsync(Owner,depotId,id,input.Method,input.QuoteId,ct);
        return Ok(ApiResponse<string>.Ok("Đã cập nhật phương thức vận chuyển."));
    }
    [HttpPost("batches/{id:guid}/book")]
    public async Task<IActionResult> Book(Guid id,[FromQuery] Guid depotId,BookShippingRequest input,CancellationToken ct) {
        await service.BookAsync(Owner,depotId,id,input,ct);
        return Ok(ApiResponse<string>.Ok("Đã tiếp nhận yêu cầu vận chuyển."));
    }
    [HttpPost("batches/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id,[FromQuery] Guid depotId,CancellationToken ct) {
        await service.CancelAsync(Owner,depotId,id,ct);
        return Ok(ApiResponse<string>.Ok("Đã gửi yêu cầu hủy. Kiểm tra trạng thái vận đơn trước khi đặt lại."));
    }
    public sealed record RecoverRequest(string OrderId);
    [HttpPost("batches/{id:guid}/recover")]
    public async Task<IActionResult> Recover(Guid id,[FromQuery] Guid depotId,RecoverRequest input,CancellationToken ct) {
        await service.RecoverAsync(Owner,depotId,id,input.OrderId,ct);
        return Ok(ApiResponse<string>.Ok("Đã đối soát vận đơn."));
    }
}
