using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models;

public class FileUploadRequest
{
    [Required]
    public int ReleaseId { get; set; }

    public int? TrackId { get; set; }

    [Required]
    [MaxLength(20)]
    public string FileType { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long? ExpectedFileSize { get; set; }
}

public class FileCompleteRequest
{
    [Required]
    public int FileId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Checksum { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [MaxLength(1000)]
    public string? CloudfrontUrl { get; set; }

    [MaxLength(1000)]
    public string? BackupUrl { get; set; }
}

public class FileReplaceRequest
{
    [Required]
    public int OldFileId { get; set; }

    [Required]
    [MaxLength(20)]
    public string FileType { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;
}
