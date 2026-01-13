using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class SettingDto
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string Type { get; set; } = "STRING";
        public string? Description { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateSettingDto
    {
        [Required]
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = "STRING";
        public string? Description { get; set; }
    }
}
























