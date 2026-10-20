using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Employee;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;

namespace Retrack.API.Services
{
    public interface IPickupService
    {
        Task<PickupRequestDto> CreateAsync(Guid sellerId, CreatePickupRequestDto dto);
        Task<PickupRequestDto?> GetByIdAsync(Guid id, Guid actorId);
        Task<List<PickupRequestDto>> GetBySellerAsync(Guid sellerId);
        Task<List<PickupRequestDto>> GetPendingForDepotAsync(Guid depotId, Guid actorId);
        Task<PickupRequestDto> AcceptRequestAsync(Guid requestId, Guid collectorId);
        Task<PickupRequestDto> WeighAndUpdateAsync(Guid requestId, Guid collectorId, List<WeighItemDto> items, string? checkinImageUrl);
        Task<PickupRequestDto> ConfirmBySellerAsync(Guid requestId, Guid sellerId);
        Task<PickupRequestDto> MarkPaymentSentAsync(Guid requestId, Guid ownerId, string paymentProofUrl);
        Task<PickupRequestDto> MarkDoneAsync(Guid requestId, Guid sellerId);
    }

    public class PickupService : IPickupService
    {
        private readonly IPickupRequestRepository _repo;
        private readonly AppDbContext _db;
        private readonly Interfaces.IDepotPaymentService _payments;

        public PickupService(IPickupRequestRepository repo, AppDbContext db, Interfaces.IDepotPaymentService payments)
        {
            _repo = repo;
            _db = db;
            _payments = payments;
        }

        public async Task<PickupRequestDto> CreateAsync(Guid sellerId, CreatePickupRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Address))
                throw new ArgumentException("Địa chỉ thu gom không được để trống.");
            if (dto.Address.Length > 500)
                throw new ArgumentException("Địa chỉ thu gom không được vượt quá 500 ký tự.");
            if (dto.Description != null && dto.Description.Length > 1000)
                throw new ArgumentException("Mô tả không được vượt quá 1000 ký tự.");

            if (dto.TargetDepotId.HasValue)
            {
                var depotExists = await _db.Depots.AnyAsync(d => d.Id == dto.TargetDepotId.Value && d.VerificationStatus == "APPROVED" && d.Owner.IsActive);
                if (!depotExists)
                    throw new ArgumentException("Kho vựa chưa xác minh hồ sơ hoặc không còn hoạt động.");
            }
            else
            {
                if (!dto.Latitude.HasValue || !dto.Longitude.HasValue)
                    throw new ArgumentException("Khi chọn hình thức nổ đơn (broadcast), cần cung cấp tọa độ vị trí (kinh độ, vĩ độ) để các kho trong bán kính 20km nhận đơn.");
            }

            if (dto.Latitude.HasValue && (dto.Latitude.Value < -90 || dto.Latitude.Value > 90))
                throw new ArgumentException("Vĩ độ (latitude) phải nằm trong khoảng từ -90 đến 90.");

            if (dto.Longitude.HasValue && (dto.Longitude.Value < -180 || dto.Longitude.Value > 180))
                throw new ArgumentException("Kinh độ (longitude) phải nằm trong khoảng từ -180 đến 180.");

            if (dto.PreferredDatetime.HasValue && dto.PreferredDatetime.Value < DateTime.UtcNow.AddMinutes(-30))
                throw new ArgumentException("Thời gian thu gom mong muốn không thể ở quá khứ.");

            // Get platform fee from system config
            var feeConfig = await _db.SystemConfigs.FindAsync("PLATFORM_FEE_PERCENTAGE");
            var feePercent = decimal.Parse(feeConfig?.ConfigValue ?? "5.00", System.Globalization.CultureInfo.InvariantCulture);
            if (feePercent is < 0 or > 100) throw new InvalidOperationException("Cấu hình phí nền tảng không hợp lệ.");

            var request = new PickupRequest
            {
                SellerId = sellerId,
                TargetDepotId = dto.TargetDepotId,
                Description = dto.Description,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                PreferredDatetime = dto.PreferredDatetime,
                RequestImageUrl = dto.RequestImageUrl,
                PlatformFeePercentage = feePercent,
                Status = "PENDING"
            };

            await _repo.CreateAsync(request);
            _db.Notifications.Add(new Notification
            {
                UserId = sellerId,
                PickupRequestId = request.Id,
                Category = "PICKUP",
                TargetType = "PICKUP_REQUEST",
                TargetId = request.Id,
                Title = "Tạo đơn thu gom thành công",
                Message = $"Đơn thu gom #{request.Id.ToString()[..8].ToUpper()} đã được tạo và đang chờ kho tiếp nhận.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return await MapToDto(request);
        }

        public async Task<PickupRequestDto?> GetByIdAsync(Guid id, Guid actorId)
        {
            var req = await _repo.GetByIdAsync(id);
            if (req != null && req.SellerId != actorId &&
                !await _db.Depots.AnyAsync(d => d.Id == req.TargetDepotId && d.OwnerId == actorId) &&
                !(req.AcceptedCollectorId == actorId && await _db.DepotStaffs.AnyAsync(s =>
                    s.UserId == actorId && s.DepotId == req.TargetDepotId && s.IsActive && s.User.IsActive)))
                throw new DepotForbiddenException();
            return req == null ? null : await MapToDto(req);
        }

        public async Task<List<PickupRequestDto>> GetBySellerAsync(Guid sellerId)
        {
            var requests = await _repo.GetBySellerIdAsync(sellerId);
            var result = new List<PickupRequestDto>();
            foreach (var r in requests) result.Add(await MapToDto(r));
            return result;
        }

        public async Task<List<PickupRequestDto>> GetPendingForDepotAsync(Guid depotId, Guid actorId)
        {
            var owner = await _db.Depots.AnyAsync(d => d.Id == depotId && d.OwnerId == actorId && d.Owner.IsActive);
            var employee = await _db.DepotStaffs.AnyAsync(s => s.DepotId == depotId && s.UserId == actorId &&
                s.IsActive && s.User.IsActive && s.StaffType == "DEPOT_EMPLOYEE");
            if (!owner && !employee) throw new DepotForbiddenException();
            var requests = await _db.PickupRequests.Include(p => p.Seller).Include(p => p.TargetDepot).Include(p => p.Items)
                .Where(p => p.TargetDepotId == depotId && (owner || p.Status == "PENDING" || p.AcceptedCollectorId == actorId))
                .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(100).ToListAsync();
            var result = new List<PickupRequestDto>();
            foreach (var r in requests) result.Add(await MapToDto(r));
            return result;
        }

        public async Task<PickupRequestDto> AcceptRequestAsync(Guid requestId, Guid collectorId)
        {
            var staff = await _db.DepotStaffs.SingleOrDefaultAsync(s => s.UserId == collectorId &&
                s.IsActive && s.User.IsActive && s.User.Role == "DEPOT_EMPLOYEE" && s.StaffType == "DEPOT_EMPLOYEE")
                ?? throw new DepotForbiddenException();
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await _db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {requestId} FOR UPDATE").LoadAsync();
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            if (req.TargetDepotId != staff.DepotId) throw new DepotForbiddenException();
            if (req.Status != "PENDING" || req.AcceptedCollectorId != null)
                throw new DepotConflictException("Đơn đã được nhận hoặc không còn chờ nhận.");
            if (!await _db.Depots.AnyAsync(d => d.Id == staff.DepotId && d.VerificationStatus == "APPROVED"))
                throw new DepotConflictException("Kho chưa được xác minh hồ sơ để nhận giao dịch mới.");
            req.AcceptedCollectorId = collectorId;
            req.Status = "SCHEDULED";
            _db.Notifications.Add(new Notification
            {
                UserId = req.SellerId,
                PickupRequestId = req.Id,
                Category = "PICKUP",
                TargetType = "PICKUP_REQUEST",
                TargetId = req.Id,
                Title = "Đơn thu gom đã được tiếp nhận",
                Message = $"Kho đã tiếp nhận đơn #{req.Id.ToString()[..8].ToUpper()} và lên lịch thu gom.",
                CreatedAt = DateTime.UtcNow
            });
            await _repo.UpdateAsync(req);
            await transaction.CommitAsync();
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> WeighAndUpdateAsync(Guid requestId, Guid collectorId, List<WeighItemDto> items, string? checkinImageUrl)
        {
            if (items == null || items.Count == 0 || items.Any(i => i == null))
                throw new ArgumentException("Cần ít nhất một loại phế liệu hợp lệ trước khi gửi kết quả.");
            var validated = Employee.CollectionValidation.Items(items.Select(i =>
                new DTOs.Employee.ClassificationItemInput(i.MaterialType, i.WeightKg, i.PricePerKg)).ToList());
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await _db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {requestId} FOR UPDATE").LoadAsync();
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            if (req.AcceptedCollectorId != collectorId || !await _db.DepotStaffs.AnyAsync(s =>
                s.UserId == collectorId && s.DepotId == req.TargetDepotId && s.IsActive && s.User.IsActive &&
                s.StaffType == "DEPOT_EMPLOYEE" && s.User.Role == "DEPOT_EMPLOYEE" && s.Depot.Owner.IsActive)) throw new DepotForbiddenException();
            if (req.Status != "IN_PROGRESS") throw new DepotConflictException("Đơn phải được check-in trước khi cân.");
            var checkin = await _db.PickupCheckIns.SingleOrDefaultAsync(c => c.PickupRequestId == requestId);
            if (string.IsNullOrWhiteSpace(req.CheckinImageUrl) || checkin is null || checkin.EmployeeId != collectorId)
                throw new DepotConflictException("Đơn chưa có bằng chứng check-in camera và GPS hợp lệ.");

            // Remove old items
            _db.PickupRequestItems.RemoveRange(req.Items);

            // Add new items
            decimal gross = 0;
            var newItems = new List<PickupRequestItem>();
            foreach (var item in validated)
            {
                var subTotal = Employee.CollectionValidation.SubTotal(item.WeightKg, item.PricePerKg);
                gross += subTotal;
                var row = new PickupRequestItem
                {
                    PickupRequestId = requestId,
                    MaterialType = Retrack.API.Services.Shared.MaterialCatalog.Normalize(item.MaterialType),
                    WeightKg = item.WeightKg,
                    PricePerKg = item.PricePerKg,
                    SubTotal = subTotal
                };
                newItems.Add(row);
                _db.PickupRequestItems.Add(row);
            }

            req.GrossAmount = gross;
            req.PlatformFeeAmount = Math.Round(gross * req.PlatformFeePercentage / 100, 2);
            req.NetAmount = gross - req.PlatformFeeAmount;
            // Bằng chứng check-in chỉ được ghi bởi luồng camera + GPS, không lấy URL từ request cân.
            req.Status = "WEIGHED";
            req.UpdatedAt = DateTime.UtcNow;

            // API cân cũ phải tạo cùng mốc SUBMITTED như luồng Employee mới.
            // Nếu thiếu mốc này, Seller xác nhận xong nhưng Employee không thể bàn giao sang Depot.
            var snapshotItems = newItems.OrderBy(i => i.MaterialType).ThenBy(i => i.Id)
                .Select(i => new ClassificationItemDto(i.Id, i.MaterialType, i.WeightKg, i.PricePerKg, i.SubTotal)).ToList();
            _db.EmployeeCollectionEvents.Add(new EmployeeCollectionEvent
            {
                PickupRequestId = requestId, EmployeeId = collectorId, Kind = "SUBMITTED",
                Revision = checkin.Revision, FromStatus = "IN_PROGRESS", ToStatus = "WEIGHED",
                SnapshotJson = JsonSerializer.Serialize(new Employee.CollectionSubmissionSnapshot(snapshotItems,
                    req.GrossAmount, req.PlatformFeePercentage, req.PlatformFeeAmount, req.NetAmount))
            });

            _db.Notifications.Add(new Notification
            {
                UserId = req.SellerId,
                PickupRequestId = requestId,
                Category = "PICKUP",
                TargetType = "PICKUP_REQUEST",
                TargetId = requestId,
                Title = "Đã có kết quả cân phế liệu",
                Message = $"Đơn #{requestId.ToString()[..8].ToUpper()} đã được cân xong. Tổng thực nhận: {req.NetAmount:N0} đ. Vui lòng kiểm tra và xác nhận.",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> ConfirmBySellerAsync(Guid requestId, Guid sellerId)
        {
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            if (req.SellerId != sellerId)
                throw new DepotForbiddenException();
            if (req.Status != "WEIGHED") throw new DepotConflictException("Đơn chưa chờ xác nhận kết quả cân.");
            req.Status = "SELLER_CONFIRMED";
            await _repo.UpdateAsync(req);
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> MarkPaymentSentAsync(Guid requestId, Guid ownerId, string paymentProofUrl)
        {
            var req = await _payments.MarkSentAsync(requestId, ownerId, paymentProofUrl);
            _db.Notifications.Add(new Notification
            {
                UserId = req.SellerId,
                PickupRequestId = req.Id,
                Category = "PAYMENT",
                TargetType = "PICKUP_REQUEST",
                TargetId = req.Id,
                Title = "Kho đã chuyển khoản thanh toán",
                Message = $"Kho đã gửi thanh toán cho đơn #{req.Id.ToString()[..8].ToUpper()}. Vui lòng kiểm tra tài khoản và xác nhận nhận tiền.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> MarkDoneAsync(Guid requestId, Guid sellerId)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var locked = await _db.PickupRequests.FromSqlInterpolated($"SELECT * FROM pickup_requests WHERE id = {requestId} FOR UPDATE")
                .SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            await _db.Entry(locked).ReloadAsync();
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            if (req.SellerId != sellerId) throw new DepotForbiddenException();
            if (req.Status == "DONE") return await MapToDto(req);
            if (req.Status != "PAYMENT_SENT") throw new DepotConflictException("Chủ kho chưa xác nhận chuyển tiền.");
            req.Status = "DONE";
            req.StockReceivedAt ??= DateTime.UtcNow;
            req.UpdatedAt = DateTime.UtcNow;
            if (req.TargetDepotId.HasValue)
            {
                var owner = await _db.Depots.Where(x => x.Id == req.TargetDepotId.Value).Select(x => (Guid?)x.OwnerId).SingleOrDefaultAsync();
                if (owner.HasValue)
                {
                    var notice = Notification.ForDepot(owner.Value, req.TargetDepotId.Value, "PAYMENT", "Người bán đã xác nhận nhận tiền",
                        $"Đơn {req.Id} đã hoàn tất sau khi người bán xác nhận nhận tiền. Kho có thể kiểm tra giao dịch và hàng đã nhập.");
                    notice.PickupRequestId = req.Id; _db.Notifications.Add(notice);
                }
            }
            await _repo.UpdateAsync(req);
            await transaction.CommitAsync();
            return await MapToDto(req);
        }

        private async Task<PickupRequestDto> MapToDto(PickupRequest r)
        {
            // Load relations if not loaded
            if (r.Seller == null)
                await _db.Entry(r).Reference(x => x.Seller).LoadAsync();
            if (r.TargetDepot == null && r.TargetDepotId != null)
                await _db.Entry(r).Reference(x => x.TargetDepot).LoadAsync();
            if (!r.Items.Any())
                await _db.Entry(r).Collection(x => x.Items).LoadAsync();

            // Load review if exists
            var review = await _db.SellerDepotReviews
                .FirstOrDefaultAsync(rv => rv.PickupRequestId == r.Id);

            return new PickupRequestDto
            {
                Id = r.Id,
                SellerId = r.SellerId,
                SellerName = r.Seller?.FullName ?? "",
                TargetDepotId = r.TargetDepotId,
                DepotName = r.TargetDepot?.Name,
                Description = r.Description,
                Address = r.Address,
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                PreferredDatetime = r.PreferredDatetime,
                RequestImageUrl = r.RequestImageUrl,
                Status = r.Status,
                GrossAmount = r.GrossAmount,
                NetAmount = r.NetAmount,
                PaymentProofUrl = r.PaymentProofUrl,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                Items = r.Items.Select(i => new PickupRequestItemDto
                {
                    MaterialType = i.MaterialType,
                    WeightKg = i.WeightKg,
                    PricePerKg = i.PricePerKg,
                    SubTotal = i.SubTotal
                }).ToList(),
                ReviewRating = review?.Rating,
                ReviewComment = review?.Comment,
                ReviewCreatedAt = review?.CreatedAt
            };
        }
    }
}
