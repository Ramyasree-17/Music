using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class CreateReleaseDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? TitleVersion { get; set; }

        [Required]
        public int LabelId { get; set; }

        public string? Description { get; set; }

        public string? CoverArtUrl { get; set; }

        [Required]
        public string PrimaryGenre { get; set; } = string.Empty;

        public string? SecondaryGenre { get; set; }

        public DateTime? DigitalReleaseDate { get; set; }

        public DateTime? OriginalReleaseDate { get; set; }

        public bool HasUPC { get; set; }

        public string? UPCCode { get; set; }

        public List<ContributorDto> Contributors { get; set; } = new();

        [Required]
        public DistributionOptionDto DistributionOption { get; set; } = new();

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

    public class LocalizationDto
    {
        [Required]
        public string LanguageCode { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Description { get; set; }
    }

    public class DistributionOptionDto
    {
        [Required]
        public string DistributionType { get; set; } = string.Empty; // "SelectAll" or "Manual"

        public List<int> SelectedStoreIds { get; set; } = new(); // Used when DistributionType is "Manual"
    }

    public class UpdateReleaseDto
    {
        public string? Title { get; set; }

        public string? TitleVersion { get; set; }

        public string? Description { get; set; }

        public string? CoverArtUrl { get; set; }

        public string? PrimaryGenre { get; set; }

        public string? SecondaryGenre { get; set; }

        public DateTime? DigitalReleaseDate { get; set; }

        public DateTime? OriginalReleaseDate { get; set; }

        public bool? HasUPC { get; set; }

        public string? UPCCode { get; set; }

        // Optional: replace all existing contributors if provided
        public List<ContributorDto>? Contributors { get; set; }

        // Optional: replace distribution option if provided
        public DistributionOptionDto? DistributionOption { get; set; }
    }
}

