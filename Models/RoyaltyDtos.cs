using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class UploadRoyaltyStatementRequest
    {
        public int? LabelId { get; set; }
        public int? EnterpriseId { get; set; }
        [Required]
        public DateTime PeriodStart { get; set; }
        [Required]
        public DateTime PeriodEnd { get; set; }
        [Required]
        public string Currency { get; set; } = "USD";
        public string? FileS3Key { get; set; }
    }

    public class FixMappingRequest
    {
        [Required]
        public long RowId { get; set; }
        [Required]
        public int ReleaseId { get; set; }
        public int? TrackId { get; set; }
        public string? Notes { get; set; }
    }

    public class RoyaltySummaryResponse
    {
        public int LabelId { get; set; }
        public DateRange Period { get; set; } = new();
        public decimal TotalGross { get; set; }
        public decimal TunewaveCommission { get; set; }
        public decimal LabelNet { get; set; }
        public List<ArtistRoyaltyDto> Artists { get; set; } = new();
        public int UnmappedRows { get; set; }
    }

    public class ArtistRoyaltyDto
    {
        public int ArtistId { get; set; }
        public string ArtistName { get; set; } = string.Empty;
        public decimal Gross { get; set; }
        public decimal PayoutDue { get; set; }
    }

    public class DateRange
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    public class UnmappedRowDto
    {
        public long RowId { get; set; }
        public int RowNumber { get; set; }
        public string? Isrc { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public long Units { get; set; }
    }
}
























