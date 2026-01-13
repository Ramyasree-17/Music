using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Services;

namespace TunewaveAPIDB1.Controllers;

[ApiController]
[Route("api/files")]
[Tags("Section 8 - Files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly string _connStr;
    private readonly ILogger<FilesController> _logger;
    private readonly IConfiguration _configuration;
    private readonly CdnService _cdnService;
    private readonly BackupService _backupService;
    private readonly IWebHostEnvironment _env;

    private static readonly Dictionary<string, byte> FileTypeLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio"] = 1,
        ["artwork"] = 2,
        ["backup"] = 3,
        ["metadata"] = 4,
        ["other"] = 5
    };

    public FilesController(
        IConfiguration configuration,
        ILogger<FilesController> logger,
        CdnService cdnService,
        BackupService backupService,
        IWebHostEnvironment env)
    {
        _configuration = configuration;
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
        _cdnService = cdnService;
        _backupService = backupService;
        _env = env;

    }


    /// <summary>
    /// Get all files/tracks with CloudFront and Backup URLs.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllFiles(
        [FromQuery] int? releaseId = null,
        [FromQuery] int? trackId = null,
        [FromQuery] string? fileType = null,
        [FromQuery] string? status = null)
    {
        var userId = GetUserId();
        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var sql = @"
                SELECT 
                    f.FileId,
                    f.ReleaseId,
                    r.Title AS ReleaseTitle,
                    f.TrackId,
                    t.Title AS TrackTitle,
                    t.TrackNumber,
                    f.FileTypeId,
                    f.S3Key,
                    f.CloudfrontUrl,
                    f.BackupUrl,
                    f.Status,
                    f.FileSizeBytes,
                    f.Checksum,
                    f.CreatedAt,
                    u.FullName AS CreatedBy
                FROM Files f
                LEFT JOIN Releases r ON r.ReleaseID = f.ReleaseId
                LEFT JOIN Tracks t ON t.TrackID = f.TrackId
                LEFT JOIN Users u ON u.UserID = f.CreatedByUserId
                WHERE 1=1";

            var parameters = new List<SqlParameter>();

            if (releaseId.HasValue)
            {
                sql += " AND f.ReleaseId = @ReleaseId";
                parameters.Add(new SqlParameter("@ReleaseId", releaseId.Value));
            }

            if (trackId.HasValue)
            {
                sql += " AND f.TrackId = @TrackId";
                parameters.Add(new SqlParameter("@TrackId", trackId.Value));
            }

            if (!string.IsNullOrWhiteSpace(fileType))
            {
                if (FileTypeLookup.TryGetValue(fileType.ToLowerInvariant(), out var fileTypeId))
                {
                    sql += " AND f.FileTypeId = @FileTypeId";
                    parameters.Add(new SqlParameter("@FileTypeId", fileTypeId));
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                sql += " AND f.Status = @Status";
                parameters.Add(new SqlParameter("@Status", status));
            }

            sql += " ORDER BY f.CreatedAt DESC";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters.ToArray());

            await using var reader = await cmd.ExecuteReaderAsync();
            var files = new List<object>();

            while (await reader.ReadAsync())
            {
                var fileTypeId = reader["FileTypeId"] != DBNull.Value ? Convert.ToByte(reader["FileTypeId"]) : (byte?)null;
                var s3Key = reader["S3Key"]?.ToString();
                var cloudfrontUrl = reader["CloudfrontUrl"]?.ToString();
                var backupUrl = reader["BackupUrl"]?.ToString();
                var fileStatus = reader["Status"]?.ToString();

                // Map FileTypeId to string
                string fileTypeString = fileTypeId switch
                {
                    1 => "Audio",
                    2 => "Artwork",
                    3 => "Backup",
                    4 => "Metadata",
                    5 => "Other",
                    _ => "Unknown"
                };

                // Auto-generate CloudFront URL if missing and status is AVAILABLE
                if (string.IsNullOrWhiteSpace(cloudfrontUrl) && !string.IsNullOrWhiteSpace(s3Key) && fileStatus == "AVAILABLE")
                {
                    cloudfrontUrl = _cdnService.GenerateCloudFrontUrl(s3Key);
                }

                // Check access for each file's release
                var fileReleaseId = reader["ReleaseId"] != DBNull.Value ? Convert.ToInt32(reader["ReleaseId"]) : (int?)null;
                bool hasAccess = fileReleaseId.HasValue && await HasAccessToReleaseAsync(fileReleaseId.Value, userId, role);

                // Only include files user has access to
                if (hasAccess || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(new
                    {
                        fileId = reader["FileId"],
                        releaseId = reader["ReleaseId"],
                        releaseTitle = reader["ReleaseTitle"]?.ToString(),
                        trackId = reader["TrackId"],
                        trackTitle = reader["TrackTitle"]?.ToString(),
                        trackNumber = reader["TrackNumber"],
                        fileType = fileTypeString,
                        s3Key = s3Key,
                        cloudfrontUrl = fileStatus == "AVAILABLE" ? cloudfrontUrl : null,
                        backupUrl = fileStatus == "AVAILABLE" ? backupUrl : null,
                        status = fileStatus,
                        fileSizeBytes = reader["FileSizeBytes"],
                        checksum = reader["Checksum"]?.ToString(),
                        createdAt = reader["CreatedAt"],
                        createdBy = reader["CreatedBy"]?.ToString()
                    });
                }
            }

            return Ok(new
            {
                totalFiles = files.Count,
                files = files
            });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch files list");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> RequestUpload([FromBody] FileUploadRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var role = GetUserRole();

        if (!await HasAccessToReleaseAsync(request.ReleaseId, userId, role))
            return Forbid();

        if (request.TrackId.HasValue)
        {
            var releaseId = await GetReleaseIdForTrackAsync(request.TrackId.Value);
            if (releaseId == null || releaseId.Value != request.ReleaseId)
                return BadRequest(new { error = "Track does not belong to the specified release." });
        }

        try
        {
            byte fileTypeId = ResolveFileType(request.FileType);

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_FileRequestUpload", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", request.ReleaseId);
            cmd.Parameters.AddWithValue("@TrackId", (object?)request.TrackId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FileTypeId", fileTypeId);
            cmd.Parameters.AddWithValue("@FileName", request.FileName);
            cmd.Parameters.AddWithValue("@ContentType", request.ContentType);
            cmd.Parameters.AddWithValue("@ExpectedSize", (object?)request.ExpectedFileSize ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return StatusCode(500, new { error = "Failed to create file record." });

            var fileId = reader.GetInt32(0);
            var storageKey = reader.GetString(1);

            // Generate presigned URL (15 minutes expiry as per spec)
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
            var uploadUrl = BuildUploadUrl(storageKey, request.ContentType, expiresAt);

            return Ok(new
            {
                fileId,
                uploadUrl,
                expiresAt = expiresAt.ToString("O") // ISO 8601 format
            });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to create upload request");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteUpload([FromBody] FileCompleteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var role = GetUserRole();

        var releaseId = await GetReleaseIdForFileAsync(request.FileId);
        if (releaseId == null)
            return NotFound(new { error = "File not found." });

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Fetch file details and validate status (Section 8.2: validate Status is UPLOADING or VERIFYING)
            string? s3Key = null;
            byte? fileTypeId = null;
            string? currentStatus = null;
            int? trackId = null;

            const string getFileSql = @"
                SELECT S3Key, FileTypeId, Status, TrackId
                FROM Files 
                WHERE FileId = @FileId;";
            await using var getFileCmd = new SqlCommand(getFileSql, conn);
            getFileCmd.Parameters.AddWithValue("@FileId", request.FileId);
            await using var fileReader = await getFileCmd.ExecuteReaderAsync();
            if (await fileReader.ReadAsync())
            {
                s3Key = fileReader["S3Key"]?.ToString();
                fileTypeId = fileReader["FileTypeId"] != DBNull.Value ? Convert.ToByte(fileReader["FileTypeId"]) : null;
                currentStatus = fileReader["Status"]?.ToString();
                trackId = fileReader["TrackId"] != DBNull.Value ? Convert.ToInt32(fileReader["TrackId"]) : null;
            }
            await fileReader.CloseAsync();

            if (string.IsNullOrWhiteSpace(s3Key))
            {
                return NotFound(new { error = "File not found." });
            }

            // Validate status (Section 8.2: Status must be UPLOADING or VERIFYING)
            if (currentStatus != "UPLOADING" && currentStatus != "VERIFYING")
            {
                return Conflict(new { error = "File already completed or in wrong state.", currentStatus });
            }

            // Extract fileName from S3Key (format: labels/X/releases/Y/tracks/Z/GUID_filename.ext)
            string? fileName = null;
            if (!string.IsNullOrWhiteSpace(s3Key))
            {
                var parts = s3Key.Split('/');
                if (parts.Length > 0)
                {
                    var lastPart = parts[^1];
                    var underscoreIndex = lastPart.IndexOf('_');
                    if (underscoreIndex > 0 && underscoreIndex < lastPart.Length - 1)
                    {
                        fileName = lastPart.Substring(underscoreIndex + 1);
                    }
                    else
                    {
                        fileName = lastPart;
                    }
                }
            }

            // Generate CloudFront URL if not provided (Section 8.6: CDN integration)
            string? cloudfrontUrl = request.CloudfrontUrl;
            if (string.IsNullOrWhiteSpace(cloudfrontUrl))
            {
                cloudfrontUrl = _cdnService.GenerateCloudFrontUrl(s3Key);
                _logger.LogInformation("Auto-generated CloudFront URL for FileId {FileId}: {CloudFrontUrl}", request.FileId, cloudfrontUrl);
            }

            // Update file status to AVAILABLE (Section 8.2: Status = 'AVAILABLE')
            await using var cmd = new SqlCommand("sp_FileCompleteUpload", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@FileId", request.FileId);
            cmd.Parameters.AddWithValue("@Checksum", request.Checksum);
            cmd.Parameters.AddWithValue("@FileSize", request.FileSize);
            cmd.Parameters.AddWithValue("@CloudfrontUrl", cloudfrontUrl);
            cmd.Parameters.AddWithValue("@BackupUrl", string.IsNullOrWhiteSpace(request.BackupUrl) ? DBNull.Value : request.BackupUrl);

            await cmd.ExecuteNonQueryAsync();

            // If file is associated with TrackId, set Tracks.AudioFileId if not already set (Section 8.2)
            if (trackId.HasValue && fileTypeId == 1) // Audio file
            {
                const string updateTrackSql = @"
                    UPDATE Tracks
                    SET AudioFileId = @FileId
                    WHERE TrackId = @TrackId 
                      AND (AudioFileId IS NULL OR AudioFileId = 0);";
                await using var updateTrackCmd = new SqlCommand(updateTrackSql, conn);
                updateTrackCmd.Parameters.AddWithValue("@TrackId", trackId.Value);
                updateTrackCmd.Parameters.AddWithValue("@FileId", request.FileId);
                await updateTrackCmd.ExecuteNonQueryAsync();
            }

            // Check if this file is replacing another file (Section 8.2: handle file replacement)
            // Look for files with ReplacedByFileId pointing to this file
            const string checkReplacementSql = @"
                SELECT FileId FROM Files 
                WHERE ReplacedByFileId = @FileId;";
            await using var checkReplacementCmd = new SqlCommand(checkReplacementSql, conn);
            checkReplacementCmd.Parameters.AddWithValue("@FileId", request.FileId);
            var oldFileIdObj = await checkReplacementCmd.ExecuteScalarAsync();

            if (oldFileIdObj != null)
            {
                // This shouldn't happen in normal flow, but handle it if it does
                _logger.LogInformation("File {FileId} is replacing file {OldFileId}", request.FileId, oldFileIdObj);
            }

            // Enqueue backup job (Section 8.6: Backup integration)
            if (fileTypeId.HasValue && !string.IsNullOrWhiteSpace(fileName))
            {
                await _backupService.EnqueueBackupJobAsync(request.FileId, s3Key, fileName);
            }

            // Return response per Section 8.2 spec
            return Ok(new
            {
                fileId = request.FileId,
                status = "AVAILABLE",
                cloudfrontUrl = cloudfrontUrl
            });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to complete upload for file {FileId}", request.FileId);

            // Handle specific SQL errors
            if (ex.Message.Contains("already completed") || ex.Message.Contains("wrong state"))
            {
                return Conflict(new { error = ex.Message });
            }

            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("status/{fileId:int}")]
    public async Task<IActionResult> GetStatus(int fileId)
    {
        var userId = GetUserId();
        var role = GetUserRole();

        var releaseId = await GetReleaseIdForFileAsync(fileId);
        if (releaseId == null)
            return NotFound(new { error = "File not found." });

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_FileGetStatus", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@FileId", fileId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return NotFound(new { error = "File not found." });

            var fileTypeId = reader["FileTypeId"] != DBNull.Value ? Convert.ToByte(reader["FileTypeId"]) : (byte?)null;
            var status = reader["Status"]?.ToString();
            var s3Key = reader["S3Key"]?.ToString();
            var cloudfrontUrl = reader["CloudfrontUrl"]?.ToString();
            var backupUrl = reader["BackupUrl"]?.ToString();

            // Map FileTypeId to string (Section 8.3: return fileType as string)
            string fileTypeString = fileTypeId switch
            {
                1 => "Audio",
                2 => "Artwork",
                3 => "Backup",
                4 => "Metadata",
                5 => "Other",
                _ => "Unknown"
            };

            // If CloudFront URL is missing and status is AVAILABLE, generate it
            if (string.IsNullOrWhiteSpace(cloudfrontUrl) && !string.IsNullOrWhiteSpace(s3Key) && status == "AVAILABLE")
            {
                cloudfrontUrl = _cdnService.GenerateCloudFrontUrl(s3Key);
            }

            // Return CDN URLs only if status == AVAILABLE (Section 8.3)
            var response = new
            {
                fileId = reader["FileId"],
                releaseId = reader["ReleaseId"],
                trackId = reader["TrackId"],
                fileType = fileTypeString, // Section 8.3: return as string
                status = status,
                s3Key = s3Key,
                cloudfrontUrl = status == "AVAILABLE" ? cloudfrontUrl : null, // Only return if AVAILABLE
                backupUrl = status == "AVAILABLE" ? backupUrl : null, // Only return if AVAILABLE
                checksum = reader["Checksum"],
                fileSizeBytes = reader["FileSizeBytes"],
                createdAt = reader["CreatedAt"]
            };

            return Ok(response);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch file status {FileId}", fileId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a signed CDN URL for protected content (Section 8.6: Signed URLs for protected content).
    /// </summary>
    [HttpGet("signed-url/{fileId:int}")]
    public async Task<IActionResult> GetSignedUrl(int fileId, [FromQuery] int expiresInMinutes = 60)
    {
        var userId = GetUserId();
        var role = GetUserRole();

        var releaseId = await GetReleaseIdForFileAsync(fileId);
        if (releaseId == null)
            return NotFound(new { error = "File not found." });

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            const string sql = "SELECT S3Key FROM Files WHERE FileId = @FileId;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@FileId", fileId);

            var s3Key = await cmd.ExecuteScalarAsync() as string;
            if (string.IsNullOrWhiteSpace(s3Key))
            {
                return NotFound(new { error = "File S3Key not found." });
            }

            var signedUrl = _cdnService.GenerateSignedUrl(s3Key, expiresInMinutes);

            return Ok(new
            {
                fileId,
                signedUrl,
                expiresInMinutes,
                expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes)
            });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to generate signed URL for file {FileId}", fileId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Backup all files to Google Drive (bulk sync).
    /// Syncs all tracks/files that don't have BackupUrl yet.
    /// Artists can backup files from releases they have access to.
    /// </summary>
    /// 


    //[HttpPost("backup-all")]
    //public async Task<IActionResult> BackupAllFiles([FromQuery] int? releaseId = null, [FromQuery] bool force = false)
    //{
    //    var userId = GetUserId();
    //    var role = GetUserRole();

    //    try
    //    {
    //        await using var conn = new SqlConnection(_connStr);
    //        await conn.OpenAsync();

    //        // Build query to get files user has access to
    //        var sql = @"
    //            SELECT DISTINCT f.FileId, f.S3Key, f.CloudfrontUrl, f.CreatedAt
    //            FROM Files f
    //            WHERE f.Status = 'AVAILABLE'
    //              AND f.S3Key IS NOT NULL";

    //        // If force=false, only backup files without BackupUrl
    //        // If force=true, also backup files with simulated/incorrect BackupUrl
    //        if (!force)
    //        {
    //            sql += " AND f.BackupUrl IS NULL";
    //        }
    //        else
    //        {
    //            // Force backup: include files with no BackupUrl OR files with simulated BackupUrl
    //            sql += @" AND (f.BackupUrl IS NULL 
    //                       OR f.BackupUrl LIKE '%backup.tunewave.in%'
    //                       OR f.BackupUrl NOT LIKE '%drive.google.com%')";
    //        }

    //        var parameters = new List<SqlParameter>();

    //        // If Artist role, only backup files from releases they have access to
    //        if (string.Equals(role, "Artist", StringComparison.OrdinalIgnoreCase))
    //        {
    //            sql += @"
    //              AND f.ReleaseId IN (
    //                -- Releases created by user
    //                SELECT ReleaseID FROM Releases WHERE CreatedBy = @UserId
    //                UNION
    //                -- Releases where user's artist is a contributor
    //                SELECT DISTINCT rc.ReleaseID 
    //                FROM ReleaseContributors rc
    //                INNER JOIN Artists a ON rc.ArtistID = a.ArtistId
    //                WHERE a.ClaimedUserId = @UserId
    //                UNION
    //                -- Releases where user's artist has tracks
    //                SELECT DISTINCT t.ReleaseId
    //                FROM Tracks t
    //                INNER JOIN TrackArtists ta ON ta.TrackID = t.TrackID
    //                INNER JOIN Artists a ON ta.ArtistID = a.ArtistId
    //                WHERE a.ClaimedUserId = @UserId
    //                  AND (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
    //              )";
    //            parameters.Add(new SqlParameter("@UserId", userId));
    //        }
    //        // If specific release requested, filter by it
    //        else if (releaseId.HasValue)
    //        {
    //            sql += " AND f.ReleaseId = @ReleaseId";
    //            parameters.Add(new SqlParameter("@ReleaseId", releaseId.Value));

    //            // Check access to this release
    //            if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
    //            {
    //                return Forbid();
    //            }
    //        }

    //        sql += " ORDER BY f.CreatedAt ASC";

    //        await using var cmd = new SqlCommand(sql, conn);
    //        cmd.Parameters.AddRange(parameters.ToArray());

    //        await using var reader = await cmd.ExecuteReaderAsync();
    //        var filesToBackup = new List<(int FileId, string S3Key, string? CloudfrontUrl)>();

    //        while (await reader.ReadAsync())
    //        {
    //            var fileId = reader.GetInt32(0);
    //            var s3Key = reader.GetString(1);
    //            var cloudfrontUrl = reader["CloudfrontUrl"]?.ToString();
    //            filesToBackup.Add((fileId, s3Key, cloudfrontUrl));
    //        }
    //        await reader.CloseAsync();

    //        _logger.LogInformation("Bulk backup started by user {UserId} ({Role}). Found {Count} files to backup.", userId, role, filesToBackup.Count);

    //        if (filesToBackup.Count == 0)
    //        {
    //            // Check if files have BackupUrl but might be simulated/incorrect
    //            const string checkBackupUrlSql = @"
    //                SELECT COUNT(*) 
    //                FROM Files f
    //                WHERE f.Status = 'AVAILABLE'
    //                  AND f.BackupUrl IS NOT NULL
    //                  AND (f.BackupUrl LIKE '%backup.tunewave.in%' OR f.BackupUrl NOT LIKE '%drive.google.com%')";

    //            await using var checkCmd = new SqlCommand(checkBackupUrlSql, conn);
    //            var filesWithIncorrectBackup = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);

    //            if (filesWithIncorrectBackup > 0)
    //            {
    //                return Ok(new
    //                {
    //                    success = true,
    //                    message = $"Found {filesWithIncorrectBackup} files with simulated/incorrect BackupUrl. Use ?force=true to re-backup them.",
    //                    filesBackedUp = 0,
    //                    filesWithIncorrectBackup = filesWithIncorrectBackup,
    //                    hint = "Add ?force=true to backup files even if BackupUrl exists"
    //                });
    //            }

    //            return Ok(new
    //            {
    //                success = true,
    //                message = "No files need backup. All accessible files are already backed up.",
    //                filesBackedUp = 0
    //            });
    //        }

    //        var provider = _configuration["Backup:Provider"] ?? "GoogleDrive";
    //        var successCount = 0;

    //        foreach (var (fileId, s3Key, cloudfrontUrl) in filesToBackup)
    //        {
    //            try
    //            {
    //                // Extract fileName from S3Key
    //                var fileName = s3Key.Split('/').LastOrDefault() ?? $"file_{fileId}";
    //                var underscoreIndex = fileName.IndexOf('_');
    //                if (underscoreIndex > 0 && underscoreIndex < fileName.Length - 1)
    //                {
    //                    fileName = fileName.Substring(underscoreIndex + 1);
    //                }

    //                // Use the backup service to backup this file
    //                await _backupService.EnqueueBackupJobAsync(fileId, s3Key, fileName);
    //                successCount++;

    //                // Small delay to avoid rate limiting
    //                await Task.Delay(500);
    //            }
    //            catch (Exception ex)
    //            {
    //                _logger.LogError(ex, "Failed to backup FileId {FileId}", fileId);
    //            }
    //        }

    //        _logger.LogInformation("Bulk backup completed. {SuccessCount} of {TotalCount} files backed up successfully", successCount, filesToBackup.Count);

    //        var providerName = provider.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) ? "OneDrive" : "Google Drive";

    //        return Ok(new
    //        {
    //            success = true,
    //            message = $"Backup initiated. {successCount} files will be backed up to {providerName}.",
    //            filesBackedUp = successCount,
    //            totalFiles = filesToBackup.Count,
    //            provider = providerName,
    //            note = "Backup runs in background. Check logs for progress. Verify credentials are configured in appsettings.json."
    //        });
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Bulk backup failed");
    //        return StatusCode(500, new { error = $"Backup failed: {ex.Message}" });
    //    }
    //}






    [HttpPost("UploadAudio")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> UploadAudio(IFormFile file, int releaseId, int trackId)
    {
        string filePath = null;  // ✅ Declare early so all scopes can access it

        if (file == null || file.Length == 0)
            return BadRequest("File not selected");

        try
        {
            // 1) Validate Release
            using (var checkConn = new SqlConnection(_connStr))
            {
                await checkConn.OpenAsync();
                using var checkCmd = new SqlCommand("SELECT COUNT(1) FROM Releases WHERE ReleaseId = @ReleaseId", checkConn);
                checkCmd.Parameters.AddWithValue("@ReleaseId", releaseId);

                int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (exists == 0)
                    return BadRequest("ReleaseId does NOT exist in Releases table");
            }

            // 2) Build path
            string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadFolder = Path.Combine(webRootPath, "audio");

            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var safeFileName = Path.GetFileName(file.FileName);
            string fileName = $"{Guid.NewGuid():N}_{safeFileName}";
            filePath = Path.Combine(uploadFolder, fileName);
            string relativePath = $"/audio/{fileName}";

            // 3) Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4) Insert DB
            int generatedId;
            using (var conn = new SqlConnection(_connStr))
            {
                await conn.OpenAsync();

                string query = @"
                INSERT INTO Files (ReleaseId, TrackId, FileTypeId, S3Key, CloudfrontUrl)
                OUTPUT INSERTED.FileId
                VALUES (@ReleaseId, @TrackId, @FileTypeId, @S3Key, @CloudfrontUrl)";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                cmd.Parameters.AddWithValue("@TrackId", trackId);
                cmd.Parameters.AddWithValue("@FileTypeId", 1);
                cmd.Parameters.AddWithValue("@S3Key", relativePath);
                cmd.Parameters.AddWithValue("@CloudfrontUrl",
                    $"{Request.Scheme}://{Request.Host}/api/files/play/");

                generatedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            return Ok(new
            {
                FileId = generatedId,
                FilePath = relativePath,
                PlayUrl = $"{Request.Scheme}://{Request.Host}/api/files/play/{generatedId}"
            });
        }
        catch (Exception ex)
        {
            // optional cleanup
            if (filePath != null && System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);   // ✅ NOW valid
            }

            return StatusCode(500, ex.Message);
        }

    }




    [HttpGet("play/{id}")]
    public async Task<IActionResult> PlayAudio(int id)
    {
        using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        string query = "SELECT S3Key FROM Files WHERE FileId = @FileId";
        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@FileId", id);

        string relativePath = (string)await cmd.ExecuteScalarAsync();
        if (relativePath == null)
            return NotFound("Invalid FileId");

        string fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath))
            return NotFound("File not found");

        string ext = Path.GetExtension(fullPath).ToLower();
        string mime = ext switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };

        // Let ASP.NET Core handle ranges automatically
        return PhysicalFile(fullPath, mime, enableRangeProcessing: true);
    }















    [HttpPost("backup-all")]
    public async Task<IActionResult> BackupAllFiles(
    [FromQuery] int? releaseId = null,
    [FromQuery] bool force = false)
    {
        var userId = GetUserId();
        var role = GetUserRole();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Base query
            var sql = @"
            SELECT DISTINCT 
                f.FileId,
                f.S3Key,
                f.CloudfrontUrl,
                f.CreatedAt,
                f.ReleaseId
            FROM Files f
            WHERE f.Status = 'AVAILABLE'
              AND f.S3Key IS NOT NULL";

            // BackupUrl logic
            if (!force)
            {
                sql += " AND f.BackupUrl IS NULL";
            }
            else
            {
                // Only re-backup known incorrect / simulated backup URLs
                sql += @"
                AND (
                     f.BackupUrl IS NULL
                     OR f.BackupUrl LIKE '%backup.tunewave.in%'
                     OR f.BackupUrl LIKE '%simulated%'
                )";
            }

            var parameters = new List<SqlParameter>();

            // Artist-specific filtering
            if (string.Equals(role, "Artist", StringComparison.OrdinalIgnoreCase))
            {
                sql += @"
              AND f.ReleaseId IN (
                    SELECT ReleaseID FROM Releases WHERE CreatedBy = @UserId

                    UNION

                    SELECT DISTINCT rc.ReleaseID 
                    FROM ReleaseContributors rc
                    INNER JOIN Artists a ON rc.ArtistID = a.ArtistId
                    WHERE a.ClaimedUserId = @UserId

                    UNION

                    SELECT DISTINCT t.ReleaseId
                    FROM Tracks t
                    INNER JOIN TrackArtists ta ON ta.TrackID = t.TrackID
                    INNER JOIN Artists a ON ta.ArtistID = a.ArtistId
                    WHERE a.ClaimedUserId = @UserId
                      AND (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
              )";

                parameters.Add(new SqlParameter("@UserId", userId));
            }
            else if (releaseId.HasValue)
            {
                // When releaseId specified: restrict files
                sql += " AND f.ReleaseId = @ReleaseId";
                parameters.Add(new SqlParameter("@ReleaseId", releaseId.Value));

                // Access validation
                if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
                {
                    return Forbid();
                }
            }
            else
            {
                // Non-artist roles without releaseId → restrict by enterprise
                if (role.Equals("EnterpriseAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    sql += @"
                    AND f.ReleaseId IN (
                        SELECT ReleaseID FROM Releases WHERE EnterpriseID =
                            (SELECT EnterpriseID FROM Users WHERE UserID = @UserId)
                    )";

                    parameters.Add(new SqlParameter("@UserId", userId));
                }
            }

            sql += " ORDER BY f.CreatedAt ASC";

            // Execute query
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters.ToArray());

            await using var reader = await cmd.ExecuteReaderAsync();
            var filesToBackup = new List<(int FileId, string S3Key, string? CloudfrontUrl)>();

            while (await reader.ReadAsync())
            {
                var fileId = reader.GetInt32(0);
                var s3Key = reader.GetString(1);

                var cloudfrontUrl = reader.IsDBNull(2)
                    ? null
                    : reader.GetString(2);

                filesToBackup.Add((fileId, s3Key, cloudfrontUrl));
            }

            await reader.CloseAsync();

            _logger.LogInformation(
                "Bulk backup started by user {UserId} ({Role}). Found {Count} files.",
                userId, role, filesToBackup.Count
            );

            // If nothing to backup
            if (filesToBackup.Count == 0)
            {
                const string checkBackupUrlSql = @"
                SELECT COUNT(*) 
                FROM Files f
                WHERE f.Status = 'AVAILABLE'
                  AND f.BackupUrl IS NOT NULL
                  AND (f.BackupUrl LIKE '%backup.tunewave.in%' OR f.BackupUrl LIKE '%simulated%')";

                await using var checkCmd = new SqlCommand(checkBackupUrlSql, conn);
                var incorrectCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);

                if (incorrectCount > 0)
                {
                    return Ok(new
                    {
                        success = true,
                        message = $"Found {incorrectCount} incorrect/simulated BackupUrl files. Use ?force=true to re-backup.",
                        filesBackedUp = 0,
                        filesWithIncorrectBackup = incorrectCount
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "No files need backup. All accessible files are already backed up.",
                    filesBackedUp = 0
                });
            }

            // Backup provider
            var provider = _configuration["Backup:Provider"] ?? "GoogleDrive";
            var providerName = provider.Equals("OneDrive", StringComparison.OrdinalIgnoreCase)
                ? "OneDrive"
                : "Google Drive";

            var successCount = 0;

            // Backup loop
            foreach (var file in filesToBackup)
            {
                try
                {
                    // FileName extraction
                    var fileName = Path.GetFileName(file.S3Key);
                    var parts = fileName.Split('_', 2);
                    fileName = parts.Length == 2 ? parts[1] : fileName;

                    // Queue job
                    await _backupService.EnqueueBackupJobAsync(
                        file.FileId,
                        file.S3Key,
                        fileName
                    );

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup FileId {FileId}", file.FileId);
                }
            }

            _logger.LogInformation(
                "Bulk backup completed. {SuccessCount} of {TotalCount} files queued.",
                successCount, filesToBackup.Count
            );

            return Ok(new
            {
                success = true,
                message = $"Backup initiated. {successCount} files will be backed up to {providerName}.",
                filesBackedUp = successCount,
                totalFiles = filesToBackup.Count,
                provider = providerName,
                note = "Backup jobs run in background. Check logs for progress."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk backup failed");
            return StatusCode(500, new { error = $"Backup failed: {ex.Message}" });
        }
    }




    [HttpPost("replace")]
    public async Task<IActionResult> ReplaceFile([FromBody] FileReplaceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var role = GetUserRole();

        // Get old file details
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        int? releaseId = null;
        int? trackId = null;
        string? contentType = null;

        const string getOldFileSql = @"
            SELECT ReleaseId, TrackId, ContentType
            FROM Files
            WHERE FileId = @OldFileId;";
        await using var getOldFileCmd = new SqlCommand(getOldFileSql, conn);
        getOldFileCmd.Parameters.AddWithValue("@OldFileId", request.OldFileId);
        await using var oldFileReader = await getOldFileCmd.ExecuteReaderAsync();
        if (await oldFileReader.ReadAsync())
        {
            releaseId = oldFileReader["ReleaseId"] != DBNull.Value ? Convert.ToInt32(oldFileReader["ReleaseId"]) : null;
            trackId = oldFileReader["TrackId"] != DBNull.Value ? Convert.ToInt32(oldFileReader["TrackId"]) : null;
            contentType = oldFileReader["ContentType"]?.ToString() ?? "application/octet-stream";
        }
        await oldFileReader.CloseAsync();

        if (releaseId == null)
            return NotFound(new { error = "Original file not found." });

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            byte fileTypeId = ResolveFileType(request.FileType);

            // Create new Files row (Section 8.4: create new Files row and pre-signed URL)
            await using var cmd = new SqlCommand("sp_FileRequestUpload", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", releaseId.Value);
            cmd.Parameters.AddWithValue("@TrackId", (object?)trackId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FileTypeId", fileTypeId);
            cmd.Parameters.AddWithValue("@FileName", request.FileName);
            cmd.Parameters.AddWithValue("@ContentType", contentType);
            cmd.Parameters.AddWithValue("@ExpectedSize", DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return StatusCode(500, new { error = "Failed to create new file record." });

            var newFileId = reader.GetInt32(0);
            var storageKey = reader.GetString(1);

            // Store oldFileId reference in a temporary way (we'll use it when completing)
            // For now, we'll handle replacement in the complete endpoint by checking ReplacedByFileId

            // Generate presigned URL (15 minutes expiry)
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
            var uploadUrl = BuildUploadUrl(storageKey, contentType, expiresAt);

            // Return new file info (Section 8.4: return newFileId and uploadUrl)
            return Ok(new
            {
                newFileId = newFileId,
                uploadUrl = uploadUrl,
                expiresAt = expiresAt.ToString("O"),
                oldFileId = request.OldFileId // Include for client reference
            });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to create replacement file for old file {OldFile}", request.OldFileId);
            return BadRequest(new { error = ex.Message });
        }
    }

    #region Helpers

    private int GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Authenticated user does not contain NameIdentifier claim.");
        return int.Parse(value);
    }

    private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private byte ResolveFileType(string type)
    {
        if (!FileTypeLookup.TryGetValue(type.ToLowerInvariant(), out var id))
            throw new ArgumentException($"Unsupported fileType '{type}'.");
        return id;
    }

    private string BuildUploadUrl(string storageKey, string contentType, DateTimeOffset expiresAt)
    {
        // In production, this would generate a real presigned S3 URL
        // For now, return a placeholder URL with expiry info
        // Format: https://s3.amazonaws.com/bucket/...?X-Amz-Signature=...
        var baseUrl = $"https://upload.tunewave.local/{Uri.EscapeDataString(storageKey)}";
        var queryParams = $"?ContentType={Uri.EscapeDataString(contentType)}&Expires={expiresAt.ToUnixTimeSeconds()}";
        return baseUrl + queryParams;
    }

    private async Task<int?> GetReleaseIdForTrackAsync(int trackId)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();
        const string sql = "SELECT ReleaseId FROM Tracks WHERE TrackId = @TrackId;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TrackId", trackId);
        var result = await cmd.ExecuteScalarAsync();
        return result == null ? null : Convert.ToInt32(result);
    }

    private async Task<int?> GetReleaseIdForFileAsync(int fileId)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();
        const string sql = "SELECT ReleaseId FROM Files WHERE FileId = @FileId;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FileId", fileId);
        var result = await cmd.ExecuteScalarAsync();
        return result == null ? null : Convert.ToInt32(result);
    }

    private async Task<(int LabelId, int EnterpriseId)?> GetReleaseContextAsync(int releaseId)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        const string sql = @"
            SELECT r.LabelID, l.EnterpriseID
            FROM Releases r
            INNER JOIN Labels l ON l.LabelID = r.LabelID
            WHERE r.ReleaseID = @ReleaseId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private async Task<bool> HasAccessToReleaseAsync(int releaseId, int userId, string role)
    {
        var context = await GetReleaseContextAsync(releaseId);
        if (context == null)
            return false;

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        if (string.Equals(role, "EnterpriseAdmin", StringComparison.OrdinalIgnoreCase))
        {
            const string sql = "SELECT 1 FROM EnterpriseUserRoles WHERE EnterpriseID = @EnterpriseId AND UserID = @UserId;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EnterpriseId", context.Value.EnterpriseId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        if (string.Equals(role, "LabelAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "LabelEditor", StringComparison.OrdinalIgnoreCase))
        {
            const string sql = "SELECT 1 FROM UserLabelRoles WHERE LabelID = @LabelId AND UserID = @UserId;";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@LabelId", context.Value.LabelId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        // Allow Artists to access releases they created, are contributors to, or have tracks on
        if (string.Equals(role, "Artist", StringComparison.OrdinalIgnoreCase))
        {
            // Check if user created the release
            const string createdBySql = "SELECT 1 FROM Releases WHERE ReleaseID = @ReleaseId AND CreatedBy = @UserId;";
            await using var createdByCmd = new SqlCommand(createdBySql, conn);
            createdByCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            createdByCmd.Parameters.AddWithValue("@UserId", userId);
            var createdByResult = await createdByCmd.ExecuteScalarAsync();
            if (createdByResult != null)
                return true;

            // Check if user's claimed artist is a contributor to the release
            const string contributorSql = @"
                SELECT 1 
                FROM ReleaseContributors rc
                INNER JOIN Artists a ON rc.ArtistID = a.ArtistId
                WHERE rc.ReleaseID = @ReleaseId AND a.ClaimedUserId = @UserId;";
            await using var contributorCmd = new SqlCommand(contributorSql, conn);
            contributorCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            contributorCmd.Parameters.AddWithValue("@UserId", userId);
            var contributorResult = await contributorCmd.ExecuteScalarAsync();
            if (contributorResult != null)
                return true;

            // Check if user's claimed artist is already on any track in this release
            const string trackArtistSql = @"
                SELECT 1 
                FROM TrackArtists ta
                INNER JOIN Tracks t ON ta.TrackID = t.TrackID
                INNER JOIN Artists a ON ta.ArtistID = a.ArtistId
                WHERE t.ReleaseID = @ReleaseId 
                  AND a.ClaimedUserId = @UserId
                  AND (t.IsDeleted = 0 OR t.IsDeleted IS NULL);";
            await using var trackArtistCmd = new SqlCommand(trackArtistSql, conn);
            trackArtistCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            trackArtistCmd.Parameters.AddWithValue("@UserId", userId);
            var trackArtistResult = await trackArtistCmd.ExecuteScalarAsync();
            return trackArtistResult != null;
        }

        return false;
    }

    #endregion
}