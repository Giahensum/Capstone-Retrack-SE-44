using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Employee;

public interface IEmployeeCollectionService
{
    Task<EmployeeCollectionDto> GetAsync(Guid userId, Guid pickupId, CancellationToken ct);
    Task<EmployeeCollectionDto> CheckInAsync(Guid userId, Guid pickupId, EmployeeCheckInRequest input, CancellationToken ct);
    Task<EmployeeCollectionDto> SaveAsync(Guid userId, Guid pickupId, SaveClassificationRequest input, CancellationToken ct);
    Task<IReadOnlyList<MaterialReferenceDto>> MaterialsAsync(Guid userId, CancellationToken ct);
    Task<EmployeeCollectionDto> TransitionAsync(Guid userId, Guid pickupId, string action, int revision, CancellationToken ct);
}

public sealed partial class EmployeeCollectionService(IEmployeeCollectionRepository repository, IStaffUnitOfWork work, ICloudinaryService images) : IEmployeeCollectionService
{
    private async Task<PickupRequest> OwnAsync(Guid userId, Guid pickupId, bool locked, CancellationToken ct)
    {
        if (!await repository.HasActiveStaffAsync(userId, null, ct)) throw new DepotForbiddenException();
        var pickup = await repository.FindPickupAsync(pickupId, locked, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn thu gom.");
        if (pickup.AcceptedCollectorId != userId || pickup.TargetDepotId == null
            || !await repository.HasActiveStaffAsync(userId, pickup.TargetDepotId, ct))
            throw new KeyNotFoundException("Đơn không thuộc nhân viên hoặc kho của bạn.");
        return pickup;
    }

    public async Task<EmployeeCollectionDto> GetAsync(Guid userId, Guid pickupId, CancellationToken ct)
    {
        // Đọc cùng một snapshot với các thao tác ghi trên đơn.
        await using var transaction = await work.BeginAsync(ct);
        var pickup = await OwnAsync(userId, pickupId, true, ct);
        var result = await Snapshot(pickup, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<EmployeeCollectionDto> CheckInAsync(Guid userId, Guid pickupId, EmployeeCheckInRequest input, CancellationToken ct)
    {
        await using var transaction = await work.BeginAsync(ct);
        var pickup = await OwnAsync(userId, pickupId, true, ct);
        var evidence = await repository.FindCheckInAsync(pickupId, ct);
        // Retry sau khi mất phản hồi không tạo thêm ảnh/thông báo hay thay bằng chứng cũ.
        if (evidence != null)
        {
            var existing = await Snapshot(pickup, ct);
            await transaction.CommitAsync(ct);
            return existing;
        }
        if (pickup.Status != "SCHEDULED") throw new DepotConflictException("Chỉ check-in đơn đã nhận và chưa bắt đầu thu gom.");
        var now = DateTimeOffset.UtcNow;
        var distance = CollectionValidation.ValidateCheckIn(pickup, input, now);
        var file = input.ImageFile;
        if (file == null || file.Length is <= 0 or > 5 * 1024 * 1024) throw new ArgumentException("Ảnh check-in phải có dung lượng từ 1 byte đến 5 MB.");
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAtLeastAsync(header, 8, throwOnEndOfStream: false, cancellationToken: ct);
        var jpeg = read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
        var png = read == 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (!jpeg && !png) throw new ArgumentException("Chỉ chấp nhận nội dung ảnh JPEG hoặc PNG.");
        await using var upload = file.OpenReadStream();
        var url = await images.UploadImageAsync(upload, $"checkin-{pickupId:N}.{(jpeg ? "jpg" : "png")}");
        repository.AddCheckIn(new PickupCheckIn
        {
            PickupRequestId = pickupId, EmployeeId = userId, Latitude = input.Latitude,
            Longitude = input.Longitude, AccuracyMeters = input.AccuracyMeters,
            DistanceMeters = distance, LocationRecordedAt = input.LocationRecordedAt?.UtcDateTime,
            PhotoTakenAt = input.PhotoTakenAt!.Value.UtcDateTime, CheckedInAt = DateTime.UtcNow, ImageUrl = url
        });
        pickup.CheckinImageUrl = url;
        pickup.Status = "IN_PROGRESS";
        pickup.UpdatedAt = DateTime.UtcNow;
        repository.AddNotification(Notification.ForPickup(pickup.SellerId, pickup.Id, "Nhân viên đã đến địa điểm",
            "Nhân viên đã check-in và bắt đầu phân loại, cân phế liệu."));
        await work.SaveAsync(ct);
        var result = await Snapshot(pickup, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<EmployeeCollectionDto> SaveAsync(Guid userId, Guid pickupId, SaveClassificationRequest input, CancellationToken ct)
    {
        if (input.ExpectedRevision < 0) throw new ArgumentException("Phiên bản dữ liệu không hợp lệ.");
        var items = CollectionValidation.Items(input.Items);
        await using var transaction = await work.BeginAsync(ct);
        var pickup = await OwnAsync(userId, pickupId, true, ct);
        var evidence = await repository.FindCheckInAsync(pickupId, ct);
        if (pickup.Status != "IN_PROGRESS" || evidence == null || string.IsNullOrWhiteSpace(pickup.CheckinImageUrl))
            throw new DepotConflictException("Đơn phải được check-in và chưa gửi kết quả cân mới có thể chỉnh sửa.");
        var previous = await repository.ItemsAsync(pickupId, ct);
        if (input.ExpectedRevision != evidence.Revision)
        {
            var same = previous.OrderBy(i => i.MaterialType, StringComparer.Ordinal)
                .Select(i => new ClassificationItemInput(i.MaterialType, i.WeightKg, i.PricePerKg)).SequenceEqual(items);
            if (input.ExpectedRevision == evidence.Revision - 1 && same)
            {
                var retried = await Snapshot(pickup, ct);
                await transaction.CommitAsync(ct);
                return retried;
            }
            throw new DepotConflictException("Kết quả cân đã thay đổi trên phiên khác. Hãy tải lại trước khi lưu.");
        }
        repository.ReplaceItems(previous, items.Select(i => new PickupRequestItem { PickupRequestId = pickupId,
            MaterialType = i.MaterialType, WeightKg = i.WeightKg, PricePerKg = i.PricePerKg,
            SubTotal = CollectionValidation.SubTotal(i.WeightKg, i.PricePerKg) }));
        evidence.Revision++;
        pickup.UpdatedAt = DateTime.UtcNow;
        // Chỉ lưu bản nháp; chưa chốt tiền, phí, trạng thái WEIGHED hoặc tồn kho.
        await work.SaveAsync(ct);
        var result = await Snapshot(pickup, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private async Task<EmployeeCollectionDto> Snapshot(PickupRequest pickup, CancellationToken ct)
    {
        var checkIn = await repository.ReadCheckInAsync(pickup.Id, ct);
        var items = await repository.ReadItemsAsync(pickup.Id, ct);
        return new(pickup.Id, pickup.Status, pickup.Address, pickup.Latitude, pickup.Longitude,
            checkIn == null ? null : new(checkIn.ImageUrl, checkIn.Latitude, checkIn.Longitude,
                checkIn.AccuracyMeters, checkIn.DistanceMeters, checkIn.CheckedInAt), checkIn?.Revision ?? 0,
            pickup.Status == "IN_PROGRESS" && checkIn != null, items, items.Sum(i => i.WeightKg), items.Sum(i => i.SubTotal),
            new(CollectionValidation.RadiusMeters, CollectionValidation.MaxAccuracyMeters, CollectionValidation.MaxLocationAgeSeconds),
            pickup.PlatformFeePercentage, decimal.Round(items.Sum(i => i.SubTotal) * pickup.PlatformFeePercentage / 100, 2),
            items.Sum(i => i.SubTotal) - decimal.Round(items.Sum(i => i.SubTotal) * pickup.PlatformFeePercentage / 100, 2));
    }

    public async Task<IReadOnlyList<MaterialReferenceDto>> MaterialsAsync(Guid userId, CancellationToken ct)
    {
        if (!await repository.HasActiveStaffAsync(userId, null, ct)) throw new DepotForbiddenException();
        var now = DateTime.UtcNow;
        var labels = new[] { "Nhựa PET", "Nhựa HDPE", "Nhựa PVC", "Giấy", "Giấy carton", "Nhôm", "Sắt", "Thép", "Đồng", "Rác thải điện tử", "Khác" };
        var result = new List<MaterialReferenceDto>();
        foreach (var (type, index) in Enum.GetValues<MaterialType>().Select((type, index) => (type, index)))
        {
            var latest = await repository.LatestPriceAsync(type, now, ct);
            result.Add(new(type.ToString(), labels[index], latest?.PricePerKg, latest?.EffectiveDate, latest?.Source));
        }
        return result;
    }
}
