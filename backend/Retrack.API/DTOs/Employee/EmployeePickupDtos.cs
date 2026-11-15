namespace Retrack.API.DTOs.Employee;

public sealed record EmployeeDashboardDto(int AvailableCount, ActivePickupSummaryDto? ActivePickup,
    int CompletedToday, int TotalPickupsCompleted, int CheckedInCount = 0, int WaitingSellerCount = 0,
    int ReadyToHandoverCount = 0, int WaitingPaymentCount = 0, int UnreadCount = 0);
public sealed record ActivePickupSummaryDto(Guid Id, string SellerName, string? Phone, string Address,
    string Status, DateTime? PreferredDatetime);
public sealed record PickupPoolItemDto(Guid Id, string SellerName, string? SellerPhone, string Address,
    decimal? Latitude, decimal? Longitude, DateTime? PreferredDatetime,
    string? Description, string? RequestImageUrl, DateTime CreatedAt);
public sealed record EmployeePickupDetailDto(Guid Id, string SellerName, string? SellerPhone, string Address,
    decimal? Latitude, decimal? Longitude, DateTime? PreferredDatetime,
    string? Description, string? RequestImageUrl, DateTime CreatedAt, string Status, bool IsAcceptedByMe);
