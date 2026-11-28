using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services.Shipping;

public sealed class ShippingService(IShippingRepository repository, IDepotService scope, IShippingProvider provider, IOptions<ShippingOptions> options, IDriverDeliveryRepository delivery)
{
    private static readonly string[] ReadyStates = ["TRANSPORT_READY", "ACCEPTED", "READY_FOR_PICKUP"];
    private static readonly string[] Terminal = ["COMPLETED","CANCELED","REJECTED","EXPIRED"];
    public async Task<object> ServicesAsync(Guid owner,Guid depot,CancellationToken ct) {
        await scope.RequireOwnerAsync(owner,depot);
        if(provider is LalamoveSandboxProvider lalamove) return await lalamove.CitiesAsync(ct);
        return new { maxWeightKg=options.Value.MaxWeightKg, internalFactor=options.Value.InternalFactor };
    }
    private Task LockAsync(Guid depot, CancellationToken ct) => repository.LockDepotAsync(depot, ct);
    private async Task<InventoryBatch> BatchAsync(Guid owner, Guid depot, Guid batch, CancellationToken ct)
    {
        await scope.RequireOwnerAsync(owner,depot);
        return await repository.FindBatchAsync(depot,batch,ct)
            ?? throw new KeyNotFoundException("Không tìm thấy lô trong kho đang quản lý.");
    }
    private async Task<ShippingRoute> RouteAsync(Guid depotId, Guid factoryId, string material, decimal weight, CancellationToken ct)
    {
        if (weight<=0 || weight>options.Value.MaxWeightKg || decimal.Round(weight,3)!=weight)
            throw new ArgumentException($"Khối lượng vận chuyển phải lớn hơn 0 và không quá {options.Value.MaxWeightKg} kg, tối đa 3 số lẻ.");
        material = Retrack.API.Services.Shared.MaterialCatalog.RequireCode(material);
        var depot = await repository.FindDepotAsync(depotId,ct);
        var factory = await repository.FindActiveFactoryAsync(factoryId,ct)
            ?? throw new ArgumentException("Nhà máy không còn hoạt động.");
        bool Coordinates(decimal? lat, decimal? lng) => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
        if (!Coordinates(depot.Latitude,depot.Longitude) || !Coordinates(factory.Latitude,factory.Longitude)
            || string.IsNullOrWhiteSpace(depot.Address) || string.IsNullOrWhiteSpace(factory.Address))
            throw new ArgumentException("Kho và nhà máy cần có địa chỉ, tọa độ hợp lệ để báo giá.");
        return new(factoryId,material,weight,depot.Address,depot.Latitude!.Value,depot.Longitude!.Value,
            factory.Address,factory.Latitude!.Value,factory.Longitude!.Value);
    }
    public async Task<ShippingQuote> QuoteAsync(Guid owner,Guid depot,QuoteRequest input,CancellationToken ct)
    {
        await scope.RequireOwnerAsync(owner,depot);
        await scope.RequireTradingAsync(owner,depot);
        RequireEnabled();
        if (input.Method is not ("INTERNAL" or "EXTERNAL")) throw new ArgumentException("Phương thức vận chuyển không hợp lệ.");
        var route = await RouteAsync(depot,input.FactoryId,input.MaterialType,input.WeightKg,ct);
        var result = await provider.QuoteAsync(route,ct);
        if (result.Amount<0 || result.Amount>1_000_000_000m || result.DistanceKm<0 || result.ExpiresAt<=DateTime.UtcNow)
            throw new HttpRequestException("Báo giá vận chuyển không hợp lệ hoặc đã hết hạn.");
        var factor = input.Method=="INTERNAL" ? options.Value.InternalFactor : 1m;
        var q = new ShippingQuote { OwnerId=owner,DepotId=depot,FactoryId=input.FactoryId,MaterialType=route.MaterialType,
            WeightKg=input.WeightKg,Method=input.Method,Provider=provider.Name,ServiceType=options.Value.ServiceType,
            ProviderQuoteId=result.Id,Amount=result.Amount,Factor=factor,EstimatedCost=decimal.Ceiling(result.Amount*factor),
            DistanceKm=result.DistanceKm,ExpiresAt=result.ExpiresAt,SnapshotJson=JsonSerializer.Serialize(route),ProviderJson=result.Json };
        repository.AddQuote(q); await repository.SaveAsync(ct); return q;
    }
    public async Task<ShippingQuote> ValidateQuoteAsync(Guid owner,Guid depot,Guid id,Guid? factory,string material,decimal weight,string method,CancellationToken ct)
    {
        var q = await repository.FindQuoteAsync(owner,depot,id,ct)
            ?? throw new KeyNotFoundException("Không tìm thấy báo giá của kho.");
        if (factory!=q.FactoryId || material!=q.MaterialType || weight!=q.WeightKg || method!=q.Method || q.Provider!=provider.Name)
            throw new DepotConflictException("Thông tin lô hoặc phương thức đã thay đổi. Hãy lấy lại báo giá.");
        if (q.ExpiresAt<=DateTime.UtcNow) throw new DepotConflictException("Báo giá đã hết hạn. Hãy lấy lại báo giá.");
        var route=await RouteAsync(depot,q.FactoryId,material,weight,ct);
        // So sánh giá trị thay vì chuỗi JSON: PostgreSQL có thể trả decimal với scale khác.
        if (JsonSerializer.Deserialize<ShippingRoute>(q.SnapshotJson)!=route) throw new DepotConflictException("Địa chỉ giao nhận đã thay đổi. Hãy lấy lại báo giá.");
        return q;
    }
    private void RequireEnabled()
    {
        if (options.Value.Mode=="Disabled") throw new DepotConflictException("Chưa bật dịch vụ vận chuyển. Liên hệ người quản trị.");
        if (options.Value.Mode=="LalamoveSandbox" && (!options.Value.ApiKey.StartsWith("pk_test",StringComparison.Ordinal)
            || !options.Value.ApiSecret.StartsWith("sk_test",StringComparison.Ordinal) || string.IsNullOrWhiteSpace(options.Value.ServiceType)))
            throw new DepotConflictException("Chưa cấu hình khóa và loại xe vận chuyển. Liên hệ người quản trị.");
    }
    public async Task<object> GetAsync(Guid owner,Guid depot,Guid id,CancellationToken ct)
    {
        var b=await BatchAsync(owner,depot,id,ct);
        var q=b.ShippingQuoteId.HasValue ? await repository.ReadQuoteAsync(b.ShippingQuoteId.Value,ct) : null;
        var shipment=await repository.ReadLatestAsync(id,ct);
        var events=shipment==null ? new List<ShippingEventDto>() : await repository.EventsAsync(shipment.Id,ct);
        return new { method=b.ShippingMethod, receivedAt=b.FactoryReceivedAt, enabled=options.Value.Mode!="Disabled", quote=q==null ? null : QuoteDto(q),
            shipment=shipment==null ? null : new {shipment.Id,shipment.OperationId,shipment.ProviderOrderId,shipment.Status,shipment.ShareLink,shipment.Amount,shipment.Currency,shipment.LastError,shipment.LastCheckedAt,shipment.CreatedAt,shipment.UpdatedAt}, events };
    }
    public static object QuoteDto(ShippingQuote q)=>new {q.Id,q.Amount,q.Factor,q.EstimatedCost,q.Currency,q.DistanceKm,q.ExpiresAt,q.ServiceType};

    public async Task SetMethodAsync(Guid owner,Guid depot,Guid id,string method,Guid? quoteId,CancellationToken ct)
    {
        if (method is not ("INTERNAL" or "EXTERNAL")) throw new ArgumentException("Phương thức vận chuyển không hợp lệ.");
        await scope.RequireOwnerAsync(owner,depot);
        await using var tx=await repository.BeginAsync(ct);
        await LockAsync(depot,ct);
        var b=await BatchAsync(owner,depot,id,ct);
        if (b.FactoryReceivedAt!=null || b.Status is not ("LISTED" or "DRAFT" or "PENDING_APPROVAL" or "PENDING_FACTORY" or "TRANSPORT_READY" or "ACCEPTED" or "READY_FOR_PICKUP")
            || b.TransportJob?.DriverId!=null || b.TransportJob?.Status is "PICKED_UP" or "IN_TRANSIT" or "DELIVERED")
            throw new DepotConflictException("Lô đã được nhận vận chuyển hoặc không còn cho phép đổi phương thức.");
        if (await repository.HasOpenShipmentAsync(id,Terminal,ct))
            throw new DepotConflictException("Cần xử lý hoặc hủy vận đơn hiện tại trước khi đổi phương thức.");
        if (method=="EXTERNAL") RequireEnabled();
        if (quoteId.HasValue) await ValidateQuoteAsync(owner,depot,quoteId.Value,b.TargetFactoryId??b.DirectOfferFactoryId,b.MaterialType,b.DeclaredWeightKg,method,ct);
        var changedToInternal=b.ShippingMethod=="EXTERNAL" && method=="INTERNAL";
        b.ShippingMethod=method; b.ShippingQuoteId=quoteId; b.UpdatedAt=DateTime.UtcNow;
        if(b.TransportJob!=null) b.TransportJob.Status=method=="EXTERNAL" ? "EXTERNAL_WAITING" : "PENDING";
        if(changedToInternal && b.TransportJob!=null)
            await delivery.QueueAvailableAsync(depot,b.TransportJob.Id,ct);
        await repository.SaveAsync(ct); await tx.CommitAsync(ct);
    }
    public static ShippingContacts ValidateContacts(ShippingContacts c)
    {
        string Name(string v) => !string.IsNullOrWhiteSpace(v) && v.Trim().Length<=100 ? v.Trim() : throw new ArgumentException("Tên người giao/nhận phải từ 1 đến 100 ký tự.");
        string Phone(string v) {
            v=Regex.Replace(v??"",@"[\s().-]","");
            if(Regex.IsMatch(v,@"^0\d{9}$")) v="+84"+v[1..];
            if(!Regex.IsMatch(v,@"^\+84\d{9}$")) throw new ArgumentException("Số điện thoại giao/nhận phải là số Việt Nam hợp lệ.");
            return v;
        }
        if(c.Note?.Length>500) throw new ArgumentException("Ghi chú giao hàng tối đa 500 ký tự.");
        return new(Name(c.SenderName),Phone(c.SenderPhone),Name(c.RecipientName),Phone(c.RecipientPhone),c.Note?.Trim());
    }
    public async Task BookAsync(Guid owner,Guid depot,Guid id,BookShippingRequest input,CancellationToken ct)
    {
        if(input.OperationId==Guid.Empty || input.Contacts==null) throw new ArgumentException("Thiếu mã thao tác hoặc thông tin liên hệ.");
        var contacts=ValidateContacts(input.Contacts);
        await scope.RequireOwnerAsync(owner,depot); await scope.RequireTradingAsync(owner,depot); RequireEnabled();
        ExternalShipment shipment; ShippingQuote quote;
        await using(var tx=await repository.BeginAsync(ct))
        {
            await LockAsync(depot,ct);
            var b=await BatchAsync(owner,depot,id,ct);
            var existing=await repository.FindOperationAsync(input.OperationId,ct);
            if(existing!=null) {
                if(existing.BatchId!=id || existing.QuoteId!=input.QuoteId || existing.ContactJson!=JsonSerializer.Serialize(contacts))
                    throw new DepotConflictException("Mã thao tác đã được dùng cho nội dung khác.");
                return; // Không gọi POST đối tác lại, kể cả lần trước chưa rõ kết quả.
            }
            if(b.ShippingMethod!="EXTERNAL" || !ReadyStates.Contains(b.Status) || b.TargetFactoryId==null || b.FactoryReceivedAt!=null
                || b.TransportJob?.DriverId!=null || b.TransportJob?.Status is "PICKED_UP" or "IN_TRANSIT" or "DELIVERED")
                throw new DepotConflictException("Chỉ đặt xe khi lô thuê ngoài đã được nhà máy chấp thuận, chưa có tài xế nhận.");
            if(await repository.HasOpenShipmentAsync(id,Terminal,ct))
                throw new DepotConflictException("Lô đã có yêu cầu vận chuyển. Hãy kiểm tra vận đơn hiện tại.");
            if(await repository.HasUsedQuoteAsync(input.QuoteId,ct))
                throw new DepotConflictException("Báo giá đã được dùng cho một yêu cầu đặt xe. Hãy lấy báo giá mới.");
            quote=await ValidateQuoteAsync(owner,depot,input.QuoteId,b.TargetFactoryId,b.MaterialType,b.DeclaredWeightKg,"EXTERNAL",ct);
            shipment=new ExternalShipment {BatchId=id,QuoteId=quote.Id,OperationId=input.OperationId,Provider=provider.Name,
                Amount=quote.Amount,ContactJson=JsonSerializer.Serialize(contacts)};
            repository.AddShipment(shipment);
            b.ShippingQuoteId=quote.Id;
            if(b.TransportJob==null) {
                b.TransportJob=new TransportJob {BatchId=id,Status="EXTERNAL_WAITING"};
                repository.AddTransport(b.TransportJob);
            }
            b.TransportJob.Status="EXTERNAL_WAITING";
            AddEvent(shipment,"BOOKING","OWNER");
            await repository.SaveAsync(ct); await tx.CommitAsync(ct);
        }
        try {
            var result=await provider.BookAsync(quote,contacts,input.OperationId,id,ct);
            await ApplyAsync(shipment.Id,result,"BOOKING_RESPONSE",ct);
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or JsonException) {
            // Đối tác có thể đã tạo đơn. Khóa retry để không tạo hai chuyến.
            repository.ClearTracking();
            var s=await repository.FindShipmentAsync(shipment.Id,CancellationToken.None);
            s.Status="UNKNOWN";s.LastError="Chưa xác định kết quả đặt xe. Cần đối soát mã thao tác với nhà vận chuyển; không đặt lại.";
            s.UpdatedAt=DateTime.UtcNow; AddEvent(s,"UNKNOWN","BOOKING_RESPONSE");
            await repository.SaveAsync(CancellationToken.None);
            throw new DepotConflictException(s.LastError);
        }
    }
    private void AddEvent(ExternalShipment s,string status,string source)=>repository.AddEvent(new ShippingEvent {ShipmentId=s.Id,Status=status,Source=source});
    public async Task ApplyAsync(Guid id,ProviderOrder result,string source,CancellationToken ct)
    {
        var depot=await repository.DepotForShipmentAsync(id,ct);
        repository.ClearTracking();
        await using var tx=await repository.BeginAsync(ct); await LockAsync(depot,ct);
        var s=await repository.FindShipmentAsync(id,ct);
        var b=await repository.FindBatchAsync(s.BatchId,ct);
        if(s.ProviderOrderId!=null && s.ProviderOrderId!=result.Id) throw new DepotConflictException("Mã vận đơn đối soát không khớp.");
        if(Terminal.Contains(s.Status)) return;
        if(result.Status is not ("ASSIGNING_DRIVER" or "ON_GOING" or "PICKED_UP" or "COMPLETED" or "CANCELED" or "REJECTED" or "EXPIRED"))
            throw new HttpRequestException("Trạng thái vận chuyển chưa được hỗ trợ; cần đối soát.");
        s.ProviderOrderId=result.Id;s.ShareLink=result.ShareLink;s.LastCheckedAt=DateTime.UtcNow;s.UpdatedAt=DateTime.UtcNow;s.LastError=null;
        if(result.Amount.HasValue) s.Amount=result.Amount.Value;
        if(s.Status!=result.Status) AddEvent(s,result.Status,source);
        s.Status=result.Status;
        // Không hạ trạng thái batch đã giao/nhận. Giao vận không tự hoàn tất QC/thanh toán.
        if(b.ShippingMethod=="EXTERNAL" && b.TransportJob!=null && b.FactoryReceivedAt==null) {
            if(result.Status=="PICKED_UP" && ReadyStates.Contains(b.Status)) {b.Status="IN_TRANSIT";b.TransportJob.Status="IN_TRANSIT";}
            if(result.Status=="COMPLETED" && (ReadyStates.Contains(b.Status)||b.Status=="IN_TRANSIT")) {b.Status="DELIVERED";b.TransportJob.Status="DELIVERED";}
            b.UpdatedAt=DateTime.UtcNow;b.TransportJob.UpdatedAt=DateTime.UtcNow;
        }
        await repository.SaveAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task RefreshAsync(Guid shipmentId,CancellationToken ct)
    {
        var s=await repository.ReadShipmentAsync(shipmentId,ct);
        if(s.Provider!=provider.Name || s.ProviderOrderId==null || Terminal.Contains(s.Status)) return;
        await ApplyAsync(s.Id,await provider.GetAsync(s,ct),"RECONCILIATION",ct);
    }
    public async Task RefreshForOwnerAsync(Guid owner,Guid depot,Guid batchId,CancellationToken ct)
    {
        await BatchAsync(owner,depot,batchId,ct);
        RequireEnabled();
        var shipment=await repository.ReadLatestAsync(batchId,ct)
            ?? throw new DepotConflictException("Lô chưa có yêu cầu vận chuyển.");
        if(shipment.Provider!=provider.Name || shipment.ProviderOrderId==null)
            throw new DepotConflictException("Chưa có mã vận đơn hoặc cấu hình nhà vận chuyển không khớp.");
        if(shipment.LastCheckedAt>DateTime.UtcNow.AddSeconds(-10))
            throw new DepotConflictException("Vận đơn vừa được kiểm tra. Vui lòng đợi 10 giây rồi thử lại.");
        await RefreshAsync(shipment.Id,ct);
    }
    public async Task RecoverAsync(Guid owner,Guid depot,Guid batchId,string orderId,CancellationToken ct)
    {
        await BatchAsync(owner,depot,batchId,ct); RequireEnabled();
        if(string.IsNullOrWhiteSpace(orderId)||orderId.Length>100)throw new ArgumentException("Mã vận đơn không hợp lệ.");
        var s=await repository.ReadRecoveryAsync(batchId,ct)
            ?? throw new KeyNotFoundException("Chưa có yêu cầu cần đối soát.");
        if(s.Status is not ("UNKNOWN" or "BOOKING") || s.Provider!=provider.Name || s.Provider!="LALAMOVE_SANDBOX")
            throw new DepotConflictException("Yêu cầu không ở trạng thái cần đối soát mã vận đơn.");
        s.ProviderOrderId=orderId.Trim();
        var result=await provider.GetAsync(s,ct);
        if(result.OperationId!=s.OperationId.ToString())throw new DepotConflictException("Vận đơn không thuộc mã thao tác này; chưa cập nhật dữ liệu.");
        await ApplyAsync(s.Id,result,"OWNER_RECONCILIATION",ct);
    }
    public async Task CancelAsync(Guid owner,Guid depot,Guid id,CancellationToken ct)
    {
        await scope.RequireOwnerAsync(owner,depot);
        ExternalShipment s;
        await using(var tx=await repository.BeginAsync(ct)) {
            await LockAsync(depot,ct); await BatchAsync(owner,depot,id,ct);
            s=await repository.FindLatestAsync(id,ct)
                ?? throw new KeyNotFoundException("Chưa có vận đơn.");
            if(s.Status=="CANCELED")return;
            if(s.Status is not ("ASSIGNING_DRIVER" or "ON_GOING") || s.ProviderOrderId==null || s.Provider!=provider.Name)
                throw new DepotConflictException("Vận đơn không còn cho phép yêu cầu hủy. Cần phối hợp nhà vận chuyển.");
            s.Status="CANCEL_REQUESTED";s.UpdatedAt=DateTime.UtcNow;AddEvent(s,s.Status,"OWNER");
            await repository.SaveAsync(ct);await tx.CommitAsync(ct);
        }
        await provider.CancelAsync(s,ct);
        // Sau DELETE thành công vẫn đối soát trạng thái; không tự hoàn tồn hoặc đổi sang xe kho.
        if(s.Provider=="DEMO") await ApplyAsync(s.Id,new(s.ProviderOrderId!,"CANCELED",null,s.Amount),"CANCEL_RESPONSE",ct);
        else await RefreshAsync(s.Id,ct);
    }

    // Worker gọi cùng service để giữ chính sách đối soát ở lớp nghiệp vụ.
    public async Task<List<Guid>> ReconciliationCandidatesAsync(CancellationToken ct)
    {
        var cutoff=DateTime.UtcNow.AddSeconds(-10);
        var staleBooking=DateTime.UtcNow.AddMinutes(-2);
        await repository.MarkStaleBookingsAsync(staleBooking,
            "Yêu cầu đặt xe bị gián đoạn. Cần đối soát mã thao tác trước khi đặt lại.",ct);
        return await repository.ReconciliationIdsAsync(provider.Name,cutoff,ct);
    }
    public async Task RecordSyncFailureAsync(Guid id,CancellationToken ct)
    {
        repository.ClearTracking();
        var shipment=await repository.FindShipmentAsync(id,ct);
        shipment.LastCheckedAt=DateTime.UtcNow;
        shipment.LastError="Chưa đồng bộ được trạng thái vận chuyển. Hệ thống sẽ thử lại.";
        await repository.SaveAsync(ct);
    }
}
