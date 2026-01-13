namespace TunewaveAPIDB1.Models
{
    public class JobDto
    {
        public long JobId { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public DateTime? NextRunAt { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class JobQueueResponse
    {
        public int Count { get; set; }
        public List<JobDto> Jobs { get; set; } = new();
    }

    public class JobStatusResponse
    {
        public long JobId { get; set; }
        public string JobType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public string? LastError { get; set; }
        public List<JobAttemptDto> AttemptsLog { get; set; } = new();
    }

    public class JobAttemptDto
    {
        public int AttemptNumber { get; set; }
        public string? WorkerId { get; set; }
        public bool Success { get; set; }
        public DateTime StartedAt { get; set; }
        public int? DurationMs { get; set; }
    }

    public class RetryJobDto
    {
        public bool Force { get; set; } = true;
        public bool ResetAttempts { get; set; } = false;
        public DateTime? NextRunAt { get; set; }
    }
}
























