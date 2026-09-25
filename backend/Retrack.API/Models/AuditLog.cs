using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [Required]
        [Column("action")]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [Column("entity_name")]
        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [Column("entity_id")]
        public Guid? EntityId { get; set; }

        [Column("old_data")]
        public string? OldData { get; set; }

        [Column("new_data")]
        public string? NewData { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
