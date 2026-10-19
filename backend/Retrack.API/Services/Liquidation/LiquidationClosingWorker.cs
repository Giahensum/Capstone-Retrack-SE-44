using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Liquidation;

public sealed class LiquidationClosingWorker(IServiceScopeFactory scopes, ILogger<LiquidationClosingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                List<Guid> expired;
                using (var scope = scopes.CreateScope())
                    expired = await scope.ServiceProvider.GetRequiredService<ILiquidationService>().ExpiredAsync();
                foreach (var id in expired)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    try
                    {
                        using var closing = scopes.CreateScope();
                        await closing.ServiceProvider.GetRequiredService<ILiquidationService>().FinalizeAsync(id);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception error) { logger.LogError(error, "Không chốt được phiên {LotId}; tiếp tục các phiên khác và thử lại sau.", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Chốt phiên thanh lý thất bại; sẽ thử lại ở lượt tiếp theo."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
