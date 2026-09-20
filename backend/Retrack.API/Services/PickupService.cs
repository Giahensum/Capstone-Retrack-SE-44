using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories;

namespace Retrack.API.Services
{
    public interface IPickupService
    {
        Task<PickupRequestDto> CreateAsync(Guid sellerId, CreatePickupRequestDto dto);
        Task<PickupRequestDto?> GetByIdAsync(Guid id);
        Task<List<PickupRequestDto>> GetBySellerAsync(Guid sellerId);
        Task<List<PickupRequestDto>> GetPendingForDepotAsync(Guid depotId);
        Task<PickupRequestDto> AcceptRequestAsync(Guid requestId, Guid collectorId);
        Task<PickupRequestDto> WeighAndUpdateAsync(Guid requestId, List<WeighItemDto> items, string? checkinImageUrl);
        Task<PickupRequestDto> ConfirmBySellerAsync(Guid requestId, Guid sellerId);
        Task<PickupRequestDto> MarkPaymentSentAsync(Guid requestId, string paymentProofUrl);
        Task<PickupRequestDto> MarkDoneAsync(Guid requestId);
    }

    public class PickupService : IPickupService
    {
        private readonly IPickupRequestRepository _repo;
        private readonly AppDbContext _db;

        public PickupService(IPickupRequestRepository repo, AppDbContext db)
        {
            _repo = repo;
            _db = db;
        }

        public async Task<PickupRequestDto> CreateAsync(Guid sellerId, CreatePickupRequestDto dto)
        {
            // Get platform fee from system config
            var feeConfig = await _db.SystemConfigs.FindAsync("PLATFORM_FEE_PERCENTAGE");
            var feePercent = decimal.Parse(feeConfig?.ConfigValue ?? "1.00");

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

        public async Task<PickupRequestDto?> GetByIdAsync(Guid id)
        {
            var req = await _repo.GetByIdAsync(id);
            return req == null ? null : await MapToDto(req);
        }

        public async Task<List<PickupRequestDto>> GetBySellerAsync(Guid sellerId)
        {
            var requests = await _repo.GetBySellerIdAsync(sellerId);
            var result = new List<PickupRequestDto>();
            foreach (var r in requests) result.Add(await MapToDto(r));
            return result;
        }

        public async Task<List<PickupRequestDto>> GetPendingForDepotAsync(Guid depotId)
        {
            var requests = await _repo.GetByDepotIdAsync(depotId);
            var result = new List<PickupRequestDto>();
            foreach (var r in requests) result.Add(await MapToDto(r));
            return result;
        }

        public async Task<PickupRequestDto> AcceptRequestAsync(Guid requestId, Guid collectorId)
        {
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            req.AcceptedCollectorId = collectorId;
            req.Status = "SCHEDULED";
            await _repo.UpdateAsync(req);
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> WeighAndUpdateAsync(Guid requestId, List<WeighItemDto> items, string? checkinImageUrl)
        {
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");

            // Remove old items
            _db.PickupRequestItems.RemoveRange(req.Items);

            // Add new items
            decimal gross = 0;
            foreach (var item in items)
            {
                var subTotal = item.WeightKg * item.PricePerKg;
                gross += subTotal;
                _db.PickupRequestItems.Add(new PickupRequestItem
                {
                    PickupRequestId = requestId,
                    MaterialType = item.MaterialType,
                    WeightKg = item.WeightKg,
                    PricePerKg = item.PricePerKg,
                    SubTotal = subTotal
                });
            }

            req.GrossAmount = gross;
            req.PlatformFeeAmount = Math.Round(gross * req.PlatformFeePercentage / 100, 2);
            req.NetAmount = gross - req.PlatformFeeAmount;
            req.CheckinImageUrl = checkinImageUrl;
            req.Status = "WEIGHED";

            await _db.SaveChangesAsync();
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> ConfirmBySellerAsync(Guid requestId, Guid sellerId)
        {
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            if (req.SellerId != sellerId)
                throw new UnauthorizedAccessException("Không có quyền.");
            req.Status = "SELLER_CONFIRMED";
            await _repo.UpdateAsync(req);
            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> MarkPaymentSentAsync(Guid requestId, string paymentProofUrl)
        {
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            req.PaymentProofUrl = paymentProofUrl;
            req.Status = "PAYMENT_SENT";
            await _repo.UpdateAsync(req);

            // Record platform transaction
            _db.PlatformTransactions.Add(new PlatformTransaction
            {
                SourceType = "PICKUP_REQUEST",
                SourceId = requestId,
                FeeAmount = req.PlatformFeeAmount,
                Description = $"Phí thu gom phế liệu #{requestId}"
            });
            await _db.SaveChangesAsync();

            return await MapToDto(req);
        }

        public async Task<PickupRequestDto> MarkDoneAsync(Guid requestId)
        {
            var req = await _repo.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
            req.Status = "DONE";
            await _repo.UpdateAsync(req);
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
                Status = r.Status,
                GrossAmount = r.GrossAmount,
                NetAmount = r.NetAmount,
                CreatedAt = r.CreatedAt,
                Items = r.Items.Select(i => new PickupRequestItemDto
                {
                    MaterialType = i.MaterialType,
                    WeightKg = i.WeightKg,
                    PricePerKg = i.PricePerKg,
                    SubTotal = i.SubTotal
                }).ToList()
            };
        }
    }
}

