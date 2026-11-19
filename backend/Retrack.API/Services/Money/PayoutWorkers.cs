using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Retrack.API.Data;
using Retrack.API.Models;

namespace Retrack.API.Services.Money;

/// <summary>
/// Chi tự động theo lịch (payout.schedule: DAILY 02:00 hoặc WEEKLY thứ Hai 02:00, giờ Việt Nam). Mốc đã xử lý được lưu trong
/// system_configs (payout.auto_last_boundary) nên khởi động lại không chạy trùng; chạy lại cũng idempotent theo từng người nhận.
/// </summary>
public sealed class AutoPayoutWorker(IServiceScopeFactory scopes, IOptions<PayoutWorkerOptions> options, TimeProvider clock, ILogger<AutoPayoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(options.Value.AutoInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "Chi tự động thất bại, sẽ thử lại ở lượt sau."); }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Một lượt kiểm tra lịch. Trả số kỳ chi mới tạo (0 nếu chưa tới hạn, đã xử lý mốc này, hoặc chi tự động đang tắt).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = await scope.ServiceProvider.GetRequiredService<MoneyConfigReader>().GetAsync(ct);
        if (!cfg.PayoutAutoEnabled) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var boundary = PayoutRules.ScheduleBoundaryUtc(now, cfg.PayoutSchedule);
        var marker = await db.SystemConfigs.AsNoTracking().Where(c => c.ConfigKey == PayoutConfigKeys.AutoLastBoundary).Select(c => c.ConfigValue).FirstOrDefaultAsync(ct);
        if (DateTime.TryParse(marker, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var last) && last >= boundary) return 0;

        // Đầu kỳ: period_end gần nhất trong mọi kỳ chi; chưa có thì lùi 1 ngày. Dòng cũ chưa chi vẫn được dồn vào kỳ này.
        var latestEnd = await db.PayoutRuns.AsNoTracking().MaxAsync(r => (DateTime?)r.PeriodEnd, ct);
        var fallbackStart = latestEnd != null ? PayoutRules.Utc(latestEnd.Value) : boundary.AddDays(-1);
        var created = 0;
        if (fallbackStart < boundary)
        {
            var results = await scope.ServiceProvider.GetRequiredService<PayoutEngine>().RunAutoAsync(boundary, fallbackStart, ct);
            created = results.Count(r => r.Outcome == "CREATED");
            logger.LogInformation("Chi tự động mốc {Boundary:o}: {Created} kỳ chi mới.", boundary, created);
        }
        var boundaryText = boundary.ToString("o", CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO system_configs (config_key, config_value, description, updated_at)
            VALUES ({PayoutConfigKeys.AutoLastBoundary}, {boundaryText}, 'Mốc lịch tự chi gần nhất đã xử lý (tự ghi)', {now})
            ON CONFLICT (config_key) DO UPDATE SET config_value = EXCLUDED.config_value, updated_at = EXCLUDED.updated_at", ct);
        return created;
    }
}

/// <summary>Định kỳ hỏi nhà cung cấp về kỳ PROCESSING/UNKNOWN (cũ hơn ReconcileMinAge, tối đa ReconcileBatchSize mỗi lượt, cũ nhất trước).</summary>
public sealed class PayoutReconcileWorker(IServiceScopeFactory scopes, IOptions<PayoutWorkerOptions> options, ILogger<PayoutReconcileWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(options.Value.ReconcileInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning(ex, "Đối soát kỳ chi thất bại, sẽ thử lại ở lượt sau."); }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Một lượt đối soát. Trả số kỳ đã chạm tới (không phân biệt kết quả).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        List<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
            ids = await scope.ServiceProvider.GetRequiredService<PayoutEngine>().DueForReconcileAsync(options.Value.ReconcileMinAge, options.Value.ReconcileBatchSize, ct);
        var touched = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopes.CreateAsyncScope(); // mỗi kỳ một DbContext riêng
                await scope.ServiceProvider.GetRequiredService<PayoutEngine>().ReconcileAsync(id, null, ct);
                touched++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Không đối soát được kỳ chi {RunId}.", id); }
        }
        return touched;
    }
}
