using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class GenerateDeliveryRequest
    {
        public List<string>? Dsps { get; set; }
        public bool ForceRebuild { get; set; } = false;
        public string? Notes { get; set; }
    }

    public class RedeliverRequest
    {
        public List<string>? Dsps { get; set; }
        [Required]
        public string Reason { get; set; } = string.Empty;
        public bool ForceRebuild { get; set; } = true;
    }

    public class DeliveryPackageDto
    {
        public long PackageId { get; set; }
        public string Dsp { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public string? ExternalId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    public class DeliveryStatusResponse
    {
        public int ReleaseId { get; set; }
        public List<DeliveryPackageDto> Packages { get; set; } = new();
    }

    public class DeliveryLogDto
    {
        public int AttemptNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public object? RequestJson { get; set; }
        public object? ResponseJson { get; set; }
        public int? StatusCode { get; set; }
        public string? Message { get; set; }
    }

    public class DeliveryLogsResponse
    {
        public long PackageId { get; set; }
        public List<DeliveryLogDto> Logs { get; set; } = new();
    }
}
























