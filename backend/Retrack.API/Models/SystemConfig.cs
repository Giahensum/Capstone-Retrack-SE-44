using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Retrack.API.Models
{
    [Table("system_configs")]
    public class SystemConfig
    {
        [Key]
        [Column("config_key")]
        [MaxLength(50)]
        public string ConfigKey { get; set; } = string.Empty;

        [Required]
        [Column("config_value")]
        public string ConfigValue { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

