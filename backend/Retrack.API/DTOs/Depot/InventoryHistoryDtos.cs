namespace Retrack.API.DTOs.Depot;

public sealed class InventoryHistoryQuery : DepotQuery
{
    public string? Source { get; set; }
}
public record InventoryHistoryLineDto(string Name, string? MaterialType, decimal Quantity, string Unit);
public record InventoryHistoryDto(Guid Id, string SourceType, string Title, string Status, DateTime ReceivedAt,
    List<InventoryHistoryLineDto> Lines);
