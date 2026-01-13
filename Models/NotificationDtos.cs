using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class NotificationDto
    {
        public long NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public object? Payload { get; set; }
    }

    public class NotificationListResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<NotificationDto> Notifications { get; set; } = new();
    }

    public class SendTestNotificationDto
    {
        [Required]
        public int UserId { get; set; }
        [Required]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Message { get; set; } = string.Empty;
        [Required]
        public string Channel { get; set; } = string.Empty;
        public object? Payload { get; set; }
        public string? TemplateKey { get; set; }
    }

    public class SendNotificationDto
    {
        [Required]
        public List<int> Recipients { get; set; } = new();
        public string? TenantType { get; set; }
        public int? TenantId { get; set; }
        [Required]
        public string TemplateKey { get; set; } = string.Empty;
        public object? TemplateData { get; set; }
        [Required]
        public List<string> Channels { get; set; } = new();
        public string? ClientReference { get; set; }
    }

    public class NotificationPreferenceDto
    {
        [Required]
        public string Channel { get; set; } = string.Empty;
        public string? Topic { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
























