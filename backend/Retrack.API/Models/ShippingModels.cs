using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("shipping_quotes")]
public sealed class ShippingQuote
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("factory_id")] public Guid FactoryId { get; set; }
    [Column("material_type")] public string MaterialType { get; set; } = "";
    [Column("weight_kg")] public decimal WeightKg { get; set; }
    [Column("method")] public string Method { get; set; } = "INTERNAL";
    [Column("provider")] public string Provider { get; set; } = "";
    [Column("service_type")] public string ServiceType { get; set; } = "";
    [Column("provider_quote_id")] public string ProviderQuoteId { get; set; } = "";
    [Column("amount")] public decimal Amount { get; set; }
    [Column("factor")] public decimal Factor { get; set; }
    [Column("estimated_cost")] public decimal EstimatedCost { get; set; }
    [Column("distance_km")] public decimal DistanceKm { get; set; }
    [Column("currency")] public string Currency { get; set; } = "VND";
    [Column("snapshot_json")] public string SnapshotJson { get; set; } = "{}";
    [Column("provider_json")] public string ProviderJson { get; set; } = "{}";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
}

[Table("external_shipments")]
public sealed class ExternalShipment
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("batch_id")] public Guid BatchId { get; set; }
    [Column("quote_id")] public Guid QuoteId { get; set; }
    [Column("operation_id")] public Guid OperationId { get; set; }
    [Column("provider")] public string Provider { get; set; } = "";
    [Column("provider_order_id")] public string? ProviderOrderId { get; set; }
    [Column("status")] public string Status { get; set; } = "BOOKING";
    [Column("share_link")] public string? ShareLink { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("currency")] public string Currency { get; set; } = "VND";
    [Column("contact_json")] public string ContactJson { get; set; } = "{}";
    [Column("last_webhook_at")] public DateTime? LastWebhookAt { get; set; }
    [Column("last_error")] public string? LastError { get; set; }
    [Column("last_checked_at")] public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("shipping_events")]
public sealed class ShippingEvent
{
    [Key, Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("shipment_id")] public Guid ShipmentId { get; set; }
    [Column("status")] public string Status { get; set; } = "";
    [Column("source")] public string Source { get; set; } = "";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
