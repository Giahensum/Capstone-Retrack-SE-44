using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;

namespace Retrack.API.Services.Shipping;

public sealed class ShippingWorker(IServiceScopeFactory scopes,ILogger<ShippingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(10));
        do {
            try {
                using var scope=scopes.CreateScope();
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var provider=scope.ServiceProvider.GetRequiredService<IShippingProvider>();
                var cutoff=DateTime.UtcNow.AddSeconds(-10);
                var staleBooking=DateTime.UtcNow.AddMinutes(-2);
                await db.ExternalShipments.Where(s=>s.Status=="BOOKING" && s.CreatedAt<staleBooking)
                    .ExecuteUpdateAsync(update=>update.SetProperty(s=>s.Status,"UNKNOWN")
                        .SetProperty(s=>s.LastError,"Yêu cầu đặt xe bị gián đoạn. Cần đối soát mã thao tác trước khi đặt lại."),ct);
                var ids=await db.ExternalShipments.AsNoTracking()
                    .Where(s=>s.Provider==provider.Name && s.ProviderOrderId!=null &&
                        (s.Status=="ASSIGNING_DRIVER"||s.Status=="ON_GOING"||s.Status=="PICKED_UP"||s.Status=="CANCEL_REQUESTED") && s.LastCheckedAt<cutoff)
                    .OrderBy(s=>s.LastCheckedAt).ThenBy(s=>s.Id).Take(5).Select(s=>s.Id).ToListAsync(ct);
                // Tối đa 5 lượt/10 giây; mỗi vận đơn dùng DbContext riêng.
                // Một API chậm không chặn toàn bộ các vận đơn phía sau.
                await Parallel.ForEachAsync(ids,new ParallelOptions {MaxDegreeOfParallelism=3,CancellationToken=ct},async (id,token)=> {
                    using var itemScope=scopes.CreateScope();
                    var itemDb=itemScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    try {await itemScope.ServiceProvider.GetRequiredService<ShippingService>().RefreshAsync(id,token);}
                    catch(Exception ex) when(!ct.IsCancellationRequested) {
                        itemDb.ChangeTracker.Clear();
                        var shipment=await itemDb.ExternalShipments.SingleAsync(s=>s.Id==id,token);
                        shipment.LastCheckedAt=DateTime.UtcNow;
                        shipment.LastError="Chưa đồng bộ được trạng thái vận chuyển. Hệ thống sẽ thử lại.";
                        await itemDb.SaveChangesAsync(token);
                        logger.LogWarning("Chưa đối soát được vận đơn nội bộ {ShipmentId}; loại lỗi {ErrorType}",id,ex.GetType().Name);
                    }
                });
            } catch(OperationCanceledException) when(ct.IsCancellationRequested) {break;}
            catch(Exception) {logger.LogWarning("Chưa chạy được đối soát vận chuyển; kiểm tra cấu hình và SQL nâng cấp.");}
        } while(await timer.WaitForNextTickAsync(ct));
    }
}
