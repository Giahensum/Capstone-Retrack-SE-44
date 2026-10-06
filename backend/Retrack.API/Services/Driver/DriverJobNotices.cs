using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Driver;

public static class DriverJobNotices
{
    // Gọi trong transaction tạo chuyến; bên gọi SaveChanges cùng dữ liệu chuyến.
    public static async Task QueueAsync(AppDbContext db, Guid depotId, Guid jobId, CancellationToken ct = default)
    {
        var recipients = await db.DepotStaffs.Where(s => s.DepotId == depotId && s.StaffType == "DRIVER"
            && s.IsActive && s.User.IsActive && s.User.Role == "DRIVER" && s.Depot.Owner.IsActive)
            .Select(s => s.UserId).Distinct().ToListAsync(ct);
        db.Notifications.AddRange(recipients.Select(id => new Notification { UserId = id, TransportJobId = jobId,
            Title = "Có chuyến vận chuyển mới", Message = "Kho có chuyến mới chờ nhận. Mở chi tiết để kiểm tra tình trạng hiện tại." }));
    }
}
