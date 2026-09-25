namespace Retrack.API.DTOs
{
    // ===== USERS =====
    public class UserListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserDetailDto : UserListItemDto
    {
        public DateTime UpdatedAt { get; set; }
        public DepotProfileSummaryDto? DepotProfile { get; set; }
        public FactoryProfileSummaryDto? FactoryProfile { get; set; }
        public DepotStaffProfileSummaryDto? DepotStaffProfile { get; set; }
    }

    public class DepotProfileSummaryDto
    {
        public Guid DepotId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Rating { get; set; }
    }

    public class FactoryProfileSummaryDto
    {
        public Guid FactoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Rating { get; set; }
    }

    public class DepotStaffProfileSummaryDto
    {
        public Guid DepotId { get; set; }
        public string DepotName { get; set; } = string.Empty;
        public string StaffType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateUserDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    // ===== DASHBOARD =====
    public class AdminDashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public Dictionary<string, int> UsersByRole { get; set; } = new();
        public int TotalPickupRequests { get; set; }
        public int CompletedPickupRequests { get; set; }
        public int TotalInventoryBatches { get; set; }
        public decimal TotalPlatformRevenue { get; set; }
        public decimal RevenueThisMonth { get; set; }
    }

    // ===== MARKET PRICES =====
    public class MarketPriceDto
    {
        public Guid Id { get; set; }
        public string MaterialType { get; set; } = string.Empty;
        public decimal PricePerKg { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string? Source { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpsertMarketPriceDto
    {
        public string MaterialType { get; set; } = string.Empty;
        public decimal PricePerKg { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string? Source { get; set; }
    }

    // ===== FEE CONFIG =====
    public class FeeConfigDto
    {
        public decimal PlatformFeePercentage { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateFeeConfigDto
    {
        public decimal PlatformFeePercentage { get; set; }
    }

    // ===== REVENUE & TRANSACTIONS =====
    public class RevenuePointDto
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class RevenueReportDto
    {
        public decimal TotalRevenue { get; set; }
        public List<RevenuePointDto> Points { get; set; } = new();
    }

    public class TransactionHistoryItemDto
    {
        public Guid Id { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public Guid SourceId { get; set; }
        public string? PayerName { get; set; }
        public decimal FeeAmount { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ===== PLATFORM INVOICES =====
    public class GenerateInvoiceRequestDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class PlatformInvoiceDto
    {
        public Guid Id { get; set; }
        public Guid PayerId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal TotalFeeAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ===== AUDIT LOGS =====
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public Guid? EntityId { get; set; }
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
