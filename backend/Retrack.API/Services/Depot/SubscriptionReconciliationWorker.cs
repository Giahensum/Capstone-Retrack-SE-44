using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

// Webhook có thể bị mất khi mất mạng; kiểm tra lại từng đơn bằng provider, không tin URL phía trình duyệt.
public sealed class SubscriptionReconciliationWorker(IServiceScopeFactory scopes, ILogger<SubscriptionReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                List<Guid> ids;
                using (var scope = scopes.CreateScope())
                {
                    if (!scope.ServiceProvider.GetRequiredService<IPayOsClient>().IsConfigured) continue;
                    ids = await scope.ServiceProvider.GetRequiredService<IDepotSubscriptionService>().PendingPayOsAsync();
                }
                foreach (var id in ids)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<IDepotSubscriptionService>().ReconcileAsync(id);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception e)
                    { logger.LogWarning("Chưa đối soát được đơn gói {OrderId}; sẽ thử lại. Loại lỗi: {ErrorType}", id, e.GetType().Name); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception e) { logger.LogWarning("Chưa đọc được đơn gói chờ; sẽ thử lại. Loại lỗi: {ErrorType}", e.GetType().Name); }
        }
    }
}
