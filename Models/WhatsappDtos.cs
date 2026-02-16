using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class WhatsappDto
    {
        [Required]
        [MaxLength(200)]
        public string AppKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string AuthKey { get; set; } = string.Empty;
    }

    public class WhatsappResponseDto
    {
        public int Id { get; set; }
        public int BrandingId { get; set; }
        public string AppKey { get; set; } = string.Empty;
        public string AuthKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}


