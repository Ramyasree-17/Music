using System;
using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class UserMeResponseDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public UserMembershipDto? Memberships { get; set; }
    }

    public class UserMembershipDto
    {
        public List<EnterpriseMembershipDto> Enterprises { get; set; } = new();
        public List<LabelMembershipDto> Labels { get; set; } = new();
        public List<ArtistMembershipDto> Artists { get; set; } = new();
    }

    public class EnterpriseMembershipDto
    {
        public int EnterpriseId { get; set; }
        public string EnterpriseName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsDefault { get; set; }  // Indicates default enterprise for the user
    }

    public class LabelMembershipDto
    {
        public int LabelId { get; set; }
        public string LabelName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsDefault { get; set; }  // Indicates default label for the user
    }

    public class ArtistMembershipDto
    {
        public int ArtistId { get; set; }
        public string ArtistName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsDefault { get; set; }  // Indicates default artist for the user
    }

    public class UpdateProfileRequestDto
    {
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public string? CountryCode { get; set; }
    }

    public class ChangePasswordRequestDto
    {
        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;

        public string? ConfirmPassword { get; set; }
    }

    public class SwitchEntityRequestDto
    {
        [Required]
        public string EntityType { get; set; } = string.Empty; // label|enterprise|artist

        [Required]
        public int EntityId { get; set; }
    }

    public class UserEntitiesResponseDto
    {
        public List<EnterpriseMembershipDto> Enterprises { get; set; } = new();
        public List<LabelMembershipDtoExtended> Labels { get; set; } = new();
        public List<ArtistMembershipDto> Artists { get; set; } = new();
    }

    public class LabelMembershipDtoExtended : LabelMembershipDto
    {
        public string? PlanType { get; set; }
    }

    public class ActivityLogDto
    {
        public long AuditId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RegisterRequestDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Mobile { get; set; } = string.Empty;

        [Required]
        public string CountryCode { get; set; } = string.Empty;

        public string? Role { get; set; }
    }

    public class RegisterResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}


