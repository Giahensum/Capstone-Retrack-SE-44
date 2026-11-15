namespace Retrack.API.Services.Depot;

public sealed class DepotForbiddenException(string message = "Không có quyền truy cập kho này.") : Exception(message);
public sealed class DepotConflictException(string message) : Exception(message);
