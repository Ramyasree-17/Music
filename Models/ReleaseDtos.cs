using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class CreateReleaseDto
    {
        public string? Title { get; set; }

        public string? TitleVersion { get; set; }

        public int? LabelId { get; set; }

        public string? Description { get; set; }

        public string? CoverArtUrl { get; set; }

        /// <summary>
        /// Base64 encoded cover art image (data URI format: "data:image/jpeg;base64,..." or just base64 string)
        /// If provided, this will be saved to wwwroot/images and CoverArtUrl will be set automatically
        /// </summary>
        public string? CoverArtBase64 { get; set; }

        /// <summary>
        /// Cover art file upload (for multipart/form-data)
        /// </summary>
        public Microsoft.AspNetCore.Http.IFormFile? CoverArtFile { get; set; }

        /// <summary>
        /// Contributors as JSON string (alternative to Contributors array for form data)
        /// </summary>
        public string? ContributorsJson { get; set; }

        public string? PrimaryGenre { get; set; }

        public string? SecondaryGenre { get; set; }

        public DateTime? DigitalReleaseDate { get; set; }

        public DateTime? OriginalReleaseDate { get; set; }

        public bool HasUPC { get; set; }

        public string? UPCCode { get; set; }

        public List<ContributorDto> Contributors { get; set; } = new();

        /// <summary>
        /// Grouped contributors format (for multipart form binding)
        /// </summary>
        public ContributorGroupDto? ContributorsGroup { get; set; }

        public DistributionOptionDto? DistributionOption { get; set; }

        // Optional: existing track IDs to associate (tracks are normally created via /api/tracks)
        public List<int> TrackIds { get; set; } = new();
    }

    public class CreateTrackDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? TrackVersion { get; set; }

        [Required]
        public List<int> PrimaryArtistIds { get; set; } = new();

        public List<int> FeaturedArtistIds { get; set; } = new();

        public List<int> ComposerIds { get; set; } = new();

        public List<int> LyricistIds { get; set; } = new();

        public List<int> ProducerIds { get; set; } = new();

        public string? ISRC { get; set; }

        public int? TrackNumber { get; set; }

        public string? Language { get; set; }

        public string? Lyrics { get; set; }

        public bool IsExplicit { get; set; }

        public bool IsInstrumental { get; set; }

        public int? PreviewStartTimeSeconds { get; set; }

        public string? TrackGenre { get; set; }

        public int? DurationSeconds { get; set; }
    }

    public class ReleaseResponseDto
    {
        public int ReleaseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleVersion { get; set; }
        public int LabelId { get; set; }
        public string? Description { get; set; }
        public string? CoverArtUrl { get; set; }
        public string PrimaryGenre { get; set; } = string.Empty;
        public string? SecondaryGenre { get; set; }
        public DateTime? DigitalReleaseDate { get; set; }
        public DateTime? OriginalReleaseDate { get; set; }
        public bool HasUPC { get; set; }
        public string? UPCCode { get; set; }
        public List<ContributorDto> Contributors { get; set; } = new();
        public List<LocalizationDto> Localizations { get; set; } = new();
        public DistributionOptionDto DistributionOption { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<TrackResponseDto> Tracks { get; set; } = new();
    }

    public class TrackResponseDto
    {
        public int TrackId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TrackVersion { get; set; }
        public List<int> PrimaryArtistIds { get; set; } = new();
        public List<string> PrimaryArtistNames { get; set; } = new();
        public List<int> FeaturedArtistIds { get; set; } = new();
        public List<string> FeaturedArtistNames { get; set; } = new();
        public List<int> ComposerIds { get; set; } = new();
        public List<string> ComposerNames { get; set; } = new();
        public List<int> LyricistIds { get; set; } = new();
        public List<string> LyricistNames { get; set; } = new();
        public List<int> ProducerIds { get; set; } = new();
        public List<string> ProducerNames { get; set; } = new();
        public string? ISRC { get; set; }
        public int? TrackNumber { get; set; }
        public string? Language { get; set; }
        public string? Lyrics { get; set; }
        public bool IsExplicit { get; set; }
        public bool IsInstrumental { get; set; }
        public int? PreviewStartTimeSeconds { get; set; }
        public string? TrackGenre { get; set; }
        public int? DurationSeconds { get; set; }
    }

    public class TakedownRequestDto
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    public class ContributorDto
    {
        [Required]
        public int ArtistId { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty; // "Primary", "Featured", "Producer", etc.
    }

    /// <summary>
    /// Grouped contributors format: { "Primary Artist": [1,2], "Featured Artist": [], ... }
    /// </summary>
    public class ContributorGroupDto
    {
        public List<int>? PrimaryArtist { get; set; }
        public List<int>? FeaturedArtist { get; set; }
        public List<int>? Producer { get; set; }
        public List<int>? Director { get; set; }
        public List<int>? Composer { get; set; }
        public List<int>? Lyricist { get; set; }
    }

    /// <summary>
    /// Contributors DTO for response format (same structure as ContributorGroupDto)
    /// </summary>
    public class ContributorsDto
    {
        public List<int> PrimaryArtist { get; set; } = new();
        public List<int> FeaturedArtist { get; set; } = new();
        public List<int> Producer { get; set; } = new();
        public List<int> Director { get; set; } = new();
        public List<int> Composer { get; set; } = new();
        public List<int> Lyricist { get; set; } = new();
    }

    /// <summary>
    /// DTO for updating release status
    /// </summary>
    public class UpdateStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    public class LocalizationDto
    {
        [Required]
        public string LanguageCode { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Description { get; set; }
    }

    public class DistributionOptionDto
    {
        public string? DistributionType { get; set; } = "SelectAll"; // "SelectAll" or "Manual"

        public List<int>? SelectedStoreIds { get; set; } = new(); // Used when DistributionType is "Manual"
    }

    public class UpdateReleaseDto
    {
        public string? Title { get; set; }

        public string? TitleVersion { get; set; }

        public string? Description { get; set; }

        public string? CoverArtUrl { get; set; }

        /// <summary>
        /// Base64 encoded cover art image (data URI format: "data:image/jpeg;base64,..." or just base64 string)
        /// If provided, this will be saved to wwwroot/images and CoverArtUrl will be set automatically
        /// </summary>
        public string? CoverArtBase64 { get; set; }

        /// <summary>
        /// Cover art file upload (for multipart/form-data)
        /// </summary>
        public Microsoft.AspNetCore.Http.IFormFile? CoverArtFile { get; set; }

        public string? PrimaryGenre { get; set; }

        public string? SecondaryGenre { get; set; }

        public DateTime? DigitalReleaseDate { get; set; }

        public DateTime? OriginalReleaseDate { get; set; }

        public bool? HasUPC { get; set; }

        public string? UPCCode { get; set; }

        // Optional: replace all existing contributors if provided
        public List<ContributorDto>? Contributors { get; set; }

        /// <summary>
        /// Grouped contributors format (for multipart form binding)
        /// </summary>
        public ContributorGroupDto? ContributorsGroup { get; set; }

        // Optional: replace distribution option if provided
        public DistributionOptionDto? DistributionOption { get; set; }
    }

    // Form data DTOs for multipart/form-data support
    public class CreateReleaseFormDto
    {
        public string? Title { get; set; }
        public string? TitleVersion { get; set; }
        public int? LabelId { get; set; }
        public string? Description { get; set; }
        public string? CoverArtUrl { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CoverArtFile { get; set; }
        public string? PrimaryGenre { get; set; }
        public string? SecondaryGenre { get; set; }
        public DateTime? DigitalReleaseDate { get; set; }
        public DateTime? OriginalReleaseDate { get; set; }
        public bool? HasUPC { get; set; }
        public string? UPCCode { get; set; }
        
        // Arrays for contributors - form data uses array notation
        public List<int>? ContributorsPrimaryArtist { get; set; }
        public List<int>? ContributorsFeaturedArtist { get; set; }
        public List<int>? ContributorsProducer { get; set; }
        public List<int>? ContributorsDirector { get; set; }
        public List<int>? ContributorsComposer { get; set; }
        public List<int>? ContributorsLyricist { get; set; }
        
        // Distribution options
        public string? DistributionOptionDistributionType { get; set; }
        public List<int>? DistributionOptionSelectedStoreIds { get; set; }
        
        // Track IDs
        public List<int>? TrackIds { get; set; }
    }

    public class UpdateReleaseFormDto
    {
        public string? Title { get; set; }
        public string? TitleVersion { get; set; }
        public string? Description { get; set; }
        public string? CoverArtUrl { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CoverArtFile { get; set; }
        public string? PrimaryGenre { get; set; }
        public string? SecondaryGenre { get; set; }
        public DateTime? DigitalReleaseDate { get; set; }
        public DateTime? OriginalReleaseDate { get; set; }
        public bool? HasUPC { get; set; }
        public string? UPCCode { get; set; }
        
        // Arrays for contributors
        public List<int>? ContributorsPrimaryArtist { get; set; }
        public List<int>? ContributorsFeaturedArtist { get; set; }
        public List<int>? ContributorsProducer { get; set; }
        public List<int>? ContributorsDirector { get; set; }
        public List<int>? ContributorsComposer { get; set; }
        public List<int>? ContributorsLyricist { get; set; }
        
        // Distribution options
        public string? DistributionOptionDistributionType { get; set; }
        public List<int>? DistributionOptionSelectedStoreIds { get; set; }
    }
}

