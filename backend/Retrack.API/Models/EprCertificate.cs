using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class EprCertificate
{
    public Guid Id { get; set; }
    public Guid BatchOrderId { get; set; }
    public string CertificateCode { get; set; } = string.Empty;
    public string HashValue { get; set; } = string.Empty;
    public MaterialType MaterialType { get; set; }
    public decimal? CertifiedWeightKg { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public BatchOrder BatchOrder { get; set; } = null!;
}
