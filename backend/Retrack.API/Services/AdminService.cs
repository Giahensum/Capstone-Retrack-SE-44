using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.Models;
using Retrack.API.Models.Enums;
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
        Task<PlatformPaymentAccountDto> GetPaymentAccountAsync();
        Task<PlatformPaymentAccountDto> UpdatePaymentAccountAsync(PlatformPaymentAccountDto dto, Guid actorId);

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
        private readonly TimeProvider _clock;

        public AdminService(
            IUserRepository userRepo,
            IMarketPriceRepository priceRepo,
            IAuditLogRepository auditRepo,
            IPlatformInvoiceRepository invoiceRepo,
            INotificationService notificationService,
            AppDbContext db, TimeProvider? clock = null)
        {
            _userRepo = userRepo;
            _priceRepo = priceRepo;
            _auditRepo = auditRepo;
            _invoiceRepo = invoiceRepo;
            _notificationService = notificationService;
            _db = db;
            _clock = clock ?? TimeProvider.System;
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
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@') || dto.Email.Length > 255)
                throw new ArgumentException("Email không hợp lệ.");
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                throw new ArgumentException("Mật khẩu phải có ít nhất 6 ký tự.");
            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new ArgumentException("Họ tên không được để trống.");

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

            if (string.IsNullOrWhiteSpace(dto.FullName))
                throw new ArgumentException("Họ tên không được để trống.");

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

            if (id == actorId)
                throw new InvalidOperationException("Không thể tự xóa tài khoản đang đăng nhập.");

            if (user.Role == "ADMIN" && user.IsActive)
            {
                var otherAdmins = await _db.Users.CountAsync(u => u.Role == "ADMIN" && u.IsActive && u.Id != id);
                if (otherAdmins == 0)
                    throw new InvalidOperationException("Không thể xóa tài khoản Admin cuối cùng.");
            }

            var hasDependents =
                await _db.Depots.AnyAsync(d => d.OwnerId == id) ||
                await _db.Factories.AnyAsync(f => f.OwnerId == id) ||
                await _db.DepotStaffs.AnyAsync(s => s.UserId == id) ||
                await _db.PickupRequests.AnyAsync(p => p.SellerId == id || p.AcceptedCollectorId == id) ||
                await _db.TransportJobs.AnyAsync(t => t.DriverId == id) ||
                // Giữ người tạo/duyệt trong lịch sử kho kể cả khi liên kết nhân viên đã được gỡ.
                await _db.DepotStockReceipts.AnyAsync(r => r.CreatedBy == id || r.ReviewedBy == id || r.PostedBy == id) ||
                await _db.DepotReuseSales.AnyAsync(s => s.CreatedBy == id) ||
                await _db.DepotBatchExpenses.AnyAsync(e => e.CreatedBy == id);

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
            MaterialType? filter = string.IsNullOrWhiteSpace(materialType) ? null : ParseMaterialType(materialType);
            var prices = await _priceRepo.GetAllAsync(filter);
            return prices.Select(MapPrice).ToList();
        }

        /// <summary>
        /// MarketPrice.MaterialType là enum (lưu DB dạng string qua HasConversion), còn DTO nhận/trả string —
        /// parse tại biên service để giá trị sai trả 400 kèm thông báo thay vì lỗi hệ thống.
        /// </summary>
        private static MaterialType ParseMaterialType(string? value)
        {
            if (!Enum.TryParse<MaterialType>(value?.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ArgumentException($"Loại vật liệu không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<MaterialType>())}.");
            return parsed;
        }

        public async Task<MarketPriceDto> CreateMarketPriceAsync(UpsertMarketPriceDto dto, Guid actorId)
        {
            if (dto.PricePerKg <= 0)
                throw new ArgumentException("Giá phải lớn hơn 0.");
            var price = new MarketPrice
            {
                MaterialType = ParseMaterialType(dto.MaterialType),
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

            if (dto.PricePerKg <= 0)
                throw new ArgumentException("Giá phải lớn hơn 0.");
            var oldData = new { MaterialType = price.MaterialType.ToString(), price.PricePerKg, price.EffectiveDate, price.Source };
            price.MaterialType = ParseMaterialType(dto.MaterialType);
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
            await LogAsync(actorId, "DELETE", "MarketPrice", id, new { MaterialType = price.MaterialType.ToString(), price.PricePerKg }, null);
        }

        private static MarketPriceDto MapPrice(MarketPrice p) => new()
        {
            Id = p.Id,
            MaterialType = p.MaterialType.ToString(),
            PricePerKg = p.PricePerKg,
            EffectiveDate = p.EffectiveDate,
            Source = p.Source,
            CreatedAt = p.CreatedAt
        };

        // ── Fee Config ─────────────────────────────────────────────────
        private const string FeeConfigKey = "PLATFORM_FEE_PERCENTAGE";
        private static readonly string[] PaymentAccountKeys = ["PLATFORM_FEE_BANK_NAME", "PLATFORM_FEE_ACCOUNT_NUMBER", "PLATFORM_FEE_ACCOUNT_HOLDER"];

        public async Task<PlatformPaymentAccountDto> GetPaymentAccountAsync()
        {
            var settings = await _db.SystemConfigs.AsNoTracking().Where(c => PaymentAccountKeys.Contains(c.ConfigKey))
                .ToDictionaryAsync(c => c.ConfigKey, c => c.ConfigValue);
            return new(settings.GetValueOrDefault(PaymentAccountKeys[0], ""),
                settings.GetValueOrDefault(PaymentAccountKeys[1], ""), settings.GetValueOrDefault(PaymentAccountKeys[2], ""));
        }

        public async Task<PlatformPaymentAccountDto> UpdatePaymentAccountAsync(PlatformPaymentAccountDto dto, Guid actorId)
        {
            var bankName = dto.BankName?.Trim() ?? "";
            var accountNumber = dto.AccountNumber?.Trim() ?? "";
            var accountHolder = dto.AccountHolder?.Trim() ?? "";
            if (bankName.Length is < 2 or > 100 || accountHolder.Length is < 2 or > 120 ||
                !System.Text.RegularExpressions.Regex.IsMatch(accountNumber, "^[0-9]{4,30}$"))
                throw new ArgumentException("Cần nhập tên ngân hàng, số tài khoản 4–30 chữ số và tên chủ tài khoản hợp lệ.");
            var values = new[] { bankName, accountNumber, accountHolder };
            var settings = await _db.SystemConfigs.Where(c => PaymentAccountKeys.Contains(c.ConfigKey))
                .ToDictionaryAsync(c => c.ConfigKey);
            for (var index = 0; index < PaymentAccountKeys.Length; index++)
            {
                var key = PaymentAccountKeys[index];
                if (!settings.TryGetValue(key, out var config))
                {
                    config = new SystemConfig { ConfigKey = key };
                    _db.SystemConfigs.Add(config);
                }
                config.ConfigValue = values[index];
                config.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            await LogAsync(actorId, "UPDATE", "PlatformPaymentAccount", null, null, new { Configured = true });
            return new(bankName, accountNumber, accountHolder);
        }

        public async Task<FeeConfigDto> GetFeeConfigAsync()
        {
            var config = await _db.SystemConfigs.FindAsync(FeeConfigKey);
            return new FeeConfigDto
            {
                PlatformFeePercentage = decimal.Parse(config?.ConfigValue ?? "5.00", CultureInfo.InvariantCulture),
                UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public async Task<FeeConfigDto> UpdateFeeConfigAsync(UpdateFeeConfigDto dto, Guid actorId)
        {
            if (dto.PlatformFeePercentage < 0 || dto.PlatformFeePercentage > 100 || decimal.Round(dto.PlatformFeePercentage, 2) != dto.PlatformFeePercentage)
                throw new ArgumentException("Phí nền tảng phải từ 0 đến 100% và tối đa hai chữ số thập phân.");

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

            var points = transactions
                .GroupBy(t => PeriodLabel(t.CreatedAt, groupBy))
                .Select(g => new RevenuePointDto { PeriodLabel = g.Key, Amount = g.Sum(t => t.FeeAmount) })
                .OrderBy(p => p.PeriodLabel, StringComparer.Ordinal)
                .ToList();

            return new RevenueReportDto { TotalRevenue = transactions.Sum(t => t.FeeAmount), Points = points };
        }

        /// <summary>
        /// Nhãn kỳ dùng để gộp doanh thu. Tuần theo ISO-8601: "ww" không phải custom format
        /// specifier hợp lệ của .NET nên ToString("yyyy-'W'ww") ném FormatException.
        /// </summary>
        private static string PeriodLabel(DateTime at, string? groupBy) => groupBy?.ToLowerInvariant() switch
        {
            "month" => at.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "week" => $"{ISOWeek.GetYear(at)}-W{ISOWeek.GetWeekOfYear(at):00}",
            _ => at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

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
                FeePolicy = t.FeePolicy,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();

            return new PagedResult<TransactionHistoryItemDto> { Items = dtos, TotalCount = total, Page = page, PageSize = pageSize };
        }

        /// <summary>
        /// Ưu tiên người nộp phí đã chốt; chỉ tra owner hiện tại cho giao dịch cũ chưa có PayerId.
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
            foreach (var t in transactions.Where(t => t.PayerId.HasValue))
                map[(t.SourceType, t.SourceId)] = t.PayerId!.Value;
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
            var (periodStart, periodEnd) = Shared.BillingPeriod.ClosedMonth(year, month, _clock.GetUtcNow());
            await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;

            var transactions = await _db.PlatformTransactions
                .Where(t => t.CreatedAt >= periodStart && t.CreatedAt < periodEnd
                    && (t.SourceType != "BATCH_ORDER" || t.FeePolicy == Retrack.API.Services.Shared.PlatformFees.BuyerSeparate))
                .ToListAsync();

            var payerIds = await ResolvePayerIdsAsync(transactions);

            var totalsByPayer = transactions
                .Where(t => payerIds.ContainsKey((t.SourceType, t.SourceId)))
                .GroupBy(t => payerIds[(t.SourceType, t.SourceId)])
                .ToDictionary(g => g.Key, g => g.Sum(t => t.FeeAmount));
            var invoicedPayers = await _db.PlatformInvoices.Where(i => i.PeriodYear == year && i.PeriodMonth == month)
                .Select(i => i.PayerId).Distinct().ToListAsync();

            var created = new List<PlatformInvoiceDto>();
            foreach (var payerId in totalsByPayer.Keys.Union(invoicedPayers).Order())
            {
                // Tuần tự hóa phát hành nhưng cho phép FK KEY SHARE của thông báo khi luồng khác giữ hóa đơn.
                if (_db.Database.IsRelational())
                    await _db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {payerId} FOR NO KEY UPDATE").LoadAsync();
                var periodInvoices = _db.Database.IsRelational()
                    ? await _db.PlatformInvoices.FromSqlInterpolated($"SELECT * FROM platform_invoices WHERE payer_id = {payerId} AND period_year = {year} AND period_month = {month} FOR UPDATE").ToListAsync()
                    : await _db.PlatformInvoices.Where(i => i.PayerId == payerId && i.PeriodYear == year && i.PeriodMonth == month).ToListAsync();
                if (_db.Database.IsRelational())
                    foreach (var saved in periodInvoices) await _db.Entry(saved).ReloadAsync();
                var total = totalsByPayer.GetValueOrDefault(payerId);
                var difference = total - periodInvoices.Sum(i => i.TotalFeeAmount);
                if (difference < 0)
                {
                    var payerName = await _db.Users.Where(u => u.Id == payerId).Select(u => u.FullName).SingleAsync();
                    throw new Depot.DepotConflictException($"Phí kỳ {month}/{year} của {payerName} thấp hơn tổng hóa đơn đã phát hành. Cần đối soát/ghi điều chỉnh giảm riêng; hệ thống không tự sửa hóa đơn đã khóa.");
                }
                if (difference == 0) continue;

                var invoice = periodInvoices.Where(i => i.Status == "PENDING" && i.SubmittedAt == null && string.IsNullOrWhiteSpace(i.PaymentProofUrl))
                    .OrderByDescending(i => i.Revision).FirstOrDefault();
                if (invoice != null)
                {
                    var oldAmount = invoice.TotalFeeAmount;
                    invoice.TotalFeeAmount += difference;
                    await _invoiceRepo.UpdateAsync(invoice);
                    await LogAsync(actorId, "RECONCILE", "PlatformInvoice", invoice.Id,
                        new { TotalFeeAmount = oldAmount }, new { invoice.TotalFeeAmount, year, month });
                }
                else
                {
                    var original = periodInvoices.SingleOrDefault(i => i.Revision == 0);
                    invoice = new PlatformInvoice
                    {
                        PayerId = payerId,
                        PeriodYear = year,
                        PeriodMonth = month,
                        Revision = original == null ? 0 : periodInvoices.Max(i => i.Revision) + 1,
                        AdjustmentOfId = original?.Id,
                        TotalFeeAmount = difference,
                        Status = "PENDING"
                    };
                    await _invoiceRepo.CreateAsync(invoice);
                    await LogAsync(actorId, invoice.Revision == 0 ? "ISSUE" : "SUPPLEMENT", "PlatformInvoice", invoice.Id,
                        null, new { invoice.TotalFeeAmount, invoice.Revision, invoice.AdjustmentOfId, year, month });
                }

                await _notificationService.SendInvoiceAsync(payerId,
                    $"Đối soát hóa đơn phí nền tảng tháng {month}/{year}",
                    $"{(invoice.Revision == 0 ? "Hóa đơn gốc" : $"Hóa đơn bổ sung lần {invoice.Revision}")} kỳ {month}/{year}: {invoice.TotalFeeAmount:N0}đ. Vui lòng kiểm tra hóa đơn trước khi thanh toán; hóa đơn đã gửi chứng từ/đã thanh toán được giữ nguyên.");

                created.Add(await MapInvoiceAsync(invoice));
            }

            await LogAsync(actorId, "GENERATE", "PlatformInvoice", null, null, new { year, month, count = created.Count });
            if (transaction != null) await transaction.CommitAsync();
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
            await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
            if (_db.Database.IsRelational())
                await _db.PlatformInvoices.FromSqlInterpolated($"SELECT * FROM platform_invoices WHERE id = {id} FOR UPDATE").LoadAsync();
            var invoice = await _invoiceRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
            await _db.Entry(invoice).ReloadAsync();
            if (invoice.Status == "PAID") return await MapInvoiceAsync(invoice);
            invoice.Status = "PAID";
            invoice.PaidAt = DateTime.UtcNow;
            await _invoiceRepo.UpdateAsync(invoice);

            await _notificationService.SendInvoiceAsync(invoice.PayerId,
                $"Đã xác nhận thanh toán phí nền tảng tháng {invoice.PeriodMonth}/{invoice.PeriodYear}",
                $"Hóa đơn phí nền tảng {invoice.TotalFeeAmount:N0}đ đã được Admin xác nhận thanh toán.");

            await LogAsync(actorId, "MARK_PAID", "PlatformInvoice", invoice.Id, null, new { invoice.Status, invoice.PaidAt });
            if (transaction != null) await transaction.CommitAsync();
            return await MapInvoiceAsync(invoice);
        }

        public async Task ResendInvoiceReminderAsync(Guid id)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

            await _notificationService.SendInvoiceAsync(invoice.PayerId,
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
            Revision = i.Revision,
            AdjustmentOfId = i.AdjustmentOfId,
            TotalFeeAmount = i.TotalFeeAmount,
            Status = i.Status,
            PaidAt = i.PaidAt,
            PaymentProofUrl = i.PaymentProofUrl,
            SubmittedAt = i.SubmittedAt,
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
