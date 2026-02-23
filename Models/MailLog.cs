namespace TunewaveAPIDB1.Models
{
    public class MailLog
    {
        public int Id { get; set; }
        public int BrandingId { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime SentAt { get; set; }
    }
}



