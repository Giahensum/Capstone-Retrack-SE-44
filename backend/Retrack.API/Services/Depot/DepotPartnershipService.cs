using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

public sealed class DepotPartnershipService(IDepotService scope, IDepotPartnershipRepository repository, IDepotUnitOfWork work) : IDepotPartnershipService
{
    private async Task<Guid> ResolveAsync(Guid ownerId, Guid? depotId)
    {
        if (depotId.HasValue) { await scope.RequireOwnerAsync(ownerId, depotId.Value); return depotId.Value; }
        var depots = await scope.GetDepotsAsync(ownerId);
        if (depots.Count > 1) throw new ArgumentException("Vui lòng chọn kho bằng depotId.");
        return depots.SingleOrDefault()?.Id ?? throw new KeyNotFoundException("Tài khoản chưa có hồ sơ vựa.");
    }
    public async Task<PagedResult<DepotPartnershipDto>> ListAsync(Guid ownerId, Guid? depotId, DepotQuery query, CancellationToken ct) =>
        await repository.ListAsync(await ResolveAsync(ownerId, depotId), query, ct);
    public async Task<DepotPartnershipStatusDto> UpdateAsync(Guid ownerId, Guid? depotId, Guid factoryId, string status, CancellationToken ct)
    {
        var selected = await ResolveAsync(ownerId, depotId);
        if (status is not ("BLOCKED" or "UNBLOCKED")) throw new ArgumentException("Chủ kho chỉ được chặn hoặc bỏ chặn của mình. Nhà máy quyết định hợp tác sau kiểm tra hàng.");
        await using var tx = await work.BeginAsync();
        await repository.LockDepotAsync(selected, ct);
        var partnership = await repository.FindAsync(selected, factoryId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hợp tác.");
        partnership.BlockedByDepot = status == "BLOCKED";
        partnership.UpdatedAt = DateTime.UtcNow;
        await repository.SaveAsync(ct);
        await tx.CommitAsync();
        return new(partnership.Id, partnership.FactoryId, partnership.DepotId, partnership.EffectiveStatus);
    }
}
