using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("depot_staffs")]
    public class DepotStaff
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("depot_id")]
        public Guid DepotId { get; set; }

        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Required]
        [Column("staff_type")]
        [MaxLength(50)]
        public string StaffType { get; set; } = string.Empty; // DEPOT_EMPLOYEE, DRIVER

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("DepotId")]
        public Depot Depot { get; set; } = null!;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}

