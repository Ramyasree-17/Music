using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class CreateTicketDto
    {
        public string? TenantType { get; set; }
        public int? TenantId { get; set; }
        [Required]
        public string Category { get; set; } = string.Empty;
        [Required]
        public string Priority { get; set; } = "MEDIUM";
        [Required]
        public string Subject { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        public List<int>? Attachments { get; set; }
        public List<int>? CcUserIds { get; set; }
    }

    public class TicketDto
    {
        public long TicketId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? TenantType { get; set; }
        public int? TenantId { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? AssignedTo { get; set; }
        public List<TicketMessageDto> Messages { get; set; } = new();
        public List<TicketParticipantDto> Participants { get; set; } = new();
    }

    public class TicketMessageDto
    {
        public long MessageId { get; set; }
        public int SenderUserId { get; set; }
        public string Body { get; set; } = string.Empty;
        public List<AttachmentDto>? Attachments { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsInternal { get; set; }
    }

    public class AttachmentDto
    {
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? CloudfrontUrl { get; set; }
    }

    public class TicketParticipantDto
    {
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class AddMessageDto
    {
        [Required]
        public string Body { get; set; } = string.Empty;
        public List<int>? Attachments { get; set; }
        public bool IsInternal { get; set; } = false;
    }

    public class CloseTicketDto
    {
        public string? ResolutionNotes { get; set; }
        [Required]
        public string CloseStatus { get; set; } = "RESOLVED";
    }
}
























