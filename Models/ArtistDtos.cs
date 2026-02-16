using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class CreateArtistDto
    {
        public string? ArtistName { get; set; }

        public string? PublicProfileName { get; set; }

        public string? Country { get; set; }

        public string? Genre { get; set; }

        public string? Bio { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public int? LabelId { get; set; }

        public string? ImageUrl { get; set; }

        public string? SoundCloudUrl { get; set; }

        public string? SpotifyUrl { get; set; }

        public string? AppleMusicUrl { get; set; }
    }

    public class UpdateArtistDto
    {
        public string? StageName { get; set; }

        /// <summary>
        /// Legacy compatibility - maps to StageName
        /// </summary>
        public string? ArtistName
        {
            get => StageName;
            set => StageName = value;
        }

        public string? PublicProfileName { get; set; }

        public string? DisplayName { get; set; }

        public string? PrimaryLanguage { get; set; }

        public string? Country { get; set; }

        public string? Genre { get; set; }

        public string? Bio { get; set; }

        public string? ImageUrl { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? SoundCloudUrl { get; set; }

        public string? SpotifyUrl { get; set; }

        public string? AppleMusicUrl { get; set; }
    }

    public class ArtistResponseDto
    {
        public int ArtistId { get; set; }
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// Legacy compatibility - maps to StageName
        /// </summary>
        public string ArtistName
        {
            get => StageName;
            set => StageName = value ?? string.Empty;
        }

        public string? PublicProfileName { get; set; }
        public string? DisplayName { get; set; }
        public string? PrimaryLanguage { get; set; }
        public string? Bio { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Country { get; set; }
        public string? Genre { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ClaimStatus { get; set; }
        public int? ClaimedUserId { get; set; }
        public string? ContactEmail { get; set; }
        public string? Email { get; set; }
        public int? LabelId { get; set; }  // Label that created this artist
        public DateTime CreatedAt { get; set; }
    }

    public class ClaimRequestDto
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    public class ArtistClaimRequestDto
    {
        [Required]
        public string EvidenceJson { get; set; } = "{}";

        public string? Note { get; set; }
    }

    public class ArtistClaimActionDto
    {
        [Required]
        public long ClaimId { get; set; }

        public string? AdminNotes { get; set; }
    }

    public class ArtistAccessRequestDto
    {
        [Required]
        public int LabelId { get; set; }

        public bool CanViewReleases { get; set; } = true;

        public bool CanViewAnalytics { get; set; } = false;

        public bool CanViewWallet { get; set; } = false;
    }

    public class ArtistAccessRevokeDto
    {
        [Required]
        public int LabelId { get; set; }
    }

    public class ArtistReleaseDto
    {
        public int ReleaseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string LabelName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? ReleaseDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ArtistWalletDto
    {
        public string Currency { get; set; } = "INR";
        public decimal Balance { get; set; }
        public decimal Reserved { get; set; }
        public decimal Available => Balance - Reserved;
        public DateTime? UpdatedAt { get; set; }
    }

    public class ArtistAnalyticsDto
    {
        public int TotalReleases { get; set; }
        public int DeliveredReleases { get; set; }
        public int PendingReleases { get; set; }
        public DateTime? LatestReleaseDate { get; set; }
        public List<ArtistTopLabelDto> TopLabels { get; set; } = new();
    }

    public class ArtistTopLabelDto
    {
        public string LabelName { get; set; } = string.Empty;
        public int Releases { get; set; }
    }
}


