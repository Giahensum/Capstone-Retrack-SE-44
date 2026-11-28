namespace Retrack.API.DTOs.Employee;

public sealed record EmployeeHistoryRow(Guid Id, string SellerName, string Address, string Status, DateTime UpdatedAt, decimal GrossAmount);
public sealed record EmployeeTotals(int Pickups, decimal WeightKg, decimal GrossAmount);
public sealed record EmployeeStatsDto(EmployeeTotals AllTime, EmployeeTotals ThisMonth, string Timezone, string DateBasis);
public sealed record EmployeeNotificationDto(Guid Id, string Title, string? Message, bool IsRead, DateTime CreatedAt, Guid? PickupRequestId);
public sealed record EmployeeNotificationPage(List<EmployeeNotificationDto> Items, int TotalCount, int UnreadCount, int Page, int PageSize);
