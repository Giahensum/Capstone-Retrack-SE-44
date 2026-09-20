using Retrack.API.Models.Enums;
namespace Retrack.API.Models;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid BatchOrderId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceFileUrl { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.PENDING;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BatchOrder BatchOrder { get; set; } = null!;
}
