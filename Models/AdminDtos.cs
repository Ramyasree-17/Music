using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class CreateSuperAdminDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string FullName { get; set; } = string.Empty;
        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;
        public bool MfaEnabled { get; set; } = true;
    }

    public class DeleteEntityDto
    {
        [Required]
        public int EntityId { get; set; }
        [Required]
        public string Reason { get; set; } = string.Empty;
        public bool ArchiveData { get; set; } = true;
    }

    public class SystemAnalyticsResponse
    {
        public int ActiveUsersLast30Days { get; set; }
        public int MonthlyNewReleases { get; set; }
        public Dictionary<string, decimal> MonthlyRevenue { get; set; } = new();
        public int OpenSupportTickets { get; set; }
        public int QcQueueLength { get; set; }
        public int DeliveryQueueLength { get; set; }
        public int SearchAvgLatencyMs { get; set; }
        public long S3StorageBytes { get; set; }
        public decimal PendingPayoutAmount { get; set; }
    }

    public class FixPasswordHashDto
    {
        public int? UserId { get; set; }
        public string? Email { get; set; }
        [Required]
        public string PlainTextPassword { get; set; } = string.Empty;
    }
}












