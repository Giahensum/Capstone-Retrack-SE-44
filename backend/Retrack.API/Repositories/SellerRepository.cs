using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
using Retrack.API.Services.Shared;

namespace Retrack.API.Repositories
{
    public class SellerRepository : ISellerRepository
    {
        private readonly AppDbContext _db;

        public SellerRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<User?> GetSellerProfileAsync(Guid sellerId)
        {
            return await _db.Users.FindAsync(sellerId);
        }

        public async Task UpdateSellerProfileAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task<PickupRequest?> GetPickupRequestByIdAsync(Guid requestId)
        {
            return await _db.PickupRequests.FindAsync(requestId);
        }

        public async Task UpdatePickupRequestAsync(PickupRequest request)
        {
            request.UpdatedAt = DateTime.UtcNow;
            _db.PickupRequests.Update(request);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> HasSellerReviewedRequestAsync(Guid requestId)
        {
            return await _db.SellerDepotReviews.AnyAsync(r => r.PickupRequestId == requestId);
        }

        public async Task AddReviewAsync(SellerDepotReview review)
        {
            review.CreatedAt = DateTime.UtcNow;
            _db.SellerDepotReviews.Add(review);
            await _db.SaveChangesAsync();
        }

        public async Task<List<NearbyDepotDto>> GetNearbyDepotsAsync(decimal? lat, decimal? lng)
        {
            var rawDepots = await _db.Depots
                .Include(d => d.Owner)
                .Where(d => d.Owner.IsActive)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.Address,
                    d.Latitude,
                    d.Longitude,
                    OwnerName = d.Owner.FullName,
                    AvgRating = _db.SellerDepotReviews
                        .Where(r => r.DepotId == d.Id && r.Rating != null)
                        .Average(r => (double?)r.Rating) ?? 0,
                    TotalDone = _db.PickupRequests
                        .Count(r => r.TargetDepotId == d.Id && r.Status == "DONE")
                })
                .ToListAsync();

            var prices = await _db.MarketPrices
                .GroupBy(p => p.MaterialType)
                .Select(g => g.OrderByDescending(p => p.EffectiveDate).FirstOrDefault())
                .ToListAsync();

            var priceDict = prices
                .Where(p => p != null)
                .ToDictionary(p => p!.MaterialType.ToString(), p => p!.PricePerKg);

            if (lat.HasValue && lng.HasValue)
            {
                return rawDepots
                    .Select(d => new NearbyDepotDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Address = d.Address,
                        Latitude = d.Latitude,
                        Longitude = d.Longitude,
                        OwnerName = d.OwnerName,
                        AvgRating = d.AvgRating,
                        TotalDone = d.TotalDone,
                        Prices = priceDict,
                        DistanceKm = (d.Latitude.HasValue && d.Longitude.HasValue)
                            ? GeoUtils.CalculateDistanceKm(
                                (double)lat.Value, (double)lng.Value,
                                (double)d.Latitude.Value, (double)d.Longitude.Value)
                            : null
                    })
                    .OrderBy(d => d.DistanceKm ?? double.MaxValue)
                    .ToList();
            }

            return rawDepots
                .Select(d => new NearbyDepotDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Address = d.Address,
                    Latitude = d.Latitude,
                    Longitude = d.Longitude,
                    OwnerName = d.OwnerName,
                    AvgRating = d.AvgRating,
                    TotalDone = d.TotalDone,
                    Prices = priceDict,
                    DistanceKm = null
                })
                .ToList();
        }

        public async Task<SellerStatsDto> GetSellerStatsAsync(Guid sellerId)
        {
            var vietnamToday = DateTime.UtcNow.AddHours(7);
            var monthStart = new DateTime(vietnamToday.Year, vietnamToday.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);
            var nextMonthStart = monthStart.AddMonths(1);

            var requests = _db.PickupRequests.AsNoTracking().Where(r => r.SellerId == sellerId);

            return new SellerStatsDto
            {
                Total = await requests.CountAsync(),
                Pending = await requests.CountAsync(r => r.Status == "PENDING"),
                InProgress = await requests.CountAsync(r =>
                    r.Status == "SCHEDULED" ||
                    r.Status == "IN_PROGRESS" ||
                    r.Status == "WEIGHED" ||
                    r.Status == "SELLER_CONFIRMED" ||
                    r.Status == "AWAITING_PAYMENT" ||
                    r.Status == "PAYMENT_SENT"),
                Done = await requests.CountAsync(r => r.Status == "DONE"),
                TotalIncome = await requests.Where(r => r.Status == "DONE").SumAsync(r => r.NetAmount),
                MonthlyIncome = await requests.Where(r => r.Status == "DONE" && r.UpdatedAt >= monthStart && r.UpdatedAt < nextMonthStart).SumAsync(r => r.NetAmount)
            };
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
