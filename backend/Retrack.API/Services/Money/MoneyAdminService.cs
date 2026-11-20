using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Money;
using Retrack.API.Models;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Money;

/// <summary>Vai trò có thể nhận tiền từ sàn.</summary>
public static class PayeeRoles
{
    public static readonly string[] All = ["SELLER", "DEPOT_OWNER", "FACTORY"];
}

/// <summary>Truy vấn và cấu hình cho màn hình Thu chi của Admin. Logic chi tiền nằm ở <see cref="PayoutEngine"/>.</summary>
public sealed class MoneyAdminService(AppDbContext db, MoneyConfigReader configReader, IPayoutProviderResolver providers, IOptions<PayOsPayoutOptions> payoutOptions, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public static (int Page, int PageSize) Paging(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100));

    // ============================ CẤU HÌNH ============================

    public async Task<MoneySettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var cfg = await configReader.GetAsync(ct);
        return new(cfg.Mode, cfg.DefaultCommission.ToDictionary(k => k.Key, k => k.Value), cfg.PayoutAutoEnabled, cfg.PayoutSchedule, cfg.PayoutHoldMinutes,
            cfg.PayoutMinAmount, cfg.PayoutAutoMaxAmount, cfg.PayoutProvider, cfg.PayoutTestEnabled, cfg.PayoutTestMaxAmount, payoutOptions.Value.IsConfigured, cfg.PayoutScheduleHour, cfg.PayoutScheduleWeekday);
    }

    public async Task<MoneySettingsDto> UpdateSettingsAsync(UpdateMoneySettingsDto dto, Guid actorId, CancellationToken ct = default)
    {
        var before = await GetSettingsAsync(ct);
        var changes = new Dictionary<string, string>();
        if (dto.Mode != null)
        {
            var mode = dto.Mode.Trim().ToUpperInvariant();
            if (mode is not (MoneyConfigKeys.ModeLegacy or MoneyConfigKeys.ModePlatform)) throw new ArgumentException("Chế độ phải là LEGACY hoặc PLATFORM.");
            changes[MoneyConfigKeys.Mode] = mode;
        }
        if (dto.DefaultCommission != null)
            foreach (var (flow, rate) in dto.DefaultCommission)
            {
                if (!MoneyFlows.CommissionFlows.Contains(flow)) throw new ArgumentException($"Dòng tiền không hợp lệ: {flow}. Chỉ nhận PICKUP, BATCH, LIQUIDATION.");
                changes[MoneyConfigKeys.CommissionDefaultKey(flow)] = Rate(rate).ToString(CultureInfo.InvariantCulture);
            }
        if (dto.PayoutAutoEnabled != null) changes[MoneyConfigKeys.PayoutAutoEnabled] = dto.PayoutAutoEnabled.Value ? "true" : "false";
        if (dto.PayoutSchedule != null)
        {
            var s = dto.PayoutSchedule.Trim().ToUpperInvariant();
            if (s is not ("DAILY" or "WEEKLY")) throw new ArgumentException("Lịch chi phải là DAILY hoặc WEEKLY.");
            changes[MoneyConfigKeys.PayoutSchedule] = s;
        }
        if (dto.PayoutScheduleHour != null)
        {
            if (dto.PayoutScheduleHour is < 0 or > 23) throw new ArgumentException("Giờ chạy chi tự động phải từ 0 đến 23.");
            changes[MoneyConfigKeys.PayoutScheduleHour] = dto.PayoutScheduleHour.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (dto.PayoutScheduleWeekday != null)
        {
            if (dto.PayoutScheduleWeekday is < 1 or > 7) throw new ArgumentException("Ngày chạy hằng tuần phải từ 1 (thứ Hai) đến 7 (Chủ nhật).");
            changes[MoneyConfigKeys.PayoutScheduleWeekday] = dto.PayoutScheduleWeekday.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (dto.PayoutHoldMinutes != null)
        {
            if (dto.PayoutHoldMinutes is < 0 or > 525600) throw new ArgumentException("Thời gian giữ tiền phải từ 0 đến 525600 phút.");
            changes[MoneyConfigKeys.PayoutHoldMinutes] = dto.PayoutHoldMinutes.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (dto.PayoutMinAmount != null) changes[MoneyConfigKeys.PayoutMinAmount] = Whole(dto.PayoutMinAmount.Value, "Số chi tối thiểu", 0, 1_000_000_000m);
        if (dto.PayoutAutoMaxAmount != null) changes[MoneyConfigKeys.PayoutAutoMaxAmount] = Whole(dto.PayoutAutoMaxAmount.Value, "Hạn mức tự chi", 0, 100_000_000_000m);
        if (dto.PayoutMinAmount != null && dto.PayoutAutoMaxAmount != null && dto.PayoutAutoMaxAmount < dto.PayoutMinAmount)
            throw new ArgumentException("Hạn mức tự chi phải lớn hơn hoặc bằng số chi tối thiểu.");
        if (dto.PayoutProvider != null)
        {
            var p = dto.PayoutProvider.Trim().ToUpperInvariant();
            if (p is not ("SIMULATED" or "PAYOS")) throw new ArgumentException("Nhà cung cấp chi phải là SIMULATED hoặc PAYOS.");
            if (p == "PAYOS" && !payoutOptions.Value.IsConfigured)
                throw new InvalidOperationException("Chưa cấu hình khóa kênh chi payOS (PayOSPayout__ClientId, __ApiKey, __ChecksumKey) nên không thể chọn PAYOS.");
            changes[MoneyConfigKeys.PayoutProvider] = p;
        }
        if (dto.PayoutTestEnabled != null) changes[MoneyConfigKeys.PayoutTestEnabled] = dto.PayoutTestEnabled.Value ? "true" : "false";
        if (dto.PayoutTestMaxAmount != null) changes[MoneyConfigKeys.PayoutTestMaxAmount] = Whole(dto.PayoutTestMaxAmount.Value, "Số tiền chuyển thử tối đa", 1000, 100_000);
        if (changes.Count == 0) return before;

        var now = Now;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var (key, value) in changes)
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO system_configs (config_key, config_value, updated_at) VALUES ({key}, {value}, {now})
                ON CONFLICT (config_key) DO UPDATE SET config_value = EXCLUDED.config_value, updated_at = EXCLUDED.updated_at", ct);
        var after = await GetSettingsAsync(ct);
        db.AuditLogs.Add(new() { UserId = actorId, Action = "MONEY_SETTINGS_UPDATED", EntityName = "system_configs", OldData = JsonSerializer.Serialize(before, Json),
            NewData = JsonSerializer.Serialize(after, Json), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return after;
    }

    private static decimal Rate(decimal r) => r is >= 0 and <= 100 ? decimal.Round(r, 4) : throw new ArgumentException("Tỷ lệ hoa hồng phải từ 0 đến 100 (%).");

    private static string Whole(decimal v, string label, decimal min, decimal max)
    {
        if (v != decimal.Truncate(v) || v < min || v > max)
            throw new ArgumentException($"{label} phải là số nguyên đồng từ {MoneyFormat.Vnd(min)} đến {MoneyFormat.Vnd(max)}.");
        return v.ToString(CultureInfo.InvariantCulture);
    }

    // ============================ HOA HỒNG ============================

    public async Task<CommissionsDto> GetCommissionsAsync(Guid? userId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var cfg = await configReader.GetAsync(ct);
        var q = db.PayeeCommissions.AsNoTracking().Where(c => userId == null || c.UserId == userId);
        var total = await q.CountAsync(ct);
        var joined = await q.Join(db.Users.AsNoTracking(), c => c.UserId, u => u.Id, (c, u) => new { u.Id, u.FullName, u.Role, c.Flow, c.Rate, c.UpdatedAt })
            .OrderBy(x => x.FullName).ThenBy(x => x.Id).ThenBy(x => x.Flow).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var rows = joined.Select(x => new CommissionOverrideDto(x.Id, x.FullName, x.Role, x.Flow, x.Rate, x.UpdatedAt)).ToList();
        return new(cfg.DefaultCommission.ToDictionary(k => k.Key, k => k.Value), new PagedResult<CommissionOverrideDto> { Items = rows, TotalCount = total, Page = page, PageSize = pageSize });
    }

    public async Task<MoneySettingsDto> SetDefaultCommissionAsync(SetCommissionDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (dto.Rate == null) throw new ArgumentException("Cần nhập tỷ lệ hoa hồng mặc định.");
        return await UpdateSettingsAsync(new() { DefaultCommission = new() { [dto.Flow ?? ""] = dto.Rate.Value } }, actorId, ct);
    }

    /// <summary>Đặt hoặc xóa (Rate null) hoa hồng riêng của người nhận cho một dòng tiền. Chỉ ảnh hưởng dòng sổ cái tạo SAU đó.</summary>
    public async Task<CommissionOverrideDto?> SetPayeeCommissionAsync(Guid userId, SetCommissionDto dto, Guid actorId, CancellationToken ct = default)
    {
        if (!MoneyFlows.CommissionFlows.Contains(dto.Flow)) throw new ArgumentException("Dòng tiền không hợp lệ. Chỉ nhận PICKUP, BATCH, LIQUIDATION.");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (!PayeeRoles.All.Contains(user.Role)) throw new ArgumentException("Chỉ đặt hoa hồng riêng cho Seller, Chủ kho hoặc Factory.");
        var old = await db.PayeeCommissions.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId && c.Flow == dto.Flow, ct);
        var now = Now;
        if (dto.Rate == null)
        {
            await db.PayeeCommissions.Where(c => c.UserId == userId && c.Flow == dto.Flow).ExecuteDeleteAsync(ct);
            db.AuditLogs.Add(new() { UserId = actorId, Action = "PAYEE_COMMISSION_CLEARED", EntityName = "payee_commissions", EntityId = userId, OldData = old?.Rate.ToString(CultureInfo.InvariantCulture), NewData = dto.Flow, CreatedAt = now });
            await db.SaveChangesAsync(ct);
            return null;
        }
        var rate = Rate(dto.Rate.Value);
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO payee_commissions (user_id, flow, rate, updated_by, updated_at) VALUES ({userId}, {dto.Flow}, {rate}, {actorId}, {now})
            ON CONFLICT (user_id, flow) DO UPDATE SET rate = EXCLUDED.rate, updated_by = EXCLUDED.updated_by, updated_at = EXCLUDED.updated_at", ct);
        db.AuditLogs.Add(new() { UserId = actorId, Action = "PAYEE_COMMISSION_SET", EntityName = "payee_commissions", EntityId = userId, OldData = old?.Rate.ToString(CultureInfo.InvariantCulture),
            NewData = $"{dto.Flow}={rate.ToString(CultureInfo.InvariantCulture)}", CreatedAt = now });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return new(user.Id, user.FullName, user.Role, dto.Flow, rate, now);
    }

    // ============================ KỲ CHI ============================

    public async Task<PagedResult<PayoutRunListDto>> ListPayoutsAsync(string? status, Guid? payeeId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        var q = db.PayoutRuns.AsNoTracking().AsQueryable();
        if (status != null) q = q.Where(r => r.Status == status);
        if (payeeId != null) q = q.Where(r => r.PayeeId == payeeId);
        if (from != null) q = q.Where(r => r.CreatedAt >= PayoutRules.Utc(from.Value));
        if (to != null) q = q.Where(r => r.CreatedAt < PayoutRules.Utc(to.Value));
        var total = await q.CountAsync(ct);
        var runs = await q.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await NamesAsync(runs.Select(r => r.PayeeId), ct);
        return new() { Items = runs.Select(r => ToList(r, names)).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PayoutRunDetailDto> GetPayoutAsync(Guid id, CancellationToken ct = default)
    {
        var run = await db.PayoutRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException("Không tìm thấy kỳ chi.");
        var names = await NamesAsync([run.PayeeId], ct);
        var lines = await db.PayableLines.AsNoTracking().Where(l => l.PayoutId == id).OrderBy(l => l.AvailableAt).ThenBy(l => l.Id).Take(2000).ToListAsync(ct);
        PayoutBankView? bank = null;
        if (!string.IsNullOrWhiteSpace(run.BankSnapshot))
        {
            try
            {
                var s = JsonSerializer.Deserialize<PayoutEngine.BankSnapshot>(run.BankSnapshot, Json);
                if (s != null) bank = new(s.BankName, s.Bin, PayoutBankRules.Mask(s.Account), s.Holder);
            }
            catch (JsonException) { }
        }
        var key = run.Status is PayoutStatuses.Processing or PayoutStatuses.Unknown or PayoutStatuses.Failed or PayoutStatuses.Pending or PayoutStatuses.Paid
            ? PayoutRules.KeyFor(run.Code, run.Attempt) : null;
        return new(ToList(run, names), lines.Select(PayoutEngine.View).ToList(), bank, key);
    }

    private static PayoutRunListDto ToList(PayoutRun r, Dictionary<Guid, string> names) =>
        new(r.Id, r.Code, r.PayeeId, names.GetValueOrDefault(r.PayeeId, ""), PayoutRules.Utc(r.PeriodStart), PayoutRules.Utc(r.PeriodEnd), r.Gross, r.Commission, r.RefundAmount,
            r.CarriedDebt, r.Net, r.Status, r.Provider, r.ProviderRef, r.GatewayStatus, r.FailReason, r.Attempt, PayoutRules.Utc(r.CreatedAt),
            r.PaidAt == null ? null : PayoutRules.Utc(r.PaidAt.Value), r.ApprovedAt == null ? null : PayoutRules.Utc(r.ApprovedAt.Value),
            r.LastAttemptAt == null ? null : PayoutRules.Utc(r.LastAttemptAt.Value));

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var set = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    // ============================ SỐ DƯ ============================

    public async Task<ProviderBalance> GetBalanceAsync(CancellationToken ct = default)
    {
        var cfg = await configReader.GetAsync(ct);
        if (cfg.PayoutProvider == "PAYOS" && !payoutOptions.Value.IsConfigured)
            return new("PAYOS", false, null, "Chưa cấu hình khóa kênh chi payOS.");
        try
        {
            var p = providers.GetByName(cfg.PayoutProvider);
            return new(p.Name, p.IsConfigured, (await p.GetBalanceAsync(ct)).ToString("0", CultureInfo.InvariantCulture), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(cfg.PayoutProvider, true, null, "Không tra cứu được số dư tài khoản chi.");
        }
    }

    // ============================ THU VÀ SỔ CÁI ============================

    public async Task<PagedResult<AdminMoneyCollectionDto>> ListCollectionsAsync(string? status, string? flow, Guid? payeeId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var q = db.MoneyCollections.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) { var s = status.Trim().ToUpperInvariant(); q = q.Where(c => c.Status == s); }
        if (!string.IsNullOrWhiteSpace(flow)) { var f = flow.Trim().ToUpperInvariant(); q = q.Where(c => c.Flow == f); }
        if (payeeId != null) q = q.Where(c => c.PayeeId == payeeId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await NamesAsync(rows.SelectMany(c => new[] { c.PayerId, c.PayeeId }), ct);
        return new() { Items = rows.Select(c => new AdminMoneyCollectionDto(c.Id, c.Flow, c.SourceType, c.SourceId, c.PayerId, names.GetValueOrDefault(c.PayerId, ""), c.PayeeId,
            names.GetValueOrDefault(c.PayeeId, ""), c.Amount, c.Status, c.PaidAt, c.ReceivedAmount, c.CreatedAt)).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<PayableLineDto>> ListLedgerAsync(Guid? payeeId, string? flow, string? state, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var q = db.PayableLines.AsNoTracking().AsQueryable();
        if (payeeId != null) q = q.Where(l => l.PayeeId == payeeId);
        if (!string.IsNullOrWhiteSpace(flow)) { var f = flow.Trim().ToUpperInvariant(); q = q.Where(l => l.Flow == f); }
        if (!string.IsNullOrWhiteSpace(state)) { var s = state.Trim().ToUpperInvariant(); q = q.Where(l => l.State == s); }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(l => l.PayeeId), ct);
        return new() { Items = rows.Select(l => new PayableLineDto(l.Id, l.CollectionId, l.PayeeId, names.GetValueOrDefault(l.PayeeId, ""), l.Flow, l.SourceType, l.SourceId, l.Kind,
            l.RefundKey, l.Gross, l.CommissionRate, l.Commission, PayoutRules.LineNet(l.Kind, l.Gross, l.Commission), l.State, PayoutRules.Utc(l.AvailableAt), l.PayoutId, l.CreatedAt)).ToList(),
            TotalCount = total, Page = page, PageSize = pageSize };
    }

    // ============================ NGƯỜI NHẬN ============================

    /// <summary>Người dùng nhận tiền có dòng chưa chi hoặc đã nhập ngân hàng. Số tài khoản luôn được che.</summary>
    public async Task<PagedResult<PayeeSummaryDto>> ListPayeesAsync(string? search, string? bankStatus, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var roles = PayeeRoles.All;
        var bankFilter = string.IsNullOrWhiteSpace(bankStatus) ? null : bankStatus.Trim().ToUpperInvariant();
        if (bankFilter != null && bankFilter is not ("VERIFIED" or "PENDING_VERIFICATION" or "REJECTED" or "NONE"))
            throw new ArgumentException("Bộ lọc bankStatus phải là VERIFIED, PENDING_VERIFICATION, REJECTED hoặc NONE.");
        var q = db.Users.AsNoTracking().Where(u => roles.Contains(u.Role));
        if (bankFilter == "NONE") q = q.Where(u => u.BankAccountNumber == null);
        else if (bankFilter == "PENDING_VERIFICATION") q = q.Where(u => u.BankAccountNumber != null && (u.BankStatus == null || u.BankStatus == "PENDING_VERIFICATION"));
        else if (bankFilter != null) q = q.Where(u => u.BankAccountNumber != null && u.BankStatus == bankFilter);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(u => EF.Functions.ILike(u.FullName, like) || EF.Functions.ILike(u.Email, like));
        }
        var total = await q.CountAsync(ct);
        var users = await q.OrderBy(u => u.FullName).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();
        var sums = await db.PayableLines.AsNoTracking().Where(l => ids.Contains(l.PayeeId) && (l.State == PayableStates.Held || l.State == PayableStates.Available || l.State == PayableStates.InPayout))
            .GroupBy(l => new { l.PayeeId, l.State })
            .Select(g => new { g.Key.PayeeId, g.Key.State, Net = g.Sum(l => l.Kind == PayableKinds.Refund ? -(l.Gross - l.Commission) : l.Gross - l.Commission) }).ToListAsync(ct);
        var auto = await db.PayeePayoutSettings.AsNoTracking().Where(s => ids.Contains(s.UserId)).ToDictionaryAsync(s => s.UserId, s => s.AutoEnabled, ct);
        decimal Sum(Guid id, string state) => sums.Where(s => s.PayeeId == id && s.State == state).Sum(s => s.Net);
        return new() { Items = users.Select(u => new PayeeSummaryDto(u.Id, u.FullName, u.Role, u.IsActive, Sum(u.Id, PayableStates.Held), Sum(u.Id, PayableStates.Available),
            Sum(u.Id, PayableStates.InPayout), auto.GetValueOrDefault(u.Id, true), BankDto(u))).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    /// <summary>Trạng thái hiển thị: NONE khi chưa nhập tài khoản; còn lại là bank_status (PENDING_VERIFICATION, VERIFIED, REJECTED).</summary>
    public static string BankStatusOf(User u) => string.IsNullOrWhiteSpace(u.BankAccountNumber) ? "NONE" : u.BankStatus ?? "PENDING_VERIFICATION";

    public static PayeeBankDto BankDto(User u) =>
        new(u.BankName, u.BankBin, PayoutBankRules.Mask(u.BankAccountNumber), u.BankAccountName, BankStatusOf(u),
            u.BankVerifiedAt == null ? null : PayoutRules.Utc(u.BankVerifiedAt.Value), u.BankChangedAt == null ? null : PayoutRules.Utc(u.BankChangedAt.Value),
            u.BankStatus == "REJECTED" ? u.BankRejectReason : null);

    /// <summary>Số người nhận tiền đã nhập đủ tài khoản ngân hàng và đang chờ Admin duyệt (huy hiệu trên màn hình Admin).</summary>
    public async Task<int> CountPendingBankAsync(CancellationToken ct = default)
    {
        var roles = PayeeRoles.All;
        return await db.Users.AsNoTracking().CountAsync(u => roles.Contains(u.Role) && u.IsActive && u.BankAccountNumber != null && (u.BankStatus == null || u.BankStatus == "PENDING_VERIFICATION"), ct);
    }

    /// <summary>Chi tiết ngân hàng cho Admin đối chiếu trước khi duyệt/từ chối (có số tài khoản đầy đủ; chỉ Admin gọi). NameMatchesFullName chỉ là gợi ý.</summary>
    public async Task<PayeeBankAdminDto> GetPayeeBankAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await LoadPayeeAsync(userId, ct);
        return new(u.Id, u.FullName, u.BankName, u.BankBin, u.BankAccountNumber, u.BankAccountName, BankStatusOf(u),
            u.BankVerifiedAt, u.BankChangedAt, PayoutBankRules.NamesMatch(u.BankAccountName, u.FullName), u.BankStatus == "REJECTED" ? u.BankRejectReason : null);
    }

    private async Task<User> LoadBankForReviewAsync(Guid userId, string expectedAccountNumber, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (!PayeeRoles.All.Contains(u.Role)) throw new ArgumentException("Chỉ duyệt tài khoản của Seller, Chủ kho hoặc Factory.");
        if (!PayoutBankRules.IsValidBin(u.BankBin) || !PayoutBankRules.IsValidAccountNumber(u.BankAccountNumber) || string.IsNullOrWhiteSpace(u.BankAccountName))
            throw new InvalidOperationException("Người dùng chưa nhập đủ thông tin ngân hàng (mã BIN, số tài khoản, tên chủ tài khoản).");
        if (string.IsNullOrWhiteSpace(expectedAccountNumber) || !string.Equals(u.BankAccountNumber, expectedAccountNumber.Trim(), StringComparison.Ordinal))
            throw new DepotConflictException("Số tài khoản đã thay đổi so với lúc bạn xem. Hãy tải lại và đối chiếu lại trước khi duyệt hoặc từ chối.");
        return u;
    }

    /// <summary>
    /// Admin duyệt tài khoản ngân hàng. expectedAccountNumber là số tài khoản Admin đã xem: nếu người dùng vừa đổi thì 409,
    /// tránh duyệt nhầm một tài khoản khác với tài khoản đã đối chiếu. Thông báo cho người dùng. Việc giải phóng các kỳ chi đang HELD do chưa duyệt
    /// được làm ngay sau đó bởi <see cref="PayoutEngine.ReleaseHeldForPayeeAsync"/> (ngoài transaction này, vì có thể gọi nhà cung cấp chi).
    /// </summary>
    public async Task<PayeeBankDto> VerifyBankAsync(Guid userId, string expectedAccountNumber, Guid actorId, CancellationToken ct = default)
    {
        var u = await LoadBankForReviewAsync(userId, expectedAccountNumber, ct);
        var now = Now;
        var was = u.BankStatus;
        u.BankStatus = "VERIFIED";
        u.BankVerifiedAt = now;
        u.BankRejectReason = null;
        db.AuditLogs.Add(new() { UserId = actorId, Action = "PAYEE_BANK_VERIFIED", EntityName = "users", EntityId = u.Id,
            OldData = JsonSerializer.Serialize(new { status = was }, Json), NewData = JsonSerializer.Serialize(new { bin = u.BankBin, account = PayoutBankRules.Mask(u.BankAccountNumber) }, Json), CreatedAt = now });
        if (was != "VERIFIED")
            db.Notifications.Add(new() { UserId = u.Id, Category = "PAYOUT", TargetType = "BANK_ACCOUNT", TargetId = u.Id, Title = "Tài khoản ngân hàng đã được duyệt",
                Message = $"Tài khoản ngân hàng {PayoutBankRules.Mask(u.BankAccountNumber)} của bạn đã được Admin duyệt. Sàn có thể chi và hoàn tiền vào tài khoản này.", CreatedAt = now });
        await db.SaveChangesAsync(ct);
        var dto = BankDto(u);
        db.ChangeTracker.Clear();
        return dto;
    }

    /// <summary>Admin từ chối tài khoản ngân hàng kèm lý do 5-255 ký tự: bank_status = REJECTED, người dùng được thông báo và phải nhập lại.</summary>
    public async Task<PayeeBankDto> RejectBankAsync(Guid userId, string expectedAccountNumber, string? reason, Guid actorId, CancellationToken ct = default)
    {
        var text = (reason ?? "").Trim();
        if (text.Length is < 5 or > 255) throw new ArgumentException("Lý do từ chối phải từ 5 đến 255 ký tự.");
        var u = await LoadBankForReviewAsync(userId, expectedAccountNumber, ct);
        var now = Now;
        var was = u.BankStatus;
        u.BankStatus = "REJECTED";
        u.BankVerifiedAt = null;
        u.BankRejectReason = text;
        db.AuditLogs.Add(new() { UserId = actorId, Action = "PAYEE_BANK_REJECTED", EntityName = "users", EntityId = u.Id,
            OldData = JsonSerializer.Serialize(new { status = was }, Json), NewData = JsonSerializer.Serialize(new { bin = u.BankBin, account = PayoutBankRules.Mask(u.BankAccountNumber), reason = text }, Json), CreatedAt = now });
        db.Notifications.Add(new() { UserId = u.Id, Category = "PAYOUT", TargetType = "BANK_ACCOUNT", TargetId = u.Id, Title = "Tài khoản ngân hàng bị từ chối",
            Message = $"Tài khoản ngân hàng {PayoutBankRules.Mask(u.BankAccountNumber)} chưa được duyệt. Lý do: {text}. Hãy nhập lại tài khoản đúng để nhận tiền.", CreatedAt = now });
        await db.SaveChangesAsync(ct);
        var dto = BankDto(u);
        db.ChangeTracker.Clear();
        return dto;
    }

    public async Task<bool> SetAutoPayoutAsync(Guid userId, bool enabled, Guid actorId, CancellationToken ct = default)
    {
        await LoadPayeeAsync(userId, ct);
        var now = Now;
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO payee_payout_settings (user_id, auto_enabled, updated_at) VALUES ({userId}, {enabled}, {now})
            ON CONFLICT (user_id) DO UPDATE SET auto_enabled = EXCLUDED.auto_enabled, updated_at = EXCLUDED.updated_at", ct);
        db.AuditLogs.Add(new() { UserId = actorId, Action = "PAYEE_AUTO_PAYOUT_SET", EntityName = "payee_payout_settings", EntityId = userId, NewData = enabled ? "true" : "false", CreatedAt = now });
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return enabled;
    }

    private async Task<User> LoadPayeeAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (!PayeeRoles.All.Contains(u.Role)) throw new ArgumentException("Người dùng này không phải người nhận tiền (Seller, Chủ kho, Factory).");
        return u;
    }

    // ============================ BẤT THƯỜNG THU TIỀN ============================

    public async Task<PagedResult<AdminAnomalyDto>> ListAnomaliesAsync(string? status, string? kind, string? flow, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var q = from a in db.MoneyCollectionAnomalies.AsNoTracking() join c in db.MoneyCollections.AsNoTracking() on a.CollectionId equals c.Id select new { a, c };
        if (!string.IsNullOrWhiteSpace(status)) { var v = status.Trim().ToUpperInvariant(); q = q.Where(x => x.a.Status == v); }
        if (!string.IsNullOrWhiteSpace(kind)) { var v = kind.Trim().ToUpperInvariant(); q = q.Where(x => x.a.Kind == v); }
        if (!string.IsNullOrWhiteSpace(flow)) { var v = flow.Trim().ToUpperInvariant(); q = q.Where(x => x.c.Flow == v); }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.a.CreatedAt).ThenBy(x => x.a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new() { Items = await ToAnomalyDtosAsync(rows.Select(x => (x.a, x.c)).ToList(), ct), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<AdminAnomalyDto> GetAnomalyAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.MoneyCollectionAnomalies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Không tìm thấy bất thường thu tiền.");
        var c = await db.MoneyCollections.AsNoTracking().SingleAsync(x => x.Id == a.CollectionId, ct);
        return (await ToAnomalyDtosAsync([(a, c)], ct)).Single();
    }

    private async Task<List<AdminAnomalyDto>> ToAnomalyDtosAsync(List<(MoneyCollectionAnomaly A, MoneyCollection C)> rows, CancellationToken ct)
    {
        var userIds = rows.SelectMany(r => new[] { r.C.PayerId, r.C.PayeeId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var anomalyIds = rows.Select(r => (Guid?)r.A.Id).ToList();
        var refunds = await db.MoneyRefunds.AsNoTracking().Where(r => anomalyIds.Contains(r.AnomalyId) && r.Status != RefundStatuses.Cancelled).ToListAsync(ct);
        string Name(Guid id) => users.TryGetValue(id, out var u) ? u.FullName : "";
        return rows.Select(r =>
        {
            var latest = refunds.Where(x => x.AnomalyId == r.A.Id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefault();
            var payer = users.GetValueOrDefault(r.C.PayerId);
            var refundable = r.A.Status == "OPEN" && latest == null ? r.A.RefundAmount : 0m;
            return new AdminAnomalyDto(r.A.Id, r.A.Kind, r.A.Status, r.A.ReceivedAmount, r.A.ExpectedAmount, r.A.RefundAmount, refundable, latest != null, latest?.Id, latest?.Code, latest?.Status,
                r.A.Reference, r.A.OrderCode, r.A.Note, r.A.CreatedAt, r.A.ResolvedAt, r.A.ResolvedBy, new(r.C.Id, r.C.Flow, r.C.SourceId, r.C.Amount, r.C.Status),
                new(r.C.PayerId, Name(r.C.PayerId)), new(r.C.PayeeId, Name(r.C.PayeeId)),
                payer == null ? new("NONE", null, null) : new(BankStatusOf(payer), payer.BankName, PayoutBankRules.Mask(payer.BankAccountNumber)));
        }).ToList();
    }

    public async Task<AnomalySummaryDto> GetAnomalySummaryAsync(CancellationToken ct = default) =>
        new(await db.MoneyCollectionAnomalies.AsNoTracking().CountAsync(a => a.Status == "OPEN", ct),
            await db.MoneyRefunds.AsNoTracking().CountAsync(r => r.Status == RefundStatuses.AwaitingApproval, ct));

    /// <summary>Đóng một bất thường OPEN mà không hoàn tiền (Admin ghi chú 5-500 ký tự, có audit). Không đóng được khi đã có lệnh hoàn chưa hủy.</summary>
    public async Task<AdminAnomalyDto> ResolveAnomalyAsync(Guid id, string? note, Guid actorId, CancellationToken ct = default)
    {
        var text = (note ?? "").Trim();
        if (text.Length is < 5 or > 500) throw new ArgumentException("Ghi chú xử lý phải từ 5 đến 500 ký tự.");
        var collectionId = await db.MoneyCollectionAnomalies.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.CollectionId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy bất thường thu tiền.");
        var now = Now;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Cùng thứ tự khóa với lúc tạo lệnh hoàn: khoản thu → bất thường → lệnh hoàn.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM money_collections WHERE id = {collectionId} FOR UPDATE", ct);
        var a = await db.MoneyCollectionAnomalies.FromSqlInterpolated($"SELECT * FROM money_collection_anomalies WHERE id = {id} FOR UPDATE").AsTracking().SingleAsync(ct);
        if (a.Status != "OPEN") throw new DepotConflictException("Bất thường này đã được xử lý.");
        var refund = await db.MoneyRefunds.AsNoTracking().Where(r => r.AnomalyId == id && r.Status != RefundStatuses.Cancelled).Select(r => r.Code).FirstOrDefaultAsync(ct);
        if (refund != null) throw new DepotConflictException($"Bất thường này đã có lệnh hoàn {refund}. Hãy hoàn tất hoặc hủy lệnh đó thay vì đóng.");
        var oldNote = a.Note;
        a.Status = "RESOLVED"; a.ResolvedAt = now; a.ResolvedBy = actorId;
        a.Note = string.IsNullOrWhiteSpace(oldNote) ? text : oldNote + " | " + text;
        db.AuditLogs.Add(new() { UserId = actorId, Action = "ANOMALY_RESOLVED", EntityName = "money_collection_anomalies", EntityId = id,
            OldData = JsonSerializer.Serialize(new { status = "OPEN", a.Kind, a.RefundAmount }, Json), NewData = JsonSerializer.Serialize(new { status = "RESOLVED", note = text, refunded = false }, Json), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await GetAnomalyAsync(id, ct);
    }

    // ============================ LỆNH HOÀN TIỀN ============================

    public async Task<PagedResult<AdminRefundDto>> ListRefundsAsync(string? status, Guid? payerId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var q = db.MoneyRefunds.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) { var v = status.Trim().ToUpperInvariant(); q = q.Where(r => r.Status == v); }
        if (payerId != null) q = q.Where(r => r.PayerId == payerId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(r => r.PayerId), ct);
        return new() { Items = rows.Select(r => ToRefundDto(r, names)).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<AdminRefundDetailDto> GetRefundAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.MoneyRefunds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Không tìm thấy lệnh hoàn.");
        var names = await NamesAsync([r.PayerId], ct);
        var c = await db.MoneyCollections.AsNoTracking().SingleAsync(x => x.Id == r.CollectionId, ct);
        PayoutBankView? bank = null;
        if (!string.IsNullOrWhiteSpace(r.BankSnapshot))
        {
            try
            {
                var s = JsonSerializer.Deserialize<PayoutEngine.BankSnapshot>(r.BankSnapshot, Json);
                if (s != null) bank = new(s.BankName, s.Bin, PayoutBankRules.Mask(s.Account), s.Holder);
            }
            catch (JsonException) { }
        }
        var key = r.Status is RefundStatuses.Processing or RefundStatuses.Unknown or RefundStatuses.Failed or RefundStatuses.Pending or RefundStatuses.Paid ? PayoutRules.KeyFor(r.Code, r.Attempt) : null;
        var anomalyKind = r.AnomalyId == null ? null : await db.MoneyCollectionAnomalies.AsNoTracking().Where(a => a.Id == r.AnomalyId).Select(a => a.Kind).FirstOrDefaultAsync(ct);
        var ledger = await db.PayableLines.AsNoTracking().Where(l => l.SourceType == c.SourceType && l.SourceId == c.SourceId && l.Kind == PayableKinds.Refund && l.RefundKey == r.Code).Select(l => l.State).FirstOrDefaultAsync(ct);
        return new(ToRefundDto(r, names), bank, key, new(c.Id, c.Flow, c.SourceId, c.Amount, c.Status), anomalyKind, ledger);
    }

    private static AdminRefundDto ToRefundDto(MoneyRefund r, Dictionary<Guid, string> names) =>
        new(r.Id, r.Code, r.CollectionId, r.AnomalyId, r.PayerId, names.GetValueOrDefault(r.PayerId, ""), r.Kind, r.Amount, r.Reason, r.Status, r.Provider, r.ProviderRef, r.GatewayStatus,
            r.FailReason, r.Attempt, r.CreatedBy, r.ApprovedBy, r.ApprovedAt == null ? null : PayoutRules.Utc(r.ApprovedAt.Value), PayoutRules.Utc(r.CreatedAt),
            r.LastAttemptAt == null ? null : PayoutRules.Utc(r.LastAttemptAt.Value), r.PaidAt == null ? null : PayoutRules.Utc(r.PaidAt.Value));
}
