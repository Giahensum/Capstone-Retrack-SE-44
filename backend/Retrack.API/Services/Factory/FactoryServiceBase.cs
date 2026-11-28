using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Services.Factory;

public abstract class FactoryServiceBase(AppDbContext db, IFactoryOrderRepository? orders = null, IFactoryUnitOfWork? unitOfWork = null,
    IFactoryProfileRepository? profiles = null)
{
    protected AppDbContext Db { get; } = db;
    protected IFactoryOrderRepository OrderRepository { get; } = orders ?? new FactoryOrderRepository(db);
    protected IFactoryUnitOfWork Work { get; } = unitOfWork ?? new FactoryUnitOfWork(db);
    protected IFactoryProfileRepository Profiles { get; } = profiles ?? new FactoryProfileRepository(db);

    // Giữ transaction/DbContext hiện hành của Factory; chỉ bổ sung thông báo cho Owner.
    protected async Task NotifyDepotAsync(Guid depotId, string title, string message, Guid? batchId = null,
        string? targetType = null, CancellationToken ct = default)
    {
        var ownerId = await Db.Depots.Where(x => x.Id == depotId).Select(x => x.OwnerId).SingleAsync(ct);
        Db.Notifications.Add(Notification.ForDepot(ownerId, depotId, "FACTORY", title, message, batchId, targetType));
    }

    // Các thao tác nhận/cân/KCS/quyết toán dùng cùng thứ tự khóa với Depot.
    protected Task<InventoryBatch?> LockOrderAsync(Guid id, Guid factoryId, CancellationToken ct) =>
        OrderRepository.LockAsync(id, factoryId, ct);

    protected async Task<Retrack.API.Models.Factory> CurrentFactory(Guid userId, CancellationToken ct = default)
    {
        var owner = await Profiles.FindOwnerAsync(userId, ct);
        if (owner is null || !owner.IsActive || !string.Equals(owner.Role, "FACTORY", StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Tài khoản chưa có hồ sơ nhà máy.");

        var factory = await Profiles.FindByOwnerAsync(userId, ct);
        if (factory is not null) return factory;

        factory = new Retrack.API.Models.Factory { OwnerId = owner.Id, Name = owner.FullName, Address = "Chưa cập nhật" };
        Profiles.Add(factory);
        await Work.SaveAsync(ct);
        return factory;
    }
}
