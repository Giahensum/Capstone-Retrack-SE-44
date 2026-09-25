using System.ComponentModel.DataAnnotations;

namespace Retrack.API.DTOs.Depot;

public record InventoryRowDto(string MaterialType, decimal ReceivedKg, decimal ReservedKg, decimal ExportedKg)
{
    public decimal OnHandKg => ReceivedKg - ExportedKg;
    public decimal AvailableKg => ReceivedKg - ReservedKg - ExportedKg;
}
public sealed class CreateDepotBatchDto
{
    public Guid OperationId { get; set; }
    [Required, StringLength(100)] public string MaterialType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public Guid? TargetFactoryId { get; set; }
    public string? Description { get; set; }
}
public record DepotBatchDto(Guid Id, Guid DepotId, string MaterialType, decimal WeightKg,
    string? Description, string Status, Guid? TargetFactoryId, string? FactoryName, DateTime CreatedAt, string? Code = null);
