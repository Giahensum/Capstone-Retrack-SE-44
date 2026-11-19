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
        var hold = await BankHoldHoursAsync(ct);
        return new(cfg.Mode, cfg.DefaultCommission.ToDictionary(k => k.Key, k => k.Value), cfg.PayoutAutoEnabled, cfg.PayoutSchedule, cfg.PayoutHoldMinutes,
            cfg.PayoutMinAmount, cfg.PayoutAutoMaxAmount, hold, cfg.PayoutProvider, cfg.PayoutTestEnabled, cfg.PayoutTestMaxAmount, payoutOptions.Value.IsConfigured);
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
        if (dto.PayoutHoldMinutes != null)
        {
            if (dto.PayoutHoldMinutes is < 0 or > 525600) throw new ArgumentException("Thời gian giữ tiền phải từ 0 đến 525600 phút.");
            changes[MoneyConfigKeys.PayoutHoldMinutes] = dto.PayoutHoldMinutes.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (dto.PayoutMinAmount != null) changes[MoneyConfigKeys.PayoutMinAmount] = Whole(dto.PayoutMinAmount.Value, "Số chi tối thiểu", 0, 1_000_000_000m);
        if (dto.PayoutAutoMaxAmount != null) changes[MoneyConfigKeys.PayoutAutoMaxAmount] = Whole(dto.PayoutAutoMaxAmount.Value, "Hạn mức tự chi", 0, 100_000_000_000m);
        if (dto.PayoutBankChangeHoldHours != null)
        {
            if (dto.PayoutBankChangeHoldHours is < 0 or > 720) throw new ArgumentException("Thời gian giữ sau khi đổi ngân hàng phải từ 0 đến 720 giờ.");
            changes[PayoutConfigKeys.BankChangeHoldHours] = dto.PayoutBankChangeHoldHours.Value.ToString(CultureInfo.InvariantCulture);
        }
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
            throw new ArgumentException($"{label} phải là số nguyên đồng từ {min.ToString("N0", CultureInfo.InvariantCulture)} đến {max.ToString("N0", CultureInfo.InvariantCulture)}.");
        return v.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<int> BankHoldHoursAsync(CancellationToken ct)
    {
        var v = await db.SystemConfigs.AsNoTracking().Where(c => c.ConfigKey == PayoutConfigKeys.BankChangeHoldHours).Select(c => c.ConfigValue).FirstOrDefaultAsync(ct);
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h is >= 0 and <= 720 ? h : PayoutConfigKeys.DefaultBankChangeHoldHours;
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

    public async Task<PagedResult<MoneyCollectionDto>> ListCollectionsAsync(string? status, string? flow, Guid? payeeId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var q = db.MoneyCollections.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) { var s = status.Trim().ToUpperInvariant(); q = q.Where(c => c.Status == s); }
        if (!string.IsNullOrWhiteSpace(flow)) { var f = flow.Trim().ToUpperInvariant(); q = q.Where(c => c.Flow == f); }
        if (payeeId != null) q = q.Where(c => c.PayeeId == payeeId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await NamesAsync(rows.SelectMany(c => new[] { c.PayerId, c.PayeeId }), ct);
        return new() { Items = rows.Select(c => new MoneyCollectionDto(c.Id, c.Flow, c.SourceType, c.SourceId, c.PayerId, names.GetValueOrDefault(c.PayerId, ""), c.PayeeId,
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
    public async Task<PagedResult<PayeeSummaryDto>> ListPayeesAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging(page, pageSize);
        var roles = PayeeRoles.All;
        var q = db.Users.AsNoTracking().Where(u => roles.Contains(u.Role)
            && (u.BankAccountNumber != null || db.PayableLines.Any(l => l.PayeeId == u.Id && (l.State == PayableStates.Held || l.State == PayableStates.Available || l.State == PayableStates.InPayout))));
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
        var hold = await BankHoldHoursAsync(ct);
        decimal Sum(Guid id, string state) => sums.Where(s => s.PayeeId == id && s.State == state).Sum(s => s.Net);
        return new() { Items = users.Select(u => new PayeeSummaryDto(u.Id, u.FullName, u.Role, u.IsActive, Sum(u.Id, PayableStates.Held), Sum(u.Id, PayableStates.Available),
            Sum(u.Id, PayableStates.InPayout), auto.GetValueOrDefault(u.Id, true), BankDto(u, hold))).ToList(), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public static PayeeBankDto BankDto(User u, int holdHours)
    {
        DateTime? holdUntil = u.BankChangedAt != null && holdHours > 0 ? PayoutRules.Utc(u.BankChangedAt.Value).AddHours(holdHours) : null;
        return new(u.BankName, u.BankBin, PayoutBankRules.Mask(u.BankAccountNumber), u.BankAccountName, u.BankStatus ?? "PENDING_VERIFICATION",
            u.BankVerifiedAt == null ? null : PayoutRules.Utc(u.BankVerifiedAt.Value), u.BankChangedAt == null ? null : PayoutRules.Utc(u.BankChangedAt.Value), holdUntil);
    }

    /// <summary>Chi tiết ngân hàng cho Admin đối chiếu trước khi xác minh (có số tài khoản đầy đủ; chỉ Admin gọi).</summary>
    public async Task<PayeeBankAdminDto> GetPayeeBankAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await LoadPayeeAsync(userId, ct);
        return new(u.Id, u.FullName, u.BankName, u.BankBin, u.BankAccountNumber, u.BankAccountName, u.BankStatus ?? "PENDING_VERIFICATION",
            u.BankVerifiedAt, u.BankChangedAt, PayoutBankRules.NamesMatch(u.BankAccountName, u.FullName));
    }

    /// <summary>
    /// Admin xác minh tài khoản ngân hàng. expectedAccountNumber là số tài khoản Admin đã xem: nếu người nhận vừa đổi thì 409,
    /// tránh xác minh nhầm một tài khoản khác với tài khoản đã đối chiếu.
    /// </summary>
    public async Task<PayeeBankDto> VerifyBankAsync(Guid userId, string expectedAccountNumber, Guid actorId, CancellationToken ct = default)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (!PayeeRoles.All.Contains(u.Role)) throw new ArgumentException("Chỉ xác minh tài khoản của Seller, Chủ kho hoặc Factory.");
        if (!PayoutBankRules.IsValidBin(u.BankBin) || !PayoutBankRules.IsValidAccountNumber(u.BankAccountNumber) || string.IsNullOrWhiteSpace(u.BankAccountName))
            throw new InvalidOperationException("Người nhận chưa nhập đủ thông tin ngân hàng (mã BIN, số tài khoản, tên chủ tài khoản).");
        if (string.IsNullOrWhiteSpace(expectedAccountNumber) || !string.Equals(u.BankAccountNumber, expectedAccountNumber.Trim(), StringComparison.Ordinal))
            throw new DepotConflictException("Số tài khoản đã thay đổi so với lúc bạn xem. Hãy tải lại và đối chiếu lại trước khi xác minh.");
        var now = Now;
        u.BankStatus = "VERIFIED";
        u.BankVerifiedAt = now;
        db.AuditLogs.Add(new() { UserId = actorId, Action = "PAYEE_BANK_VERIFIED", EntityName = "users", EntityId = u.Id, NewData = JsonSerializer.Serialize(new { bin = u.BankBin, account = PayoutBankRules.Mask(u.BankAccountNumber) }, Json), CreatedAt = now });
        await db.SaveChangesAsync(ct);
        var dto = BankDto(u, await BankHoldHoursAsync(ct));
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
}
