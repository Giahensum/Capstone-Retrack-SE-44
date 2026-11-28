namespace Retrack.API.Services.Shipping;

public sealed class ShippingWorker(IServiceScopeFactory scopes,ILogger<ShippingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(10));
        do {
            try {
                using var scope=scopes.CreateScope();
                var service=scope.ServiceProvider.GetRequiredService<ShippingService>();
                var ids=await service.ReconciliationCandidatesAsync(ct);
                // Tối đa 5 lượt/10 giây; mỗi vận đơn dùng DbContext riêng.
                // Một API chậm không chặn toàn bộ các vận đơn phía sau.
                await Parallel.ForEachAsync(ids,new ParallelOptions {MaxDegreeOfParallelism=3,CancellationToken=ct},async (id,token)=> {
                    using var itemScope=scopes.CreateScope();
                    var itemService=itemScope.ServiceProvider.GetRequiredService<ShippingService>();
                    try {await itemService.RefreshAsync(id,token);}
                    catch(Exception ex) when(!ct.IsCancellationRequested) {
                        await itemService.RecordSyncFailureAsync(id,token);
                        logger.LogWarning("Chưa đối soát được vận đơn nội bộ {ShipmentId}; loại lỗi {ErrorType}",id,ex.GetType().Name);
                    }
                });
            } catch(OperationCanceledException) when(ct.IsCancellationRequested) {break;}
            catch(Exception) {logger.LogWarning("Chưa chạy được đối soát vận chuyển; kiểm tra cấu hình và SQL nâng cấp.");}
        } while(await timer.WaitForNextTickAsync(ct));
    }
}
