using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Factory;

public abstract class FactoryServiceBase(AppDbContext db)
{
    protected AppDbContext Db { get; } = db;

    // Giữ transaction/DbContext hiện hành của Factory; chỉ bổ sung thông báo cho Owner.
    protected async Task NotifyDepotAsync(Guid depotId, string title, string message, Guid? batchId = null,
        string? targetType = null, CancellationToken ct = default)
    {
        var ownerId = await Db.Depots.Where(x => x.Id == depotId).Select(x => x.OwnerId).SingleAsync(ct);
        Db.Notifications.Add(Notification.ForDepot(ownerId, depotId, "FACTORY", title, message, batchId, targetType));
    }

    // Các thao tác nhận/cân/KCS/quyết toán dùng cùng thứ tự khóa với Depot.
    protected async Task<InventoryBatch?> LockOrderAsync(Guid id, Guid factoryId, CancellationToken ct)
    {
        var depotId = await Db.InventoryBatches.AsNoTracking().Where(x => x.Id == id && x.TargetFactoryId == factoryId)
            .Select(x => (Guid?)x.DepotId).SingleOrDefaultAsync(ct);
        if (depotId is null) return null;
        if (Db.Database.IsRelational())
            await Db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {depotId.Value} FOR UPDATE").LoadAsync(ct);
        var query = Db.Database.IsRelational()
            ? Db.InventoryBatches.FromSqlInterpolated($"SELECT * FROM inventory_batches WHERE id = {id} AND target_factory_id = {factoryId} FOR UPDATE")
            : Db.InventoryBatches.Where(x => x.Id == id && x.TargetFactoryId == factoryId);
        var batch = await query.Include(x => x.QualityCheck).SingleOrDefaultAsync(ct);
        if (batch != null)
        {
            await Db.Entry(batch).ReloadAsync(ct);
            if (batch.QualityCheck != null) await Db.Entry(batch.QualityCheck).ReloadAsync(ct);
        }
        return batch;
    }

    protected async Task<Retrack.API.Models.Factory> CurrentFactory(Guid userId, CancellationToken ct = default)
    {
        var factory = await Db.Factories.SingleOrDefaultAsync(x => x.OwnerId == userId, ct);
        if (factory is not null) return factory;

        var owner = await Db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (owner is null || !string.Equals(owner.Role, "FACTORY", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Tài khoản chưa có hồ sơ nhà máy.");

        factory = new Retrack.API.Models.Factory { OwnerId = owner.Id, Name = owner.FullName, Address = "Chưa cập nhật" };
        Db.Factories.Add(factory);
        await Db.SaveChangesAsync(ct);
        return factory;
    }
}
