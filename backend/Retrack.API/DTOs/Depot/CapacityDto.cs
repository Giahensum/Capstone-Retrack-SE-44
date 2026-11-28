namespace Retrack.API.DTOs.Depot;

public record CapacityDto(int Used, int Limit)
{
    public int Remaining => Math.Max(0, Limit - Used);
    public bool CanAdd => Used < Limit;
}
