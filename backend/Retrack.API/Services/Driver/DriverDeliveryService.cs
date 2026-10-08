using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Driver;

public sealed record DriverActionRequest(Guid OperationId, string? Reason);
public sealed record DeliveryEventDto(string Action, string? Reason, string? ImageUrl,
    double? DistanceMeters, double? AccuracyMeters, DateTime CreatedAt);
public sealed record DriverDeliveryDto(DriverJobDto Job, CollectionPolicyDto Policy, IReadOnlyList<DeliveryEventDto> Events);

public sealed class DriverDeliveryService(AppDbContext db, DriverJobService jobs, ICloudinaryService images)
{
    public async Task<DriverDeliveryDto> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var job = await jobs.DetailAsync(userId, id, ct);
        var events = await db.DriverDeliveryEvents.AsNoTracking().Where(e => e.JobId == id && e.DriverId == userId)
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Take(50)
            .Select(e => new DeliveryEventDto(e.Action, e.Reason, e.ImageUrl, e.DistanceMeters, e.AccuracyMeters, e.CreatedAt)).ToListAsync(ct);
        return new(job, new(CollectionValidation.RadiusMeters, CollectionValidation.MaxAccuracyMeters,
            CollectionValidation.MaxLocationAgeSeconds), events);
    }

    public async Task ExecuteAsync(Guid userId, Guid id, string action, DriverActionRequest request,
        EmployeeCheckInRequest? evidence, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty) throw new ArgumentException("Thiếu mã thao tác. Hãy mở lại màn hình.");
        if (action is not ("checkin" or "start" or "checkout" or "cancel" or "reject" or "incident"))
            throw new ArgumentException("Thao tác không hợp lệ.");
        var reason = request.Reason?.Trim();
        if (reason?.Length > 1000) throw new ArgumentException("Lý do không được vượt quá 1.000 ký tự.");
        if (action is "cancel" or "reject" or "incident")
            if (reason == null || reason.Length is < 10 or > 1000)
                throw new ArgumentException("Lý do phải từ 10 đến 1.000 ký tự.");
        await jobs.RequireAsync(userId, ct);
        var depotId = await db.TransportJobs.Where(j => j.Id == id).Select(j => (Guid?)j.Batch.DepotId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy chuyến.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Thứ tự khóa thống nhất với Depot: kho → lô → chuyến.
        if (db.Database.IsNpgsql())
            await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId} FOR UPDATE").LoadAsync(ct);
        if (!await db.DepotStaffs.AnyAsync(s => s.UserId == userId && s.DepotId == depotId && s.IsActive
            && s.StaffType == "DRIVER" && s.User.IsActive && s.User.Role == "DRIVER"
            && s.Depot.Owner.IsActive && s.Depot.Owner.Role == "DEPOT_OWNER", ct)) throw new DepotForbiddenException();
        var batchId = await db.TransportJobs.Where(j => j.Id == id).Select(j => j.BatchId).SingleAsync(ct);
        if (db.Database.IsNpgsql())
        {
            await db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {batchId} FOR UPDATE").LoadAsync(ct);
            await db.TransportJobs.FromSqlInterpolated($"SELECT * FROM transport_jobs WHERE id = {id} FOR UPDATE").LoadAsync(ct);
        }
        var job = await db.TransportJobs.Include(j => j.Batch).ThenInclude(b => b.Depot)
            .Include(j => j.Batch).ThenInclude(b => b.TargetFactory).ThenInclude(f => f!.Owner)
            .SingleAsync(j => j.Id == id, ct);
        await db.Entry(job).ReloadAsync(ct);
        await db.Entry(job.Batch).ReloadAsync(ct);
        var previous = await db.DriverDeliveryEvents.AsNoTracking().SingleOrDefaultAsync(e => e.JobId == id
            && e.DriverId == userId && e.OperationId == request.OperationId, ct);
        if (previous != null)
        {
            if (previous.Action != action || previous.Reason != reason)
                throw new DepotConflictException("Mã thao tác đã được dùng cho nội dung khác.");
            await tx.CommitAsync(ct); return;
        }
        if (action == "reject")
        {
            await jobs.DetailAsync(userId, id, ct);
            if (job.DriverId != null || job.Status != "PENDING") throw new DepotConflictException("Chuyến đã được nhận. Hãy tải lại.");
            if (await db.DriverDeliveryEvents.AnyAsync(e => e.JobId == id && e.DriverId == userId && e.Action == "reject", ct))
                throw new DepotConflictException("Bạn đã từ chối chuyến này.");
        }
        else if (job.DriverId != userId) throw new KeyNotFoundException("Chuyến không được giao cho bạn.");

        var batch = job.Batch;
        var entry = new DriverDeliveryEvent { JobId = id, DriverId = userId, OperationId = request.OperationId, Action = action, Reason = reason };
        switch (action)
        {
            case "checkin":
                if (job.Status != "ACCEPTED" || batch.Status is not ("ACCEPTED" or "TRANSPORT_READY" or "READY_FOR_PICKUP")
                    || batch.FactoryReceivedAt != null || batch.DeclaredWeightKg <= 0
                    || batch.TargetFactory?.Owner is not { IsActive: true, Role: "FACTORY" })
                    throw new DepotConflictException("Chỉ lấy hàng khi chuyến đã nhận và lô còn sẵn sàng vận chuyển.");
                await Capture(entry, batch.Depot.Latitude, batch.Depot.Longitude, evidence, ct);
                job.CheckinDepotImageUrl = entry.ImageUrl;
                job.Status = "PICKED_UP";
                // Hàng đã rời kho: dùng trạng thái được InventoryService tính là đã xuất.
                batch.Status = "IN_TRANSIT";
                break;
            case "start":
                if (job.Status != "PICKED_UP" || string.IsNullOrWhiteSpace(job.CheckinDepotImageUrl)
                    || batch.FactoryReceivedAt != null || !CanTravel(batch.Status))
                    throw new DepotConflictException("Cần check-in lấy hàng trước khi khởi hành.");
                job.Status = "IN_TRANSIT";
                break;
            case "checkout":
                if (job.Status is not ("IN_TRANSIT" or "ON_THE_WAY") || batch.FactoryReceivedAt != null
                    || batch.TargetFactory == null || string.IsNullOrWhiteSpace(job.CheckinDepotImageUrl) || !CanTravel(batch.Status))
                    throw new DepotConflictException("Chỉ báo giao hàng sau khi đã lấy hàng và khởi hành.");
                await Capture(entry, batch.TargetFactory.Latitude, batch.TargetFactory.Longitude, evidence, ct);
                job.CheckoutFactoryImageUrl = entry.ImageUrl;
                job.Status = "DELIVERED";
                batch.Status = "DELIVERED";
                // Factory tự xác nhận nhận hàng/QC; Driver không quyết toán hoặc xác nhận thay.
                break;
            case "cancel":
                if (job.Status != "ACCEPTED" || !string.IsNullOrWhiteSpace(job.CheckinDepotImageUrl))
                    throw new DepotConflictException("Chỉ trả chuyến trước khi lấy hàng. Sau đó hãy báo sự cố cho chủ kho.");
                job.DriverId = null;
                job.Status = "PENDING";
                break;
            case "incident":
                if (job.Status is not ("PICKED_UP" or "IN_TRANSIT" or "ON_THE_WAY"))
                    throw new DepotConflictException("Chỉ báo sự cố vận chuyển khi đã lấy hàng và chưa giao.");
                break;
        }
        if (action is not ("reject" or "incident")) job.UpdatedAt = DateTime.UtcNow;
        if (action is "checkin" or "checkout") batch.UpdatedAt = DateTime.UtcNow;
        db.DriverDeliveryEvents.Add(entry);
        var title = action switch {
            "checkin" => "Tài xế đã lấy hàng", "start" => "Chuyến xe đã khởi hành",
            "checkout" => "Tài xế báo đã giao hàng", "cancel" => "Tài xế trả chuyến về pool",
            "reject" => "Tài xế từ chối chuyến", _ => "Sự cố vận chuyển cần xử lý" };
        var recipients = new HashSet<Guid> { batch.Depot.OwnerId };
        if ((action is "start" or "checkout" or "incident") && batch.TargetFactory != null)
            recipients.Add(batch.TargetFactory.OwnerId);
        foreach (var recipient in recipients)
            db.Notifications.Add(new Notification { UserId = recipient, TransportJobId = id, Title = title,
                Message = $"Lô {batch.Code ?? batch.Id.ToString()}. {reason ?? (action == "checkout" ? "Nhà máy vui lòng xác nhận nhận hàng và kiểm định." : title)}" });
        if (action == "cancel") await DriverJobNotices.QueueAsync(db, depotId, id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // Cho phép dữ liệu lấy hàng của bản cũ; không ghi đè lô đã hủy/QC/quyết toán.
    private static bool CanTravel(string status) => status is "IN_TRANSIT" or "IN_PROGRESS"
        or "ACCEPTED" or "TRANSPORT_READY" or "READY_FOR_PICKUP";

    private async Task Capture(DriverDeliveryEvent entry, decimal? latitude, decimal? longitude, EmployeeCheckInRequest? input, CancellationToken ct)
    {
        if (input == null) throw new ArgumentException("Cần ảnh camera và GPS tại địa điểm.");
        var distance = CollectionValidation.ValidateCheckIn(latitude, longitude, input, DateTimeOffset.UtcNow);
        var file = input.ImageFile;
        if (file == null || file.Length is <= 0 or > 5 * 1024 * 1024) throw new ArgumentException("Ảnh phải từ 1 byte đến 5 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAtLeastAsync(header, 8, false, ct);
        var jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        var png = read == 8 && header.SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10});
        if (!jpeg && !png) throw new ArgumentException("Nội dung ảnh phải là JPEG hoặc PNG.");
        await using var upload = file.OpenReadStream();
        entry.ImageUrl = await images.UploadImageAsync(upload, $"driver-{entry.Action}-{entry.OperationId:N}.{(jpeg ? "jpg" : "png")}");
        entry.Latitude = input.Latitude; entry.Longitude = input.Longitude;
        entry.AccuracyMeters = input.AccuracyMeters; entry.DistanceMeters = distance;
        entry.LocationRecordedAt = input.LocationRecordedAt!.Value.UtcDateTime;
        entry.PhotoTakenAt = input.PhotoTakenAt!.Value.UtcDateTime;
    }
}
