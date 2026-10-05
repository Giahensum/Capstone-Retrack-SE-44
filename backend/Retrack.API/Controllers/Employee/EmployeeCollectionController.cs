using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Services.Employee;

namespace Retrack.API.Controllers.Employee;

[ApiController, Authorize(Roles = "DEPOT_EMPLOYEE")]
[Route("api/employee")]
public sealed class EmployeeCollectionController(IEmployeeCollectionService service) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");

    [HttpGet("pickup/{pickupId:guid}/classification")]
    public async Task<IActionResult> Get(Guid pickupId, CancellationToken ct) =>
        Ok(ApiResponse<EmployeeCollectionDto>.Ok(await service.GetAsync(UserId, pickupId, ct)));

    [HttpPost("pickup/{pickupId:guid}/checkin")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> CheckIn(Guid pickupId, [FromForm] EmployeeCheckInRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EmployeeCollectionDto>.Ok(await service.CheckInAsync(UserId, pickupId, request, ct), "Check-in thành công."));

    [HttpPut("pickup/{pickupId:guid}/classification")]
    public async Task<IActionResult> Save(Guid pickupId, [FromBody] SaveClassificationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EmployeeCollectionDto>.Ok(await service.SaveAsync(UserId, pickupId, request, ct), "Đã lưu bản nháp kết quả cân."));

    [HttpGet("material-prices")]
    public async Task<IActionResult> Materials(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<MaterialReferenceDto>>.Ok(await service.MaterialsAsync(UserId, ct)));

    [HttpPost("pickup/{pickupId:guid}/submit-weigh")]
    public async Task<IActionResult> Submit(Guid pickupId, RevisionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EmployeeCollectionDto>.Ok(await service.TransitionAsync(UserId, pickupId, "SUBMITTED", request.ExpectedRevision, ct)));

    [HttpPost("pickup/{pickupId:guid}/finalize")]
    public async Task<IActionResult> FinalizeCollection(Guid pickupId, RevisionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EmployeeCollectionDto>.Ok(await service.TransitionAsync(UserId, pickupId, "HANDED_OVER", request.ExpectedRevision, ct)));

    [HttpPost("pickup/{pickupId:guid}/reopen-weigh")]
    public async Task<IActionResult> Reopen(Guid pickupId, RevisionRequest request, CancellationToken ct) =>
        Ok(ApiResponse<EmployeeCollectionDto>.Ok(await service.TransitionAsync(UserId, pickupId, "REOPENED", request.ExpectedRevision, ct)));

    public sealed record RevisionRequest(int ExpectedRevision);
}
