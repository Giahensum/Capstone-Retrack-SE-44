using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Factory;
using Retrack.API.Models;

namespace Retrack.API.Controllers.Factory;

[Route("api/factory/qc")]
[Authorize(Roles = "FACTORY")]
public class FactoryQCController(Retrack.API.Data.AppDbContext db, IConfiguration configuration) : FactoryControllerBase(db)
{
    [HttpPost("orders/{id:guid}/weigh")]
    public async Task<IActionResult> Weigh(Guid id, WeighRequest request, CancellationToken ct)
    {
        if (request.GrossWeightKg <= request.TareWeightKg)
            return BadRequest(new { success = false, message = "Khối lượng tổng phải lớn hơn khối lượng bì." });
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.Include(x => x.QualityCheck)
            .SingleOrDefaultAsync(x => x.Id == id && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (batch.Status is not ("RECEIVED" or "WEIGHED"))
            return Conflict(new { success = false, message = "Chỉ cân sau khi nhận hàng và trước khi chốt KCS." });
        var net = request.GrossWeightKg - request.TareWeightKg;
        var difference = batch.DeclaredWeightKg == 0 ? 0 : (net - batch.DeclaredWeightKg) / batch.DeclaredWeightKg * 100m;
        var threshold = configuration.GetValue<decimal?>("Factory:WeightDiscrepancyPercentage") ?? 5m;
        if (Math.Abs(difference) > threshold && string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { success = false, message = $"Lệch cân trên {threshold}%: cần ghi chú biên bản." });
        var qc = batch.QualityCheck;
        if (qc is null)
        {
            qc = new BatchQualityCheck
            {
                BatchId = batch.Id, FactoryId = factory.Id, ActualWeightKg = net, Grade = "PENDING",
                AgreedPricePerKg = 0, GrossAmount = 0, NetAmount = 0, IsAccepted = false
            };
            Db.BatchQualityChecks.Add(qc);
        }
        qc.ActualWeightKg = net;
        qc.GrossWeightKg = request.GrossWeightKg;
        qc.TareWeightKg = request.TareWeightKg;
        qc.DifferencePercentage = decimal.Round(difference, 2);
        qc.TicketNumber = request.TicketNumber?.Trim();
        qc.TicketImageUrl = request.TicketImageUrl;
        qc.QualityNote = request.Note?.Trim();
        qc.IsAccepted = false;
        batch.ActualWeightKg = net;
        batch.Status = "WEIGHED";
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = new { id = batch.Id, status = batch.Status, grossWeightKg = request.GrossWeightKg, tareWeightKg = request.TareWeightKg, netWeightKg = net, depotWeightKg = batch.DeclaredWeightKg, differencePercentage = decimal.Round(difference, 2), flagged = Math.Abs(difference) > threshold } });
    }

    [HttpPost("orders/{id:guid}/quality")]
    public async Task<IActionResult> Quality(Guid id, QualityRequest request, CancellationToken ct)
    {
        var factory = await CurrentFactory(ct);
        var batch = await Db.InventoryBatches.Include(x => x.QualityCheck)
            .SingleOrDefaultAsync(x => x.Id == id && x.TargetFactoryId == factory.Id, ct);
        if (batch is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
        if (batch.Status != "WEIGHED" || batch.QualityCheck is null)
            return Conflict(new { success = false, message = "Cần hoàn tất cân trước khi chốt KCS." });
        if (request.Accept && request.PurityPercent < factory.MinimumPurityPercent)
            return BadRequest(new { success = false, message = $"Độ tinh khiết thấp hơn ngưỡng nhà máy ({factory.MinimumPurityPercent}%)." });
        if (!request.Accept && (string.IsNullOrWhiteSpace(request.Note) || string.IsNullOrWhiteSpace(request.Resolution)))
            return BadRequest(new { success = false, message = "Từ chối cần lý do và hướng xử lý." });
        var qc = batch.QualityCheck;
        qc.PurityPercent = request.PurityPercent;
        qc.MoisturePercent = request.MoisturePercent;
        qc.ContaminationPercent = request.ContaminationPercent;
        qc.Grade = request.Grade;
        qc.Resolution = request.Accept ? null : request.Resolution;
        qc.QualityNote = request.Accept ? request.Note?.Trim() : $"{request.Resolution}: {request.Note?.Trim()}";
        qc.IsAccepted = request.Accept;
        batch.Status = request.Accept ? "VERIFIED" : "REJECTED";
        batch.FactoryDecidedAt = DateTime.UtcNow;
        batch.RejectionReason = request.Accept ? null : request.Note?.Trim();
        await Db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = request.Accept ? "Đã chấp nhận kết quả KCS." : "Đã ghi nhận từ chối nghiệm thu.", data = new { id = batch.Id, status = batch.Status, qc.PurityPercent, qc.MoisturePercent, qc.ContaminationPercent, qc.Grade, qc.IsAccepted } });
    }
}
