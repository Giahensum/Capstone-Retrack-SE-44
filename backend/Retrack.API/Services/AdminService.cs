using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Interfaces;

namespace Retrack.API.Services
{
    public interface IAdminService
    {
        // Dashboard
        Task<AdminDashboardStatsDto> GetDashboardStatsAsync();

        // Users
        Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? keyword, string? role, bool? isActive, int page, int pageSize);
        Task<UserDetailDto?> GetUserDetailAsync(Guid id);
        Task<UserDetailDto> CreateUserAsync(CreateUserDto dto, Guid actorId);
        Task<UserDetailDto> UpdateUserAsync(Guid id, UpdateUserDto dto, Guid actorId);
        Task<UserDetailDto> SetUserActiveAsync(Guid id, bool isActive, Guid actorId);
        Task DeleteUserAsync(Guid id, Guid actorId);

        // Market prices
        Task<List<MarketPriceDto>> GetMarketPricesAsync(string? materialType);
        Task<MarketPriceDto> CreateMarketPriceAsync(UpsertMarketPriceDto dto, Guid actorId);
        Task<MarketPriceDto> UpdateMarketPriceAsync(Guid id, UpsertMarketPriceDto dto, Guid actorId);
        Task DeleteMarketPriceAsync(Guid id, Guid actorId);

        // Fee config
        Task<FeeConfigDto> GetFeeConfigAsync();
        Task<FeeConfigDto> UpdateFeeConfigAsync(UpdateFeeConfigDto dto, Guid actorId);

        // Revenue & transactions
        Task<RevenueReportDto> GetRevenueReportAsync(DateTime from, DateTime to, string groupBy);
        Task<PagedResult<TransactionHistoryItemDto>> GetTransactionsAsync(DateTime? from, DateTime? to, string? sourceType, int page, int pageSize);

        // Invoices
        Task<List<PlatformInvoiceDto>> GenerateMonthlyInvoicesAsync(int year, int month, Guid actorId);
        Task<PagedResult<PlatformInvoiceDto>> GetInvoicesAsync(string? status, int page, int pageSize);
        Task<PlatformInvoiceDto> MarkInvoicePaidAsync(Guid id, Guid actorId);
        Task ResendInvoiceReminderAsync(Guid id);

        // Audit logs
        Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(string? entityName, Guid? userId, int page, int pageSize);
    }

    public class AdminService : IAdminService
    {
        private static readonly string[] ValidRoles = { "SELLER", "DEPOT_OWNER", "DEPOT_EMPLOYEE", "DRIVER", "FACTORY", "ADMIN" };

        private readonly IUserRepository _userRepo;
        private readonly IMarketPriceRepository _priceRepo;
        private readonly IAuditLogRepository _auditRepo;
        private readonly IPlatformInvoiceRepository _invoiceRepo;
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _db;

        public AdminService(
            IUserRepository userRepo,
            IMarketPriceRepository priceRepo,
            IAuditLogRepository auditRepo,
            IPlatformInvoiceRepository invoiceRepo,
            INotificationService notificationService,
            AppDbContext db)
        {
            _userRepo = userRepo;
            _priceRepo = priceRepo;
            _auditRepo = auditRepo;
            _invoiceRepo = invoiceRepo;
            _notificationService = notificationService;
            _db = db;
        }

        // ── Dashboard ──────────────────────────────────────────────────
        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
        {
            var usersByRole = await _userRepo.CountByRoleAsync();
            var now = DateTime.UtcNow;

            return new AdminDashboardStatsDto
            {
                TotalUsers = usersByRole.Values.Sum(),
                UsersByRole = usersByRole,
                TotalPickupRequests = await _db.PickupRequests.CountAsync(),
                CompletedPickupRequests = await _db.PickupRequests.CountAsync(p => p.Status == "DONE"),
                TotalInventoryBatches = await _db.InventoryBatches.CountAsync(),
                TotalPlatformRevenue = await _db.PlatformTransactions.SumAsync(t => t.FeeAmount),
                RevenueThisMonth = await _db.PlatformTransactions
                    .Where(t => t.CreatedAt.Year == now.Year && t.CreatedAt.Month == now.Month)
                    .SumAsync(t => t.FeeAmount)
            };
        }

        // ── Users ──────────────────────────────────────────────────────
        public async Task<PagedResult<UserListItemDto>> SearchUsersAsync(string? keyword, string? role, bool? isActive, int page, int pageSize)
        {
            var (items, total) = await _userRepo.SearchAsync(keyword, role, isActive, page, pageSize);
            return new PagedResult<UserListItemDto>
            {
                Items = items.Select(MapListItem).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<UserDetailDto?> GetUserDetailAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null) return null;

            var dto = MapDetail(user);

            switch (user.Role)
            {
                case "DEPOT_OWNER":
                    var depot = await _db.Depots.FirstOrDefaultAsync(d => d.OwnerId == id);
                    if (depot != null)
                        dto.DepotProfile = new DepotProfileSummaryDto { DepotId = depot.Id, Name = depot.Name, Address = depot.Address, Rating = depot.Rating };
                    break;
                case "FACTORY":
                    var factory = await _db.Factories.FirstOrDefaultAsync(f => f.OwnerId == id);
                    if (factory != null)
                        dto.FactoryProfile = new FactoryProfileSummaryDto { FactoryId = factory.Id, Name = factory.Name, Address = factory.Address, Rating = factory.Rating };
                    break;
                case "DEPOT_EMPLOYEE":
                case "DRIVER":
                    var staff = await _db.DepotStaffs.Include(s => s.Depot).FirstOrDefaultAsync(s => s.UserId == id);
                    if (staff != null)
                        dto.DepotStaffProfile = new DepotStaffProfileSummaryDto { DepotId = staff.DepotId, DepotName = staff.Depot.Name, StaffType = staff.StaffType, IsActive = staff.IsActive };
                    break;
            }

            return dto;
        }

        public async Task<UserDetailDto> CreateUserAsync(CreateUserDto dto, Guid actorId)
        {
            if (!ValidRoles.Contains(dto.Role))
                throw new ArgumentException("Vai trò không hợp lệ.");

            var existing = await _userRepo.GetByEmailAsync(dto.Email);
            if (existing != null)
                throw new InvalidOperationException("Email đã được sử dụng.");

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Phone = dto.Phone,
                Role = dto.Role,
                IsActive = true
            };
            await _userRepo.CreateAsync(user);

            await LogAsync(actorId, "CREATE", "User", user.Id, null, new { user.Email, user.FullName, user.Role });
            return MapDetail(user);
        }

        public async Task<UserDetailDto> UpdateUserAsync(Guid id, UpdateUserDto dto, Guid actorId)
        {
            var user = await _userRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

            var oldData = new { user.FullName, user.Phone };
            user.FullName = dto.FullName;
            user.Phone = dto.Phone;
            await _userRepo.UpdateAsync(user);

            await LogAsync(actorId, "UPDATE", "User", user.Id, oldData, new { user.FullName, user.Phone });
            return MapDetail(user);
        }

        public async Task<UserDetailDto> SetUserActiveAsync(Guid id, bool isActive, Guid actorId)
        {
            var user = await _userRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

            if (!isActive && user.Role == "ADMIN")
            {
                var activeAdmins = await _db.Users.CountAsync(u => u.Role == "ADMIN" && u.IsActive && u.Id != id);
                if (activeAdmins == 0)
                    throw new InvalidOperationException("Không thể vô hiệu hóa tài khoản Admin cuối cùng.");
            }

            var wasActive = user.IsActive;
            user.IsActive = isActive;
            await _userRepo.UpdateAsync(user);

            await LogAsync(actorId, isActive ? "ACTIVATE" : "DEACTIVATE", "User", user.Id, new { IsActive = wasActive }, new { user.IsActive });
            return MapDetail(user);
        }

        public async Task DeleteUserAsync(Guid id, Guid actorId)
        {
            var user = await _userRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

            var hasDependents =
                await _db.Depots.AnyAsync(d => d.OwnerId == id) ||
                await _db.Factories.AnyAsync(f => f.OwnerId == id) ||
                await _db.DepotStaffs.AnyAsync(s => s.UserId == id) ||
                await _db.PickupRequests.AnyAsync(p => p.SellerId == id || p.AcceptedCollectorId == id) ||
                await _db.TransportJobs.AnyAsync(t => t.DriverId == id);

            if (hasDependents)
                throw new InvalidOperationException("Không thể xóa: tài khoản có dữ liệu liên quan. Hãy vô hiệu hóa thay vì xóa.");

            await _userRepo.DeleteAsync(user);
            await LogAsync(actorId, "DELETE", "User", id, new { user.Email, user.Role }, null);
        }

        private static UserListItemDto MapListItem(User u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            Phone = u.Phone,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        };

        private static UserDetailDto MapDetail(User u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            Phone = u.Phone,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        };

        // ── Market Prices ──────────────────────────────────────────────
        public async Task<List<MarketPriceDto>> GetMarketPricesAsync(string? materialType)
        {
            var prices = await _priceRepo.GetAllAsync(materialType);
            return prices.Select(MapPrice).ToList();
        }

        public async Task<MarketPriceDto> CreateMarketPriceAsync(UpsertMarketPriceDto dto, Guid actorId)
        {
            var price = new MarketPrice
            {
                MaterialType = dto.MaterialType,
                PricePerKg = dto.PricePerKg,
                EffectiveDate = dto.EffectiveDate,
                Source = dto.Source
            };
            await _priceRepo.CreateAsync(price);
            await LogAsync(actorId, "CREATE", "MarketPrice", price.Id, null, dto);
            return MapPrice(price);
        }

        public async Task<MarketPriceDto> UpdateMarketPriceAsync(Guid id, UpsertMarketPriceDto dto, Guid actorId)
        {
            var price = await _priceRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy giá tham khảo.");

            var oldData = new { price.MaterialType, price.PricePerKg, price.EffectiveDate, price.Source };
            price.MaterialType = dto.MaterialType;
            price.PricePerKg = dto.PricePerKg;
            price.EffectiveDate = dto.EffectiveDate;
            price.Source = dto.Source;
            await _priceRepo.UpdateAsync(price);

            await LogAsync(actorId, "UPDATE", "MarketPrice", price.Id, oldData, dto);
            return MapPrice(price);
        }

        public async Task DeleteMarketPriceAsync(Guid id, Guid actorId)
        {
            var price = await _priceRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy giá tham khảo.");
            await _priceRepo.DeleteAsync(price);
            await LogAsync(actorId, "DELETE", "MarketPrice", id, new { price.MaterialType, price.PricePerKg }, null);
        }

        private static MarketPriceDto MapPrice(MarketPrice p) => new()
        {
            Id = p.Id,
            MaterialType = p.MaterialType,
            PricePerKg = p.PricePerKg,
            EffectiveDate = p.EffectiveDate,
            Source = p.Source,
            CreatedAt = p.CreatedAt
        };

        // ── Fee Config ─────────────────────────────────────────────────
        private const string FeeConfigKey = "PLATFORM_FEE_PERCENTAGE";

        public async Task<FeeConfigDto> GetFeeConfigAsync()
        {
            var config = await _db.SystemConfigs.FindAsync(FeeConfigKey);
            return new FeeConfigDto
            {
                PlatformFeePercentage = decimal.Parse(config?.ConfigValue ?? "1.00", CultureInfo.InvariantCulture),
                UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public async Task<FeeConfigDto> UpdateFeeConfigAsync(UpdateFeeConfigDto dto, Guid actorId)
        {
            if (dto.PlatformFeePercentage < 0 || dto.PlatformFeePercentage > 100)
                throw new ArgumentException("Phí nền tảng phải trong khoảng 0-100%.");

            var config = await _db.SystemConfigs.FindAsync(FeeConfigKey);
            var oldValue = config?.ConfigValue;

            if (config == null)
            {
                config = new SystemConfig { ConfigKey = FeeConfigKey, Description = "Phí nền tảng (%)" };
                _db.SystemConfigs.Add(config);
            }
            config.ConfigValue = dto.PlatformFeePercentage.ToString(CultureInfo.InvariantCulture);
            config.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAsync(actorId, "UPDATE", "SystemConfig", null, new { Value = oldValue }, new { config.ConfigValue });
            return new FeeConfigDto { PlatformFeePercentage = dto.PlatformFeePercentage, UpdatedAt = config.UpdatedAt };
        }

        // ── Revenue & Transactions ─────────────────────────────────────
        public async Task<RevenueReportDto> GetRevenueReportAsync(DateTime from, DateTime to, string groupBy)
        {
            var transactions = await _db.PlatformTransactions
                .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
                .ToListAsync();

            var format = groupBy?.ToLower() switch
            {
                "month" => "yyyy-MM",
                "week" => "yyyy-'W'ww",
                _ => "yyyy-MM-dd"
            };

            var points = transactions
                .GroupBy(t => t.CreatedAt.ToString(format, CultureInfo.InvariantCulture))
                .Select(g => new RevenuePointDto { PeriodLabel = g.Key, Amount = g.Sum(t => t.FeeAmount) })
                .OrderBy(p => p.PeriodLabel)
                .ToList();

            return new RevenueReportDto { TotalRevenue = transactions.Sum(t => t.FeeAmount), Points = points };
        }

        public async Task<PagedResult<TransactionHistoryItemDto>> GetTransactionsAsync(DateTime? from, DateTime? to, string? sourceType, int page, int pageSize)
        {
            var query = _db.PlatformTransactions.AsQueryable();
            if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value);
            if (!string.IsNullOrWhiteSpace(sourceType)) query = query.Where(t => t.SourceType == sourceType);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var payerNames = await ResolvePayerNamesAsync(items);

            var dtos = items.Select(t => new TransactionHistoryItemDto
            {
                Id = t.Id,
                SourceType = t.SourceType,
                SourceId = t.SourceId,
                PayerName = payerNames.GetValueOrDefault((t.SourceType, t.SourceId)),
                FeeAmount = t.FeeAmount,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();

            return new PagedResult<TransactionHistoryItemDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        /// <summary>
        /// PlatformTransaction has no PayerId column (shared table other teammates are still building on) —
        /// resolve the paying account by joining SourceType/SourceId to the relevant owner instead.
        /// </summary>
        private async Task<Dictionary<(string SourceType, Guid SourceId), Guid>> ResolvePayerIdsAsync(IEnumerable<PlatformTransaction> transactions)
        {
            var pickupIds = transactions.Where(t => t.SourceType == "PICKUP_REQUEST").Select(t => t.SourceId).Distinct().ToList();
            var batchIds = transactions.Where(t => t.SourceType == "BATCH_ORDER").Select(t => t.SourceId).Distinct().ToList();

            var pickupOwners = await _db.PickupRequests
                .Where(p => pickupIds.Contains(p.Id) && p.TargetDepotId != null)
                .Select(p => new { p.Id, OwnerId = p.TargetDepot!.OwnerId })
                .ToListAsync();

            var batchOwners = await _db.BatchQualityChecks
                .Where(b => batchIds.Contains(b.BatchId))
                .Select(b => new { b.BatchId, OwnerId = b.Factory.OwnerId })
                .ToListAsync();

            var map = new Dictionary<(string, Guid), Guid>();
            foreach (var p in pickupOwners) map[("PICKUP_REQUEST", p.Id)] = p.OwnerId;
            foreach (var b in batchOwners) map[("BATCH_ORDER", b.BatchId)] = b.OwnerId;
            return map;
        }

        private async Task<Dictionary<(string, Guid), string?>> ResolvePayerNamesAsync(IEnumerable<PlatformTransaction> transactions)
        {
            var payerIds = await ResolvePayerIdsAsync(transactions);
            var userIds = payerIds.Values.Distinct().ToList();
            var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

            return payerIds.ToDictionary(kv => kv.Key, kv => names.GetValueOrDefault(kv.Value));
        }

        // ── Platform Invoices ──────────────────────────────────────────
        public async Task<List<PlatformInvoiceDto>> GenerateMonthlyInvoicesAsync(int year, int month, Guid actorId)
        {
            var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = periodStart.AddMonths(1);

            var transactions = await _db.PlatformTransactions
                .Where(t => t.CreatedAt >= periodStart && t.CreatedAt < periodEnd)
                .ToListAsync();

            var payerIds = await ResolvePayerIdsAsync(transactions);

            var totalsByPayer = transactions
                .Where(t => payerIds.ContainsKey((t.SourceType, t.SourceId)))
                .GroupBy(t => payerIds[(t.SourceType, t.SourceId)])
                .Select(g => new { PayerId = g.Key, Total = g.Sum(t => t.FeeAmount) });

            var created = new List<PlatformInvoiceDto>();
            foreach (var group in totalsByPayer)
            {
                var existing = await _invoiceRepo.GetByPayerAndPeriodAsync(group.PayerId, year, month);
                if (existing != null) continue; // already invoiced this period

                var invoice = new PlatformInvoice
                {
                    PayerId = group.PayerId,
                    PeriodYear = year,
                    PeriodMonth = month,
                    TotalFeeAmount = group.Total,
                    Status = "PENDING"
                };
                await _invoiceRepo.CreateAsync(invoice);

                await _notificationService.SendAsync(group.PayerId,
                    $"Hóa đơn phí nền tảng tháng {month}/{year}",
                    $"Bạn có hóa đơn phí nền tảng {group.Total:N0}đ cho tháng {month}/{year}. Vui lòng thanh toán.");

                created.Add(await MapInvoiceAsync(invoice));
            }

            await LogAsync(actorId, "GENERATE", "PlatformInvoice", null, null, new { year, month, count = created.Count });
            return created;
        }

        public async Task<PagedResult<PlatformInvoiceDto>> GetInvoicesAsync(string? status, int page, int pageSize)
        {
            var (items, total) = await _invoiceRepo.SearchAsync(status, page, pageSize);
            return new PagedResult<PlatformInvoiceDto>
            {
                Items = items.Select(MapInvoice).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PlatformInvoiceDto> MarkInvoicePaidAsync(Guid id, Guid actorId)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

            invoice.Status = "PAID";
            invoice.PaidAt = DateTime.UtcNow;
            await _invoiceRepo.UpdateAsync(invoice);

            await LogAsync(actorId, "MARK_PAID", "PlatformInvoice", invoice.Id, null, new { invoice.Status, invoice.PaidAt });
            return await MapInvoiceAsync(invoice);
        }

        public async Task ResendInvoiceReminderAsync(Guid id)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

            await _notificationService.SendAsync(invoice.PayerId,
                $"Nhắc thanh toán hóa đơn phí nền tảng tháng {invoice.PeriodMonth}/{invoice.PeriodYear}",
                $"Hóa đơn {invoice.TotalFeeAmount:N0}đ vẫn chưa được thanh toán. Vui lòng thanh toán sớm.");
        }

        private PlatformInvoiceDto MapInvoice(PlatformInvoice i) => new()
        {
            Id = i.Id,
            PayerId = i.PayerId,
            PayerName = i.Payer?.FullName ?? string.Empty,
            PeriodYear = i.PeriodYear,
            PeriodMonth = i.PeriodMonth,
            TotalFeeAmount = i.TotalFeeAmount,
            Status = i.Status,
            PaidAt = i.PaidAt,
            CreatedAt = i.CreatedAt
        };

        private async Task<PlatformInvoiceDto> MapInvoiceAsync(PlatformInvoice i)
        {
            if (i.Payer == null)
                await _db.Entry(i).Reference(x => x.Payer).LoadAsync();
            return MapInvoice(i);
        }

        // ── Audit Logs ─────────────────────────────────────────────────
        public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(string? entityName, Guid? userId, int page, int pageSize)
        {
            var (items, total) = await _auditRepo.SearchAsync(entityName, userId, page, pageSize);
            return new PagedResult<AuditLogDto>
            {
                Items = items.Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.User?.FullName,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    OldData = a.OldData,
                    NewData = a.NewData,
                    CreatedAt = a.CreatedAt
                }).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private async Task LogAsync(Guid? actorId, string action, string entityName, Guid? entityId, object? oldData, object? newData)
        {
            await _auditRepo.CreateAsync(new AuditLog
            {
                UserId = actorId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldData = oldData == null ? null : JsonSerializer.Serialize(oldData),
                NewData = newData == null ? null : JsonSerializer.Serialize(newData)
            });
        }
    }
}
