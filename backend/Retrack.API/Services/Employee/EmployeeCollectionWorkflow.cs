using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Employee;

public sealed partial class EmployeeCollectionService
{
    public async Task<EmployeeCollectionDto> TransitionAsync(Guid userId, Guid pickupId, string action, int revision, CancellationToken ct)
    {
        if (revision < 0 || action is not ("SUBMITTED" or "HANDED_OVER" or "REOPENED"))
            throw new ArgumentException("Thao tác hoặc phiên bản không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pickup = await OwnAsync(userId, pickupId, true, ct);
        var checkIn = await db.PickupCheckIns.SingleOrDefaultAsync(p => p.PickupRequestId == pickupId, ct)
            ?? throw new DepotConflictException("Đơn chưa có bằng chứng check-in.");
        if (checkIn.EmployeeId != userId) throw new DepotConflictException("Bằng chứng check-in không thuộc nhân viên đang xử lý.");
        if (await db.EmployeeCollectionEvents.AnyAsync(e => e.PickupRequestId == pickupId && e.Kind == action && e.Revision == revision, ct))
        {
            var existing = await Snapshot(pickup, ct);
            await transaction.CommitAsync(ct);
            return existing;
        }
        if (checkIn.Revision != revision) throw new DepotConflictException("Kết quả cân đã thay đổi. Hãy tải lại trước khi xác nhận.");
        if (action != "REOPENED" && !await db.Users.AnyAsync(u => u.Id == pickup.SellerId && u.IsActive && u.Role == "SELLER", ct))
            throw new DepotConflictException("Tài khoản người bán không còn hoạt động. Liên hệ chủ kho để xử lý.");
        var from = pickup.Status;
        var snapshot = await Snapshot(pickup, ct);
        var submitted = await db.EmployeeCollectionEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.PickupRequestId == pickupId && e.Kind == "SUBMITTED" && e.Revision == revision, ct);
        if (action == "SUBMITTED")
        {
            if (from != "IN_PROGRESS" || snapshot.Items.Count == 0 || string.IsNullOrWhiteSpace(pickup.CheckinImageUrl))
                throw new DepotConflictException("Cần check-in và lưu ít nhất một dòng cân hợp lệ trước khi gửi.");
            var items = CollectionValidation.Items(snapshot.Items.Select(i => new ClassificationItemInput(i.MaterialType, i.WeightKg, i.PricePerKg)).ToList());
            if (pickup.PlatformFeePercentage is < 0 or > 100) throw new DepotConflictException("Mức phí của đơn không hợp lệ. Liên hệ chủ kho.");
            pickup.GrossAmount = items.Sum(i => CollectionValidation.SubTotal(i.WeightKg, i.PricePerKg));
            // Giữ contract tiền và mức phí đã chốt của đơn, không tự thay chính sách tài chính.
            pickup.PlatformFeeAmount = decimal.Round(pickup.GrossAmount * pickup.PlatformFeePercentage / 100, 2);
            pickup.NetAmount = pickup.GrossAmount - pickup.PlatformFeeAmount;
            pickup.Status = "WEIGHED";
            Notify(pickup.SellerId, "Kết quả cân chờ xác nhận", $"Đơn {pickup.Id}: nhân viên đã gửi kết quả cân phiên bản {revision}. Vui lòng xem và xác nhận giá.");
        }
        else if (action == "REOPENED")
        {
            // Tương thích Seller hiện trả SCHEDULED khi từ chối. Không giả lập quyết định Seller.
            if (from != "SCHEDULED" || submitted == null)
                throw new DepotConflictException("Chỉ mở lại đơn đã gửi cân và được trả về trạng thái đã nhận.");
            pickup.Status = "IN_PROGRESS";
            checkIn.Revision++;
        }
        else
        {
            if (from != "SELLER_CONFIRMED" || submitted == null)
                throw new DepotConflictException("Chỉ bàn giao khi người bán đã xác nhận kết quả cân được gửi từ ứng dụng.");
            var sent = JsonSerializer.Deserialize<CollectionSubmissionSnapshot>(submitted.SnapshotJson)
                ?? throw new DepotConflictException("Thiếu dữ liệu đối soát kết quả cân.");
            if (sent.GrossAmount != pickup.GrossAmount || sent.NetAmount != pickup.NetAmount
                || sent.PlatformFeeAmount != pickup.PlatformFeeAmount || sent.PlatformFeePercentage != pickup.PlatformFeePercentage
                || !sent.Items.SequenceEqual(snapshot.Items))
                throw new DepotConflictException("Kết quả cân không còn khớp bản đã gửi. Liên hệ chủ kho để kiểm tra.");
            pickup.Status = "AWAITING_PAYMENT";
            var ownerId = await db.Depots.Where(d => d.Id == pickup.TargetDepotId).Select(d => d.OwnerId).SingleAsync(ct);
            Notify(ownerId, "Đơn thu gom chờ thanh toán", $"Đơn {pickup.Id}: người bán đã đồng ý kết quả cân. Nhân viên đã bàn giao; chủ kho kiểm tra và thanh toán.");
            Notify(pickup.SellerId, "Đang chờ chủ kho thanh toán", $"Đơn {pickup.Id} đã được bàn giao cho chủ kho. Nhân viên không thu hoặc chi tiền.");
        }
        pickup.UpdatedAt = DateTime.UtcNow;
        db.EmployeeCollectionEvents.Add(new EmployeeCollectionEvent
        {
            PickupRequestId = pickupId, EmployeeId = userId, Kind = action, Revision = revision,
            FromStatus = from, ToStatus = pickup.Status,
            SnapshotJson = JsonSerializer.Serialize(new CollectionSubmissionSnapshot(snapshot.Items,
                pickup.GrossAmount, pickup.PlatformFeePercentage, pickup.PlatformFeeAmount, pickup.NetAmount))
        });
        Notify(userId, action == "SUBMITTED" ? "Đã gửi kết quả cân" : action == "REOPENED" ? "Đã mở lại kết quả cân" : "Đã bàn giao cho chủ kho",
            $"Đơn {pickup.Id} · phiên bản {revision} · {pickup.Status}. Bạn không thực hiện thanh toán cho người bán.");
        await db.SaveChangesAsync(ct);
        var result = await Snapshot(pickup, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private void Notify(Guid userId, string title, string message) => db.Notifications.Add(new Notification
        { UserId = userId, Title = title, Message = message });
}

public sealed record CollectionSubmissionSnapshot(IReadOnlyList<ClassificationItemDto> Items,
    decimal GrossAmount, decimal PlatformFeePercentage, decimal PlatformFeeAmount, decimal NetAmount);
