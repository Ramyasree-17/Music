namespace TunewaveAPIDB1.Models
{
    public class BulkUploadResponseDto
    {
        public int JobId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BulkJobStatusDto
    {
        public int JobId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int FailedRows { get; set; }
        public int CurrentRowNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
        public TimeSpan? RemainingTime { get; set; }
        public double ProgressPercentage { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BulkJobListDto
    {
        public int JobId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int FailedRows { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class BulkJobLogDto
    {
        public int Id { get; set; }
        public int RowNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ReleaseTitle { get; set; }
        public string? TrackTitle { get; set; }
        public int? ReleaseId { get; set; }
        public int? TrackId { get; set; }
    }
}


