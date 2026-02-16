using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class AddressUpsertRequestDto
    {
        [MaxLength(200)]
        public string? AddressLine1 { get; set; }

        [MaxLength(200)]
        public string? AddressLine2 { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(10)]
        public string? Pincode { get; set; }
    }

    public class AddressResponseDto
    {
        public int AddressId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int OwnerId { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}















