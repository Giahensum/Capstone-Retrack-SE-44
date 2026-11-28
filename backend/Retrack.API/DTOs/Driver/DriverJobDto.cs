namespace Retrack.API.DTOs.Driver;

public sealed record JobPlace(string Name, string Address, decimal? Latitude, decimal? Longitude);
public sealed record DriverJobDto(Guid Id, Guid BatchId, string? BatchCode, string MaterialType,
    decimal WeightKg, string Status, bool IsMine, JobPlace Depot, JobPlace Factory, DateTime CreatedAt);
