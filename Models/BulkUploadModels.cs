namespace TunewaveAPIDB1.Models
{
    public class BulkJob
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BulkJobLog
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int RowNumber { get; set; }
        public string Status { get; set; } = string.Empty; // Success, Failed
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ReleaseTitle { get; set; }
        public string? TrackTitle { get; set; }
        public int? ReleaseId { get; set; }
        public int? TrackId { get; set; }
    }

    public class BulkUploadRow
    {
        // Release fields
        public string? ReleaseTitle { get; set; }
        public string? ReleaseTitleVersion { get; set; }
        public int? LabelId { get; set; }
        public string? ReleaseDescription { get; set; }
        public string? PrimaryGenre { get; set; }
        public string? SecondaryGenre { get; set; }
        public DateTime? DigitalReleaseDate { get; set; }
        public DateTime? OriginalReleaseDate { get; set; }
        public string? UPCCode { get; set; }

        // Track fields
        public string? TrackTitle { get; set; }
        public string? TrackVersion { get; set; }
        public string? PrimaryArtistIds { get; set; } // Comma-separated
        public string? FeaturedArtistIds { get; set; }
        public string? ComposerIds { get; set; }
        public string? LyricistIds { get; set; }
        public string? ProducerIds { get; set; }
        public string? ISRC { get; set; }
        public int? TrackNumber { get; set; }
        public string? Language { get; set; }
        public bool? IsExplicit { get; set; }
        public bool? IsInstrumental { get; set; }
        public string? TrackGenre { get; set; }
        public int? DurationSeconds { get; set; }

        // File fields
        public string? FileUrl { get; set; }
        
        // Additional Release fields
        public string? CoverArtUrl { get; set; }
    }
}

