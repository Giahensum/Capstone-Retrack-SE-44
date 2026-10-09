using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
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
            req.AcceptedCollectorId = collectorId;
            req.Status = "SCHEDULED";
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
            if (string.IsNullOrWhiteSpace(req.CheckinImageUrl) || !await _db.PickupCheckIns.AnyAsync(c => c.PickupRequestId == requestId))
                throw new DepotConflictException("Đơn chưa có bằng chứng check-in camera và GPS hợp lệ.");

            // Remove old items
            _db.PickupRequestItems.RemoveRange(req.Items);

            // Add new items
            decimal gross = 0;
            foreach (var item in validated)
            {
                var subTotal = Employee.CollectionValidation.SubTotal(item.WeightKg, item.PricePerKg);
                gross += subTotal;
                _db.PickupRequestItems.Add(new PickupRequestItem
                {
                    PickupRequestId = requestId,
                    MaterialType = Retrack.API.Services.Shared.MaterialCatalog.Normalize(item.MaterialType),
                    WeightKg = item.WeightKg,
                    PricePerKg = item.PricePerKg,
                    SubTotal = subTotal
                });
            }

            req.GrossAmount = gross;
            req.PlatformFeeAmount = Math.Round(gross * req.PlatformFeePercentage / 100, 2);
            req.NetAmount = gross - req.PlatformFeeAmount;
            // Bằng chứng check-in chỉ được ghi bởi luồng camera + GPS, không lấy URL từ request cân.
            req.Status = "WEIGHED";
            req.UpdatedAt = DateTime.UtcNow;

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
            => await MapToDto(await _payments.MarkSentAsync(requestId, ownerId, paymentProofUrl));

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
            req.UpdatedAt = DateTime.UtcNow;
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
