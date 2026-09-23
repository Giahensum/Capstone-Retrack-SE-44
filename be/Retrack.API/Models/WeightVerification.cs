namespace Retrack.API.Models;

public class WeightVerification
{
    public Guid Id { get; set; }
    public Guid BatchOrderId { get; set; }
    public decimal DepotWeightKg { get; set; }
    public decimal FactoryWeightKg { get; set; }
    public decimal DifferencePercentage { get; set; }
    public bool IsVerified { get; set; }
    public decimal? PurityPercent { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ContaminationPercent { get; set; }
    public string? Grade { get; set; }
    public string? QualityNote { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BatchOrder BatchOrder { get; set; } = null!;
}
