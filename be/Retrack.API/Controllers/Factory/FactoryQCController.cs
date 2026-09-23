using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;
using Retrack.API.Models.Enums;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/qc")]
[Authorize(Roles = "FACTORY")]
public class FactoryQCController(AppDbContext db, IConfiguration configuration) : FactoryControllerBase(db)
{
    [HttpPost("orders/{id:guid}/weigh")]
    public async Task<IActionResult> Weigh(Guid id, WeighRequest request, CancellationToken ct)
    {
        if (request.GrossWeightKg <= request.TareWeightKg)
            return BadRequest(new { success = false, message = "Khối lượng tổng phải lớn hơn khối lượng bì." });
        var factory = await CurrentFactory(ct);
        var order = await Db.BatchOrders.Include(x => x.Batch).Include(x => x.WeightVerification)
            .SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (order.Status != BatchStatus.RECEIVED && order.Status != BatchStatus.WEIGHED)
            return Conflict(new { success = false, message = "Chỉ cân sau khi nhận hàng và trước khi chốt KCS." });
        var net = request.GrossWeightKg - request.TareWeightKg;
        var depotWeight = order.Batch.ActualWeightKg ?? order.Batch.EstimatedWeightKg;
        var difference = depotWeight == 0 ? 0 : (net - depotWeight) / depotWeight * 100m;
        var threshold = configuration.GetValue<decimal?>("Factory:WeightDiscrepancyPercentage") ?? 5m;
        if (Math.Abs(difference) > threshold && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { success = false, message = $"Lệch cân trên {threshold}%: cần ghi chú biên bản." });
        var verification = order.WeightVerification ?? new WeightVerification { Id = Guid.NewGuid(), BatchOrderId = order.Id, DepotWeightKg = depotWeight };
        if (order.WeightVerification is null) Db.WeightVerifications.Add(verification);
        verification.FactoryWeightKg = net;
        verification.DifferencePercentage = decimal.Round(difference, 2);
        verification.Note = request.Note?.Trim();
        verification.IsVerified = false;
        order.Status = BatchStatus.WEIGHED;
        if (order.WeightTicket is null) Db.WeightTickets.Add(new WeightTicket { Id = Guid.NewGuid(), BatchOrderId = order.Id, TicketNumber = request.TicketNumber?.Trim(), GrossWeightKg = request.GrossWeightKg, TareWeightKg = request.TareWeightKg, NetWeightKg = net, TicketImageUrl = request.TicketImageUrl });
        else { order.WeightTicket.TicketNumber = request.TicketNumber?.Trim(); order.WeightTicket.GrossWeightKg = request.GrossWeightKg; order.WeightTicket.TareWeightKg = request.TareWeightKg; order.WeightTicket.NetWeightKg = net; order.WeightTicket.TicketImageUrl = request.TicketImageUrl; }
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { order.Id, status = order.Status.ToString(), grossWeightKg = request.GrossWeightKg, tareWeightKg = request.TareWeightKg, netWeightKg = net, depotWeightKg = depotWeight, differencePercentage = decimal.Round(difference, 2), flagged = Math.Abs(difference) > threshold } });
    }

    [HttpPost("orders/{id:guid}/quality")]
    public async Task<IActionResult> Quality(Guid id, QualityRequest request, CancellationToken ct)
    {
        if (request.Grade is not ("A" or "B" or "C")) return BadRequest(new { success = false, message = "Grade phải là A, B hoặc C." });
        if (!request.Accept && (string.IsNullOrWhiteSpace(request.Note) || string.IsNullOrWhiteSpace(request.Resolution)))
            return BadRequest(new { success = false, message = "Từ chối cần lý do và hướng xử lý RETURN hoặc RENEGOTIATE." });
        var factory = await CurrentFactory(ct);
        var order = await Db.BatchOrders.Include(x => x.WeightVerification)
            .SingleOrDefaultAsync(x => x.Id == id && x.FactoryId == factory.Id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (order.Status != BatchStatus.WEIGHED || order.WeightVerification is null)
            return Conflict(new { success = false, message = "Cần hoàn tất cân trước khi chốt KCS." });
        var verification = order.WeightVerification;
        verification.PurityPercent = request.PurityPercent;
        verification.MoisturePercent = request.MoisturePercent;
        verification.ContaminationPercent = request.ContaminationPercent;
        verification.Grade = request.Grade;
        verification.QualityNote = request.Accept ? request.Note?.Trim() : $"{request.Resolution}: {request.Note?.Trim()}";
        verification.IsVerified = request.Accept;
        order.Status = request.Accept ? BatchStatus.VERIFIED : BatchStatus.REJECTED;
        order.DecidedAt = DateTime.UtcNow;
        order.RejectionReason = request.Accept ? null : request.Note?.Trim();
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = request.Accept ? "Đã chấp nhận kết quả KCS." : "Đã ghi nhận từ chối nghiệm thu.", data = new { order.Id, status = order.Status.ToString(), verification.PurityPercent, verification.MoisturePercent, verification.ContaminationPercent, verification.Grade, verification.QualityNote, verification.IsVerified } });
    }
}
