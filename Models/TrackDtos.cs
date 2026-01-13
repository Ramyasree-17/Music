using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models;

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





























