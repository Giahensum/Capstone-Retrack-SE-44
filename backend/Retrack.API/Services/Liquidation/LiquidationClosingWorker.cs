using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Liquidation;

/// <summary>Đóng phiên đến giờ: V1 (trọn lô) và V2 (nhiều hạng mục) dùng chung nhịp 30 giây, mỗi phiên một scope/transaction riêng.</summary>
public sealed class LiquidationClosingWorker(IServiceScopeFactory scopes, ILogger<LiquidationClosingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            await CloseAsync(sp => sp.GetRequiredService<ILiquidationService>().ExpiredAsync(),
                (sp, id) => sp.GetRequiredService<ILiquidationService>().FinalizeAsync(id), stoppingToken);
            await CloseAsync(sp => sp.GetRequiredService<ILiquidationV2Service>().ExpiredAsync(),
                (sp, id) => sp.GetRequiredService<ILiquidationV2Service>().FinalizeAsync(id), stoppingToken);
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CloseAsync(Func<IServiceProvider, Task<List<Guid>>> expired, Func<IServiceProvider, Guid, Task> finalize, CancellationToken stoppingToken)
    {
        try
        {
            List<Guid> ids;
            using (var scope = scopes.CreateScope()) ids = await expired(scope.ServiceProvider);
            foreach (var id in ids)
            {
                if (stoppingToken.IsCancellationRequested) return;
                try
                {
                    using var closing = scopes.CreateScope();
                    await finalize(closing.ServiceProvider, id);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogError(error, "Không chốt được phiên {LotId}; tiếp tục các phiên khác và thử lại sau.", id); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception error) { logger.LogError(error, "Chốt phiên thanh lý thất bại; sẽ thử lại ở lượt tiếp theo."); }
    }
}
