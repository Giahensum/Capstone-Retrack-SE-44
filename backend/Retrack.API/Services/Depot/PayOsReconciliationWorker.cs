using Microsoft.Extensions.Options;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Depot;

/// <summary>Định kỳ hỏi PayOS về các hóa đơn đã tạo link nhưng chưa thanh toán, nên không bắt buộc đăng ký webhook.</summary>
public sealed class PayOsReconciliationWorker(IServiceScopeFactory scopes, IOptions<PayOsOptions> options, ILogger<PayOsReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured) return; // chưa có key PayOS: không chạy
        var delay = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 5, 600));
        using var timer = new PeriodicTimer(delay);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Đối soát PayOS thất bại, sẽ thử lại ở lượt sau."); }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        List<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
            ids = await scope.ServiceProvider.GetRequiredService<IDepotPayOsService>().PendingInvoiceIdsAsync(50);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopes.CreateAsyncScope(); // mỗi hóa đơn một scope/DbContext riêng
                if (await scope.ServiceProvider.GetRequiredService<IDepotPayOsService>().ReconcileAsync(id))
                    logger.LogInformation("PayOS: hóa đơn {InvoiceId} đã thanh toán.", id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Không đối soát được hóa đơn {InvoiceId}.", id); }
        }
    }
}
