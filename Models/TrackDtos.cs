using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TunewaveAPIDB1.Models;

public class TrackContributorsDto
{
    [JsonPropertyName("primaryArtist")]
    public List<int>? PrimaryArtist { get; set; }

    [JsonPropertyName("featuredArtist")]
    public List<int>? FeaturedArtist { get; set; }

    [JsonPropertyName("producer")]
    public List<int>? Producer { get; set; }

    [JsonPropertyName("composer")]
    public List<int>? Composer { get; set; }

    [JsonPropertyName("lyricist")]
    public List<int>? Lyricist { get; set; }
}

public class CreateTrackRequest
{
    [Required]
    public int ReleaseId { get; set; }

    [Required]
    public int TrackNumber { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int? DurationSeconds { get; set; }

    public bool? ExplicitFlag { get; set; }

    [MaxLength(50)]
    public string? ISRC { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    [MaxLength(100)]
    public string? TrackVersion { get; set; }

    public int? PrimaryArtistId { get; set; }

    public int? AudioFileId { get; set; }

    public TrackContributorsDto? Contributors { get; set; }
}

public class UpdateTrackRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    public int? DurationSeconds { get; set; }

    public bool? ExplicitFlag { get; set; }

    [MaxLength(50)]
    public string? ISRC { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    [MaxLength(100)]
    public string? TrackVersion { get; set; }

    public int? PrimaryArtistId { get; set; }
}

public class TrackAudioRequest
{
    [Required]
    public int FileId { get; set; }
}





























