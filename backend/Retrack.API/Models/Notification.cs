using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("transport_job_id")]
        public Guid? TransportJobId { get; set; }

        [Column("pickup_request_id")]
        public Guid? PickupRequestId { get; set; }

        [Column("depot_id")] public Guid? DepotId { get; set; }
        [Column("batch_id")] public Guid? BatchId { get; set; }
        [Column("category"), MaxLength(30)] public string Category { get; set; } = "SYSTEM";
        [Column("target_type"), MaxLength(30)] public string? TargetType { get; set; }
        [Column("target_id")] public Guid? TargetId { get; set; }

        // Chỉ tạo đích nội bộ có kiểu; không nhận URL hoặc người nhận từ request client.
        public static Notification ForDepot(Guid owner, Guid depot, string category, string title, string message,
            Guid? batch = null, string? targetType = null, Guid? targetId = null) => new()
            { UserId = owner, DepotId = depot, Category = category, Title = title, Message = message,
              BatchId = batch, TargetType = targetType, TargetId = targetId };

        [Required]
        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column("message")]
        public string? Message { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
