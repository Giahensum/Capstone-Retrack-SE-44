using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Employee;

public sealed class EmployeeCheckInRequest
{
    [Required] public IFormFile ImageFile { get; set; } = null!;
    [Required] public double? Latitude { get; set; }
    [Required] public double? Longitude { get; set; }
    [Required] public double? AccuracyMeters { get; set; }
    [Required] public DateTimeOffset? LocationRecordedAt { get; set; }
    [Required] public DateTimeOffset? PhotoTakenAt { get; set; }
    public bool IsMocked { get; set; }
}

public sealed record ClassificationItemInput(string MaterialType, decimal WeightKg, decimal PricePerKg);
public sealed record SaveClassificationRequest(int ExpectedRevision, List<ClassificationItemInput>? Items);
public sealed record ClassificationItemDto(Guid Id, string MaterialType, decimal WeightKg, decimal PricePerKg, decimal SubTotal);
public sealed record CheckInEvidenceDto(string ImageUrl, double Latitude, double Longitude,
    double AccuracyMeters, double DistanceMeters, DateTime CheckedInAt);
public sealed record CollectionPolicyDto(int RadiusMeters, int MaxAccuracyMeters, int MaxLocationAgeSeconds);
public sealed record EmployeeCollectionDto(Guid PickupId, string Status, string Address, decimal? Latitude,
    decimal? Longitude, CheckInEvidenceDto? CheckIn, int Revision, bool CanEdit,
    IReadOnlyList<ClassificationItemDto> Items, decimal TotalWeightKg, decimal GrossAmount, CollectionPolicyDto Policy,
    decimal PlatformFeePercentage = 0, decimal PlatformFeeAmount = 0, decimal NetAmount = 0);
public sealed record MaterialReferenceDto(string Code, string Label, decimal? ReferencePrice,
    DateTime? EffectiveDate, string? Source);
