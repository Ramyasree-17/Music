namespace TunewaveAPIDB1.Models
{
    public class AuditLogDto
    {
        public long AuditId { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorType { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public object? DetailsJson { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLogsResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<AuditLogDto> Logs { get; set; } = new();
    }
}
























