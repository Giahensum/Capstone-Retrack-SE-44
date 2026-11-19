using Microsoft.Extensions.Options;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services.Money;

/// <summary>Định kỳ hỏi PayOS về các khoản thu PENDING đã có link (anh em của PayOsReconciliationWorker), để không bắt buộc đăng ký webhook.</summary>
public sealed class MoneyCollectionReconciliationWorker(IServiceScopeFactory scopes, IOptions<PayOsOptions> options, ILogger<MoneyCollectionReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 5, 600)));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Đối soát khoản thu PayOS thất bại, sẽ thử lại ở lượt sau."); }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // Chỉ chạy ở chế độ PLATFORM; LEGACY không có khoản thu nào cần hỏi PayOS.
            if (!(await scope.ServiceProvider.GetRequiredService<MoneyConfigReader>().GetAsync(ct)).IsPlatformMode) return;
            ids = await scope.ServiceProvider.GetRequiredService<IMoneyCollectionService>().PendingCollectionIdsAsync(50, ct);
        }
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopes.CreateAsyncScope(); // mỗi khoản thu một scope/DbContext riêng
                if (await scope.ServiceProvider.GetRequiredService<IMoneyCollectionService>().ReconcileAsync(id, ct))
                    logger.LogInformation("PayOS: khoản thu {CollectionId} đã thanh toán.", id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Không đối soát được khoản thu {CollectionId}.", id); }
        }
    }
}
