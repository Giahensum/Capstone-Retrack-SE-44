using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models;

[Table("depot_verification_submissions")]
public sealed class DepotVerificationSubmission
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("depot_id")] public Guid DepotId { get; set; }
    [Column("status")] public string Status { get; set; } = "SUBMITTED";
    [Column("note")] public string Note { get; set; } = "";
    [Column("depot_name")] public string DepotName { get; set; } = "";
    [Column("depot_address")] public string DepotAddress { get; set; } = "";
    [Column("tax_code")] public string? TaxCode { get; set; }
    [Column("submitted_at")] public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    [Column("reviewed_at")] public DateTime? ReviewedAt { get; set; }
    [Column("reviewed_by")] public Guid? ReviewedBy { get; set; }
    [Column("review_reason")] public string? ReviewReason { get; set; }
    public Depot Depot { get; set; } = null!;
    public List<DepotVerificationDocument> Documents { get; set; } = [];
}

[Table("depot_verification_documents")]
public sealed class DepotVerificationDocument
{
    [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("submission_id")] public Guid SubmissionId { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("content_type")] public string ContentType { get; set; } = "";
    [Column("content")] public byte[] Content { get; set; } = [];
    public DepotVerificationSubmission Submission { get; set; } = null!;
}
