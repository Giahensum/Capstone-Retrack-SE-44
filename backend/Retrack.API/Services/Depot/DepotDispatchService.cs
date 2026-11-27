using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Interfaces;
namespace Retrack.API.Services.Depot;

public sealed class DepotDispatchService(IDepotDispatchRepository repository, IDepotService scope, IDepotUnitOfWork work, TimeProvider? clock = null)
{
    public async Task<IReadOnlyList<CollectionRegion>> CatalogAsync(Guid owner, Guid depot)
    {
        await scope.RequireOwnerAsync(owner, depot);
        return DanangCollectionCatalog.Regions;
    }

    private async Task<EmployeeCollectionArea> ResolveCatalogAreaAsync(Guid depot, string code)
    {
        var region = DanangCollectionCatalog.Find(code);
        var id = DanangCollectionCatalog.AreaId(depot, code);
        var area = await repository.AreaAsync(id)
            ?? await repository.MatchingAreaAsync(depot, region.Latitude, region.Longitude);
        if (area != null)
        {
            if (area.DepotId != depot) throw new DepotConflictException("Mã vùng xung đột với kho khác.");
            return area; // Không ghi đè tâm/bán kính hoặc mở lại vùng Owner đã tắt.
        }
        area = new EmployeeCollectionArea { Id = id, DepotId = depot, Name = region.Name,
            Latitude = region.Latitude, Longitude = region.Longitude, RadiusMeters = region.RadiusMeters, IsActive = true };
        repository.AddArea(area);
        return area;
    }

    public async Task<DispatchAreaDto> UseCatalogAreaAsync(Guid owner, Guid depot, string code)
    {
        await scope.RequireOwnerAsync(owner, depot);
        await using var tx = await work.BeginAsync(); await repository.LockAsync();
        var area = await ResolveCatalogAreaAsync(depot, code);
        await work.SaveAsync(); await tx.CommitAsync();
        return new(area.Id, area.Name, area.Latitude, area.Longitude, area.RadiusMeters, area.IsActive, area.DepotId);
    }
    public async Task<PagedResult<StaffDto>> EmployeesAsync(Guid owner,Guid depot,DepotQuery query)
    {await scope.RequireOwnerAsync(owner,depot);Page(query);return await repository.EmployeesAsync(depot,query);}
    private static void Page(DepotQuery query)
    { if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.Search?.Length > 255) throw new ArgumentException("Phân trang hoặc từ khóa không hợp lệ."); }
    public async Task<PagedResult<DispatchAreaDto>> AreasAsync(Guid owner, Guid depot, DepotQuery query)
    { await scope.RequireOwnerAsync(owner, depot); Page(query); return await repository.AreasAsync(depot, query); }
    public async Task<PagedResult<DispatchShiftDto>> ShiftsAsync(Guid owner, Guid depot, DepotQuery query, bool availableOnly = false, Guid? excludeEmployeeId = null, Guid? pickupId = null)
    {
        await scope.RequireOwnerAsync(owner, depot); Page(query);
        if (query.Status is not (null or "" or "CURRENT" or "UPCOMING" or "ENDED")) throw new ArgumentException("Bộ lọc ca không hợp lệ.");
        DateTime? preferred = null;
        if (pickupId.HasValue)
        {
            if (!availableOnly) throw new ArgumentException("Chỉ chọn đơn khi tìm ca có khả năng nhận.");
            var pickup = await repository.DetailAsync(depot, pickupId.Value) ?? throw new KeyNotFoundException("Không tìm thấy đơn trong kho.");
            preferred = pickup.PreferredDatetime;
        }
        return await repository.ShiftsAsync(depot, query, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, availableOnly, excludeEmployeeId, preferred);
    }
    public async Task<PagedResult<DispatchPickupDto>> PickupsAsync(Guid owner, Guid depot, DepotQuery query)
    {
        await scope.RequireOwnerAsync(owner, depot); Page(query);
        if (query.Status is not (null or "" or "PENDING" or "SCHEDULED" or "EXCEPTIONS" or "TODAY" or "UPCOMING")) throw new ArgumentException("Bộ lọc điều phối không hợp lệ.");
        return await repository.PickupsAsync(depot, query);
    }
    public async Task<DispatchSummaryDto> SummaryAsync(Guid owner, Guid depot)
    { await scope.RequireOwnerAsync(owner, depot); return await repository.SummaryAsync(depot, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime); }
    public async Task<DispatchPickupDto> DetailAsync(Guid owner, Guid depot, Guid id)
    { await scope.RequireOwnerAsync(owner, depot); return await repository.DetailAsync(depot, id) ?? throw new KeyNotFoundException("Không tìm thấy đơn trong kho."); }
    public async Task<PagedResult<DispatchHistoryDto>> HistoryAsync(Guid owner, Guid depot, Guid id, DepotQuery query)
    { await DetailAsync(owner, depot, id); Page(query); return await repository.HistoryAsync(depot, id, query); }
    public async Task<DispatchAreaDto> SaveAreaAsync(Guid owner, Guid depot, Guid id, DispatchAreaInput input)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120 ||
            !double.IsFinite(input.Latitude) || Math.Abs(input.Latitude) > 90 || !double.IsFinite(input.Longitude) || Math.Abs(input.Longitude) > 180 ||
            !double.IsFinite(input.RadiusMeters) || input.RadiusMeters is < 100 or > 20000) throw new ArgumentException("Tên/tọa độ hoặc bán kính khu vực (100–20.000m) không hợp lệ.");
        await using var tx = await work.BeginAsync(); await repository.LockAsync();
        var area = await repository.AreaAsync(id);
        if (area != null && area.DepotId != depot) throw new KeyNotFoundException("Không tìm thấy khu vực.");
        if (area == null) { area = new() { Id = id, DepotId = depot }; repository.AddArea(area); }
        area.Name = input.Name.Trim(); area.Latitude = input.Latitude; area.Longitude = input.Longitude; area.RadiusMeters = input.RadiusMeters; area.IsActive = input.IsActive;
        await work.SaveAsync(); await tx.CommitAsync(); return new(area.Id, area.Name, area.Latitude, area.Longitude, area.RadiusMeters, area.IsActive, area.DepotId);
    }
    public async Task<EmployeeDispatchShift> CreateShiftAsync(Guid owner, Guid depot, DispatchShiftInput input)
    {
        await scope.RequireOwnerAsync(owner, depot);
        var start = input.StartsAt.UtcDateTime; var end = input.EndsAt.UtcDateTime; var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (input.OperationId == Guid.Empty || end <= start || end - start > TimeSpan.FromHours(24) ||
            input.Vehicle is not ("motorcycle" or "car" or "truck") || input.CapacityKg is <= 0 or > 20000 || decimal.Round(input.CapacityKg, 2) != input.CapacityKg || input.MaxPendingOrders is < 1 or > 50)
            throw new ArgumentException("Ca tối đa 24 giờ; phương tiện/tải trọng tối đa hai số lẻ/giới hạn đơn không hợp lệ.");
        await using var tx = await work.BeginAsync(); await repository.LockAsync();
        if (input.CatalogCode != null && input.AreaId != Guid.Empty)
            throw new ArgumentException("Chỉ chọn vùng đã cấu hình hoặc vùng trong danh mục, không chọn đồng thời.");
        var area = input.CatalogCode == null ? await repository.AreaAsync(input.AreaId)
            : await ResolveCatalogAreaAsync(depot, input.CatalogCode);
        if (area == null || area.DepotId != depot) throw new ArgumentException("Khu vực không thuộc kho.");
        if (input.OperationId.HasValue)
        {
            var old = await repository.ShiftAsync(input.OperationId.Value);
            if (old != null)
            {
                if (old.AreaId != area.Id || old.EmployeeId != input.EmployeeId || old.StartsAt != start || old.EndsAt != end || old.Vehicle != input.Vehicle || old.CapacityKg != input.CapacityKg || old.MaxPendingOrders != input.MaxPendingOrders)
                    throw new DepotConflictException("Mã thao tác đã được dùng cho ca khác.");
                await tx.CommitAsync(); return old;
            }
        }
        if (end <= now || start > now.AddDays(60) || !area.IsActive || !await repository.ActiveEmployeeAsync(depot, input.EmployeeId))
            throw new ArgumentException("Chọn khu vực/nhân viên đang hoạt động và ca chưa kết thúc, trong 60 ngày tới.");
        if (await repository.OverlapAsync(input.EmployeeId, start, end)) throw new DepotConflictException("Nhân viên đã có ca trùng giờ.");
        var shift = new EmployeeDispatchShift { Id = input.OperationId ?? Guid.NewGuid(), AreaId = area.Id, EmployeeId = input.EmployeeId, StartsAt = start, EndsAt = end, Vehicle = input.Vehicle, CapacityKg = input.CapacityKg, MaxPendingOrders = input.MaxPendingOrders };
        repository.AddShift(shift); await work.SaveAsync(); await tx.CommitAsync(); return shift;
    }
    public async Task AssignAsync(Guid owner, Guid depot, Guid id, DispatchAssignInput input)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (input.OperationId == Guid.Empty || (input.ShiftId == Guid.Empty && !input.EmployeeId.HasValue) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 10 or > 1000)
            throw new ArgumentException("Cần ca và lý do phân công từ 10 đến 1.000 ký tự.");
        if(input.EmployeeId.HasValue && input.ShiftId!=Guid.Empty) throw new ArgumentException("Chỉ chọn ca hoặc người dự phòng, không chọn đồng thời.");
        await using var tx = await work.BeginAsync(); await repository.LockAsync();
        var pickup = await repository.LockPickupAsync(depot, id) ?? throw new KeyNotFoundException("Không tìm thấy đơn trong kho.");
        var old = await repository.EventAsync(input.OperationId);
        if (old != null)
        {
            if (old.PickupId != id || (input.ShiftId!=Guid.Empty && old.ShiftId != input.ShiftId) || (input.EmployeeId.HasValue && old.ToEmployeeId!=input.EmployeeId) || old.Reason != input.Reason.Trim()) throw new DepotConflictException("Mã thao tác đã dùng cho phân công khác.");
            await tx.CommitAsync(); return;
        }
        await scope.RequireTradingAsync(owner, depot);
        if (pickup.Status is not ("PENDING" or "SCHEDULED") || !string.IsNullOrEmpty(pickup.CheckinImageUrl) || await repository.HasCheckInAsync(id))
            throw new DepotConflictException("Đơn đã bắt đầu thu gom/giữ hàng hoặc kết thúc; không được chuyển người bằng điều phối.");
        if(pickup.RequiresOwnerReview || Retrack.API.Services.Employee.PickupSchedule.Day(pickup)!=Retrack.API.Services.Employee.PickupSchedule.Today((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime) || (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime>=Retrack.API.Services.Employee.PickupSchedule.End(pickup))
            throw new DepotConflictException("Đơn đã quá hẹn hoặc đang chờ xử lý. Hãy xác nhận lịch mới với người bán trước khi phân công.");
        var shift = input.ShiftId!=Guid.Empty ? await repository.ShiftAsync(input.ShiftId) : null;
        if(shift==null && input.EmployeeId.HasValue) {
            if(!await repository.ActiveEmployeeAsync(depot,input.EmployeeId.Value)) throw new ArgumentException("Nhân viên không thuộc kho đang hoạt động.");
            var existing=await repository.ShiftsAsync(depot,new DepotQuery {Page=1,PageSize=100},(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime,true);
            shift=existing.Items.FirstOrDefault(x=>x.EmployeeId==input.EmployeeId.Value) is {} candidate ? await repository.ShiftAsync(candidate.Id) : null;
            if(shift==null) {
                var day=Retrack.API.Services.Employee.PickupSchedule.Today((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
                var start=Retrack.API.Services.Employee.PickupSchedule.At(day,7);var end=Retrack.API.Services.Employee.PickupSchedule.At(day,19);
                if(end<=(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime || await repository.OverlapAsync(input.EmployeeId.Value,start,end))
                    throw new DepotConflictException("Nhân viên đang có ca đầy/tạm ngừng hoặc trùng giờ. Không tự bật lại ca; hãy chọn người khác hoặc yêu cầu nhân viên sẵn sàng trở lại.");
                var areas=await repository.AreasAsync(depot,new DepotQuery {Page=1,PageSize=100});
                var target=areas.Items.Where(a=>a.IsActive).OrderBy(a=>pickup.Latitude.HasValue && pickup.Longitude.HasValue ?
                    Retrack.API.Services.Employee.CollectionValidation.Distance((double)pickup.Latitude.Value,(double)pickup.Longitude.Value,a.Latitude,a.Longitude) : 0).ThenBy(a=>a.Id).FirstOrDefault()
                    ?? throw new DepotConflictException("Kho chưa có vùng hoạt động.");
                shift=new EmployeeDispatchShift {EmployeeId=input.EmployeeId.Value,AreaId=target.Id,StartsAt=start,EndsAt=end,IsAutomatic=true,Reason="Chủ kho bố trí người dự phòng xử lý ngoại lệ"};
                repository.AddShift(shift);
                await work.SaveAsync(); // Lưu ca trước sự kiện có FK, vẫn trong cùng transaction.
            }
        }
        if(shift==null) throw new ArgumentException("Không tìm thấy ca.");
        var area = await repository.AreaAsync(shift.AreaId); var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (area == null || area.DepotId != depot || !area.IsActive || !await repository.ActiveEmployeeAsync(depot, shift.EmployeeId)) throw new ArgumentException("Ca hoặc nhân viên không thuộc kho đang hoạt động.");
        if (shift.Availability != "AVAILABLE" || shift.StartsAt >= Retrack.API.Services.Employee.PickupSchedule.End(pickup) || shift.EndsAt <= now || pickup.PreferredDatetime >= shift.EndsAt ||
            pickup.PreferredDatetime >= now.AddHours(7).Date.AddDays(1).AddHours(-7)) throw new DepotConflictException("Ca chưa sẵn sàng/không phù hợp ngày hẹn; đơn ngày sau chờ đúng ngày phân công.");
        if (pickup.AcceptedCollectorId == shift.EmployeeId) throw new DepotConflictException("Đơn đã thuộc nhân viên này. Hãy tải lại dữ liệu.");
        if (!shift.IsAutomatic && await repository.AssignedCountAsync(depot, shift.EmployeeId) >= shift.MaxPendingOrders) throw new DepotConflictException("Nhân viên đã đủ giới hạn số đơn trong ca.");
        var previous = pickup.AcceptedCollectorId; pickup.AcceptedCollectorId = shift.EmployeeId; pickup.Status = "SCHEDULED"; pickup.UpdatedAt = now;
        repository.AddEvent(new() { Id = input.OperationId, PickupId = id, FromEmployeeId = previous, ToEmployeeId = shift.EmployeeId, ShiftId = shift.Id, Reason = input.Reason.Trim(), CreatedAt = now });
        var queue = await repository.QueueAsync(id); if (queue == null) { queue = new() { PickupId = id }; repository.AddQueue(queue); }
        queue.Reason = "ASSIGNED_MANUAL"; queue.UpdatedAt = now;
        void Notify(Guid recipient, string title) { var n = Notification.ForDepot(recipient, depot, "PICKUP", title, $"Đơn {id}: {input.Reason.Trim()}", targetType: "PICKUP_REQUEST", targetId: id); n.PickupRequestId = id; repository.AddNotice(n); }
        Notify(shift.EmployeeId, "Chủ kho đã phân công đơn thu gom"); Notify(pickup.SellerId, "Kho đã phân công nhân viên thu gom");
        if (previous.HasValue) Notify(previous.Value, "Chủ kho đã chuyển đơn sang nhân viên khác");
        await work.SaveAsync(); await tx.CommitAsync();
    }
    public async Task RescheduleAsync(Guid owner,Guid depot,Guid id,DispatchRescheduleInput input)
    {
        await scope.RequireOwnerAsync(owner,depot);
        if(input.OperationId==Guid.Empty || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is <10 or >1000)
            throw new ArgumentException("Cần mã thao tác và lý do đã thống nhất với người bán từ 10–1.000 ký tự.");
        Retrack.API.Services.Employee.PickupSchedule.Validate(input.CollectionDate,input.PreferredDatetime,input.PreferredEndDatetime,(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        if(!input.PreferredDatetime.HasValue && Retrack.API.Services.Employee.PickupSchedule.At(input.CollectionDate,19)<=(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime)
            throw new ArgumentException("Ngày thu gom đã hết giờ hoạt động.");
        await using var tx=await work.BeginAsync();await repository.LockAsync();
        var pickup=await repository.LockPickupAsync(depot,id) ?? throw new KeyNotFoundException("Không tìm thấy đơn trong kho.");
        var audit=$"Hẹn lại {input.CollectionDate:yyyy-MM-dd} {input.PreferredDatetime:O} {input.PreferredEndDatetime:O}: {input.Reason.Trim()}";
        var retry=await repository.EventAsync(input.OperationId);
        if(retry!=null) {if(retry.PickupId!=id || !retry.Reason.StartsWith(audit+"\nLịch trước:",StringComparison.Ordinal)) throw new DepotConflictException("Mã thao tác đã dùng cho yêu cầu khác.");await tx.CommitAsync();return;}
        if(pickup.Status is not ("PENDING" or "SCHEDULED") || !string.IsNullOrEmpty(pickup.CheckinImageUrl) || await repository.HasCheckInAsync(id))
            throw new DepotConflictException("Đơn đã bắt đầu thu gom hoặc kết thúc; không được hẹn lại bằng điều phối.");
        var previous=pickup.AcceptedCollectorId;
        repository.AddEvent(new() {Id=input.OperationId,PickupId=id,FromEmployeeId=previous,Reason=audit+$"\nLịch trước: {pickup.CollectionDate:yyyy-MM-dd} {pickup.PreferredDatetime:O} → {pickup.PreferredEndDatetime:O}",CreatedAt=(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime});
        pickup.CollectionDate=input.CollectionDate;pickup.PreferredDatetime=input.PreferredDatetime;pickup.PreferredEndDatetime=input.PreferredEndDatetime;
        pickup.RequiresOwnerReview=false;pickup.DispatchReviewReason=null;pickup.AcceptedCollectorId=null;pickup.Status="PENDING";pickup.UpdatedAt=(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var queue=await repository.QueueAsync(id);if(queue==null){queue=new(){PickupId=id};repository.AddQueue(queue);}queue.Reason="Đã xác nhận lịch mới, chờ điều phối";queue.UpdatedAt=(clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        void Notice(Guid user,string title){var n=Notification.ForDepot(user,depot,"PICKUP",title,audit,targetType:"PICKUP_REQUEST",targetId:id);n.PickupRequestId=id;repository.AddNotice(n);}
        Notice(pickup.SellerId,"Kho đã cập nhật lịch thu gom");if(previous.HasValue)Notice(previous.Value,"Đơn đã được hẹn lại");
        await work.SaveAsync();await tx.CommitAsync();
    }
    public async Task StopShiftAsync(Guid owner, Guid depot, Guid id, string reason)
    {
        await scope.RequireOwnerAsync(owner, depot);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 1000) throw new ArgumentException("Cần lý do ngừng ca từ 10 đến 1.000 ký tự.");
        await using var tx = await work.BeginAsync(); await repository.LockAsync();
        var shift = await repository.ShiftAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy ca.");
        var area = await repository.AreaAsync(shift.AreaId);
        if (area?.DepotId != depot) throw new KeyNotFoundException("Không tìm thấy ca trong kho.");
        if (shift.Availability == "STOPPED")
        { if (shift.Reason != reason.Trim()) throw new DepotConflictException("Ca đã ngừng với lý do khác; không sửa lịch sử."); await tx.CommitAsync(); return; }
        if (shift.EndsAt <= (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime) throw new DepotConflictException("Ca đã kết thúc; không sửa lịch sử ca cũ.");
        shift.Availability = "STOPPED"; shift.Reason = reason.Trim(); shift.UpdatedAt = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var notice = Notification.ForDepot(shift.EmployeeId, depot, "PICKUP", "Chủ kho đã ngừng ca thu gom", reason.Trim()); repository.AddNotice(notice);
        await work.SaveAsync(); await tx.CommitAsync();
        // Worker TV3 điều phối lại đơn chưa check-in trong lượt kế tiếp; không bỏ người đang giữ hàng.
    }
    public async Task<object> ConfigurationAsync(Guid owner, Guid depot, int page)
    {
        await scope.RequireOwnerAsync(owner, depot); LegacyPage(page); return await repository.ConfigurationAsync(depot, page);
    }
    public async Task<List<DispatchEventCompatDto>> LegacyHistoryAsync(Guid owner, Guid depot, Guid pickup, int page)
    {
        await DetailAsync(owner, depot, pickup); LegacyPage(page); return await repository.LegacyHistoryAsync(pickup, page);
    }
    private static void LegacyPage(int page)
    { if (page is < 1 or > 100000) throw new ArgumentException("Trang từ 1 đến 100.000."); }
}
