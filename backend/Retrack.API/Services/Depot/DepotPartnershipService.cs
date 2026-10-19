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

    public async Task<DepotPartnershipStatusDto> DecideContractAsync(Guid ownerId, Guid? depotId, Guid factoryId, DepotContractDecisionRequest request, CancellationToken ct)
    {
        var selected = await ResolveAsync(ownerId, depotId);
        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("ACCEPT" or "REJECT")) throw new ArgumentException("Quyết định hợp đồng chỉ nhận ACCEPT hoặc REJECT.");
        if (decision == "ACCEPT" && !request.ConfirmElectronicSignature)
            throw new ArgumentException("Cần xác nhận ký điện tử trước khi chấp thuận hợp đồng.");
        await using var tx = await work.BeginAsync();
        await repository.LockDepotAsync(selected, ct);
        var partnership = await repository.FindAsync(selected, factoryId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hợp tác.");
        if (partnership.IsBlocked) throw new DepotConflictException("Quan hệ đang bị chặn; chưa thể xử lý hợp đồng.");
        if (partnership.ContractStatus != "PENDING_DEPOT" || string.IsNullOrWhiteSpace(partnership.ContractTermsHash))
            throw new DepotConflictException("Không có hợp đồng đang chờ chủ vựa xác nhận.");
        var now = DateTime.UtcNow;
        partnership.ContractStatus = decision == "ACCEPT" ? "ACTIVE" : "REJECTED";
        partnership.Status = decision == "ACCEPT" ? "APPROVED" : "DECLINED";
        partnership.DepotSignedBy = decision == "ACCEPT" ? ownerId : null;
        partnership.DepotSignedAt = decision == "ACCEPT" ? now : null;
        partnership.ContractEffectiveAt = decision == "ACCEPT" ? now : null;
        partnership.UpdatedAt = now;
        await repository.SaveAsync(ct);
        await tx.CommitAsync();
        return new(partnership.Id, partnership.FactoryId, partnership.DepotId, partnership.EffectiveStatus,
            partnership.ContractCode, partnership.ContractStatus);
    }

    public async Task<DepotPartnershipStatusDto> RequestTerminationAsync(Guid ownerId, Guid? depotId, Guid factoryId, DepotContractTerminationRequest request, CancellationToken ct)
    {
        var selected = await ResolveAsync(ownerId, depotId);
        var reason = request.Reason?.Trim() ?? "";
        if (reason.Length is < 10 or > 2000) throw new ArgumentException("Lý do chấm dứt phải từ 10 đến 2.000 ký tự.");
        await using var tx = await work.BeginAsync();
        await repository.LockDepotAsync(selected, ct);
        var partner = await repository.FindAsync(selected, factoryId, ct) ?? throw new KeyNotFoundException("Không tìm thấy quan hệ hợp tác.");
        if (partner.ContractStatus != "ACTIVE") throw new DepotConflictException("Chỉ hợp đồng đang hiệu lực mới được đề nghị chấm dứt.");
        if (partner.TerminationStatus is "PENDING_DEPOT" or "PENDING_FACTORY") throw new DepotConflictException("Hợp đồng đã có đề nghị chấm dứt đang chờ xử lý.");
        if (await repository.HasOpenOrdersAsync(selected, factoryId, ct)) throw new DepotConflictException("Cần hoàn tất các lô đang giao, KCS hoặc chờ quyết toán trước khi chấm dứt hợp đồng.");
        partner.TerminationStatus = "PENDING_FACTORY";
        partner.TerminationRequestedBy = ownerId;
        partner.TerminationRequestedAt = DateTime.UtcNow;
        partner.TerminationReason = reason;
        partner.TerminatedAt = null;
        partner.UpdatedAt = DateTime.UtcNow;
        await repository.SaveAsync(ct);
        await tx.CommitAsync();
        return View(partner);
    }

    public async Task<DepotPartnershipStatusDto> DecideTerminationAsync(Guid ownerId, Guid? depotId, Guid factoryId, DepotContractTerminationDecisionRequest request, CancellationToken ct)
    {
        var selected = await ResolveAsync(ownerId, depotId);
        var decision = request.Decision?.Trim().ToUpperInvariant();
        if (decision is not ("ACCEPT" or "REJECT")) throw new ArgumentException("Quyết định chỉ nhận ACCEPT hoặc REJECT.");
        await using var tx = await work.BeginAsync();
        await repository.LockDepotAsync(selected, ct);
        var partner = await repository.FindAsync(selected, factoryId, ct) ?? throw new KeyNotFoundException("Không tìm thấy quan hệ hợp tác.");
        if (partner.TerminationStatus != "PENDING_DEPOT") throw new DepotConflictException("Không có đề nghị chấm dứt từ nhà máy đang chờ chủ vựa xử lý.");
        if (decision == "ACCEPT" && await repository.HasOpenOrdersAsync(selected, factoryId, ct)) throw new DepotConflictException("Cần hoàn tất các lô đang xử lý trước khi chấp thuận chấm dứt.");
        if (decision == "ACCEPT")
        {
            partner.TerminationStatus = "TERMINATED";
            partner.TerminatedAt = DateTime.UtcNow;
            partner.ContractStatus = "TERMINATED";
            partner.Status = "DECLINED";
        }
        else partner.TerminationStatus = "REJECTED";
        partner.UpdatedAt = DateTime.UtcNow;
        await repository.SaveAsync(ct);
        await tx.CommitAsync();
        return View(partner);
    }

    private static DepotPartnershipStatusDto View(Retrack.API.Models.FactoryDepotPartnership partner) =>
        new(partner.Id, partner.FactoryId, partner.DepotId, partner.EffectiveStatus, partner.ContractCode, partner.ContractStatus, partner.TerminationStatus);
}
