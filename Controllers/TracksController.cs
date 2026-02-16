using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers;

    [ApiController]
    [Route("api/tracks")]
    [Tags("Section 7 - Tracks")]
[Authorize]
public class TracksController : ControllerBase
{
    private readonly string _connStr;
    private readonly ILogger<TracksController> _logger;

    public TracksController(IConfiguration configuration, ILogger<TracksController> logger)
    {
        _connStr = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTrack([FromBody] CreateTrackRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        var role = GetUserRole();

        // For Artists: Allow if they created the release OR are adding themselves as primary artist
        bool hasAccess = false;
        if (string.Equals(role, "Artist", StringComparison.OrdinalIgnoreCase))
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            
            // First check if user created the release - if so, allow any artist to be added
            const string createdBySql = "SELECT 1 FROM Releases WHERE ReleaseID = @ReleaseId AND CreatedBy = @UserId;";
            await using var createdByCmd = new SqlCommand(createdBySql, conn);
            createdByCmd.Parameters.AddWithValue("@ReleaseId", request.ReleaseId);
            createdByCmd.Parameters.AddWithValue("@UserId", userId);
            var createdRelease = await createdByCmd.ExecuteScalarAsync();
            
            if (createdRelease != null)
            {
                // User created the release - allow them to add any artist
                hasAccess = true;
            }
            else if (request.PrimaryArtistId.HasValue && request.PrimaryArtistId.Value > 0)
            {
                // Check if the primaryArtistId belongs to this user's claimed artist
                const string checkArtistSql = @"
                    SELECT 1 
                    FROM Artists 
                    WHERE ArtistId = @ArtistId AND ClaimedUserId = @UserId;";
                await using var checkArtistCmd = new SqlCommand(checkArtistSql, conn);
                checkArtistCmd.Parameters.AddWithValue("@ArtistId", request.PrimaryArtistId.Value);
                checkArtistCmd.Parameters.AddWithValue("@UserId", userId);
                var artistOwned = await checkArtistCmd.ExecuteScalarAsync();
                
                if (artistOwned != null)
                {
                    // Artist is adding themselves - allow it
                    hasAccess = true;
                }
            }
            
            // If still no access, check other access methods (contributor, existing tracks)
            if (!hasAccess)
            {
                hasAccess = await HasAccessToReleaseAsync(request.ReleaseId, userId, role);
            }
        }
        else
        {
            // For non-Artists, use normal access check
            hasAccess = await HasAccessToReleaseAsync(request.ReleaseId, userId, role);
        }

        if (!hasAccess)
        {
            // Provide more helpful error message for Artists
            if (string.Equals(role, "Artist", StringComparison.OrdinalIgnoreCase))
            {
                // Get user's claimed artist ID to help them
                int? claimedArtistId = null;
                await using (var conn2 = new SqlConnection(_connStr))
                {
                    await conn2.OpenAsync();
                    const string getClaimedArtistSql = "SELECT ArtistId FROM Artists WHERE ClaimedUserId = @UserId;";
                    await using var getArtistCmd = new SqlCommand(getClaimedArtistSql, conn2);
                    getArtistCmd.Parameters.AddWithValue("@UserId", userId);
                    var artistIdResult = await getArtistCmd.ExecuteScalarAsync();
                    if (artistIdResult != null)
                    {
                        claimedArtistId = Convert.ToInt32(artistIdResult);
                    }
                }
                
                return StatusCode(403, new { 
                    error = "Access denied. You can create tracks if: (1) You created the release, (2) Your claimed artist is a contributor, (3) Your claimed artist already has tracks on this release, or (4) You're adding yourself as the primary artist.",
                    userId = userId,
                    releaseId = request.ReleaseId,
                    primaryArtistId = request.PrimaryArtistId,
                    yourClaimedArtistId = claimedArtistId,
                    hint = claimedArtistId.HasValue 
                        ? $"Try using 'primaryArtistId': {claimedArtistId.Value} to add yourself as the primary artist."
                        : "You need to claim an artist first, or have your artist added as a contributor to this release."
                });
            }
            return Forbid();
        }

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_CreateTrack", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Map to legacy stored procedure parameters
            cmd.Parameters.AddWithValue("@ReleaseId", request.ReleaseId);
            cmd.Parameters.AddWithValue("@TrackNumber", request.TrackNumber);
            cmd.Parameters.AddWithValue("@Title", request.Title);
            cmd.Parameters.AddWithValue("@DurationSeconds", (object?)request.DurationSeconds ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExplicitFlag", (object?)request.ExplicitFlag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ISRC", string.IsNullOrWhiteSpace(request.ISRC) ? DBNull.Value : request.ISRC);
            cmd.Parameters.AddWithValue("@Language", string.IsNullOrWhiteSpace(request.Language) ? DBNull.Value : request.Language);
            cmd.Parameters.AddWithValue("@TrackVersion", string.IsNullOrWhiteSpace(request.TrackVersion) ? DBNull.Value : request.TrackVersion);
            // Stored procedure expects @ArtistId, not @PrimaryArtistId
            cmd.Parameters.AddWithValue("@ArtistId", (object?)request.PrimaryArtistId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AudioFileId", (object?)request.AudioFileId ?? DBNull.Value);

            // New columns - provide safe defaults so NOT NULL columns are satisfied
            // We treat ExplicitFlag as the public flag and mirror it into IsExplicit
            var isExplicit = request.ExplicitFlag ?? false;
            cmd.Parameters.AddWithValue("@Lyrics", DBNull.Value);
            cmd.Parameters.AddWithValue("@IsExplicit", isExplicit);
            cmd.Parameters.AddWithValue("@IsInstrumental", false);
            cmd.Parameters.AddWithValue("@PreviewStartSeconds", DBNull.Value);
            cmd.Parameters.AddWithValue("@PreviewStartTimeSeconds", DBNull.Value);
            cmd.Parameters.AddWithValue("@TrackGenre", DBNull.Value);
            cmd.Parameters.AddWithValue("@AudioUrl", DBNull.Value);

            var trackId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Insert contributors into TrackContributors table
            if (request.Contributors != null)
            {
                // Helper to get artist name from artist ID
                async Task<string?> GetArtistNameAsync(int artistId)
                {
                    try
                    {
                        using var artistCmd = new SqlCommand("SELECT ArtistName FROM Artists WHERE ArtistId = @ArtistId", conn);
                        artistCmd.Parameters.AddWithValue("@ArtistId", artistId);
                        var result = await artistCmd.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                    catch
                    {
                        return null;
                    }
                }

                // Helper to insert contributor
                async Task InsertContributorAsync(string contributorName, string role)
                {
                    try
                    {
                        using var contribCmd = new SqlCommand(@"
                            INSERT INTO TrackContributors (TrackID, ContributorName, Role, CreatedAt)
                            VALUES (@TrackId, @ContributorName, @Role, SYSUTCDATETIME())", conn);
                        contribCmd.Parameters.AddWithValue("@TrackId", trackId);
                        contribCmd.Parameters.AddWithValue("@ContributorName", contributorName);
                        contribCmd.Parameters.AddWithValue("@Role", role);
                        await contribCmd.ExecuteNonQueryAsync();
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogWarning(ex, "Failed to insert contributor {ContributorName} with role {Role} for track {TrackId}", contributorName, role, trackId);
                        // Continue with other contributors even if one fails
                    }
                }

                // Process Primary Artists
                if (request.Contributors.PrimaryArtist != null && request.Contributors.PrimaryArtist.Any(id => id > 0))
                {
                    foreach (var artistId in request.Contributors.PrimaryArtist.Where(id => id > 0))
                    {
                        var artistName = await GetArtistNameAsync(artistId);
                        if (!string.IsNullOrWhiteSpace(artistName))
                        {
                            await InsertContributorAsync(artistName, "Primary Artist");
                        }
                    }
                }

                // Process Featured Artists
                if (request.Contributors.FeaturedArtist != null && request.Contributors.FeaturedArtist.Any(id => id > 0))
                {
                    foreach (var artistId in request.Contributors.FeaturedArtist.Where(id => id > 0))
                    {
                        var artistName = await GetArtistNameAsync(artistId);
                        if (!string.IsNullOrWhiteSpace(artistName))
                        {
                            await InsertContributorAsync(artistName, "Featured Artist");
                        }
                    }
                }

                // Process Producers
                if (request.Contributors.Producer != null && request.Contributors.Producer.Any(id => id > 0))
                {
                    foreach (var artistId in request.Contributors.Producer.Where(id => id > 0))
                    {
                        var artistName = await GetArtistNameAsync(artistId);
                        if (!string.IsNullOrWhiteSpace(artistName))
                        {
                            await InsertContributorAsync(artistName, "Producer");
                        }
                    }
                }

                // Process Composers
                if (request.Contributors.Composer != null && request.Contributors.Composer.Any(id => id > 0))
                {
                    foreach (var artistId in request.Contributors.Composer.Where(id => id > 0))
                    {
                        var artistName = await GetArtistNameAsync(artistId);
                        if (!string.IsNullOrWhiteSpace(artistName))
                        {
                            await InsertContributorAsync(artistName, "Composer");
                        }
                    }
                }

                // Process Lyricists
                if (request.Contributors.Lyricist != null && request.Contributors.Lyricist.Any(id => id > 0))
                {
                    foreach (var artistId in request.Contributors.Lyricist.Where(id => id > 0))
                    {
                        var artistName = await GetArtistNameAsync(artistId);
                        if (!string.IsNullOrWhiteSpace(artistName))
                        {
                            await InsertContributorAsync(artistName, "Lyricist");
                        }
                    }
                }
            }

            return CreatedAtAction(nameof(GetTrack), new { trackId }, new { trackId });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to create track");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{trackId:int}")]
    public async Task<IActionResult> GetTrack(int trackId)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        const string sql = @"
            SELECT 
                TrackId, 
                ReleaseId, 
                TrackNumber, 
                Title, 
                DurationSeconds, 
                ExplicitFlag, 
                ISRC, 
                Language, 
                TrackVersion, 
                AudioFileId
            FROM Tracks 
            WHERE TrackId = @TrackId;";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TrackId", trackId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return NotFound(new { error = "Track not found." });

        // Helper to safely read nullable columns
        object? SafeRead(string column)
        {
            try
            {
                var ord = reader.GetOrdinal(column);
                return reader.IsDBNull(ord) ? null : reader.GetValue(ord);
            }
            catch
            {
                return null;
            }
        }

        // Read all track data while reader is open
        int dbTrackId = reader.GetInt32(reader.GetOrdinal("TrackId"));
        int dbReleaseId = reader.GetInt32(reader.GetOrdinal("ReleaseId"));
        int dbTrackNumber = reader.GetInt32(reader.GetOrdinal("TrackNumber"));
        string? title = SafeRead("Title")?.ToString();
        string? trackVersion = SafeRead("TrackVersion")?.ToString();
        int? durationSeconds = SafeRead("DurationSeconds") as int?;
        bool? explicitFlag = SafeRead("ExplicitFlag") as bool?;
        string? isrc = SafeRead("ISRC")?.ToString();
        string? language = SafeRead("Language")?.ToString();
        
        int? audioFileId = null;
        var audioOrd = reader.GetOrdinal("AudioFileId");
        if (!reader.IsDBNull(audioOrd))
            audioFileId = reader.GetInt32(audioOrd);

        // Close the reader before querying contributors
        await reader.CloseAsync();

        // Get contributors in grouped format - TrackContributors table uses ContributorName, not ArtistID
        var contributorsDict = new Dictionary<string, List<int>>
        {
            { "primaryArtist", new List<int>() },
            { "featuredArtist", new List<int>() },
            { "producer", new List<int>() },
            { "composer", new List<int>() },
            { "lyricist", new List<int>() }
        };

        try
        {
            // Check if TrackContributors table exists
            var tableExistsCmd = new SqlCommand("SELECT OBJECT_ID('dbo.TrackContributors', 'U')", conn);
            var tableExists = await tableExistsCmd.ExecuteScalarAsync();
            
            if (tableExists != DBNull.Value && tableExists != null)
            {
                // Query track contributors - join with Artists to get ArtistId from ContributorName
                const string contributorsSql = @"
                    SELECT tc.ContributorName, tc.Role, a.ArtistId
                    FROM TrackContributors tc
                    LEFT JOIN Artists a ON a.ArtistName = tc.ContributorName
                    WHERE tc.TrackID = @TrackId;";
                
                using var contribCmd = new SqlCommand(contributorsSql, conn);
                contribCmd.Parameters.AddWithValue("@TrackId", dbTrackId);
                using var contribReader = await contribCmd.ExecuteReaderAsync();
                
                while (await contribReader.ReadAsync())
                {
                    var role = contribReader["Role"]?.ToString();
                    var artistIdOrd = contribReader.GetOrdinal("ArtistId");
                    int? artistId = contribReader.IsDBNull(artistIdOrd) ? null : contribReader.GetInt32(artistIdOrd);
                    
                    // Skip if we don't have an artist ID
                    if (!artistId.HasValue || artistId.Value <= 0)
                        continue;
                    
                    var roleKey = role?.ToLowerInvariant() switch
                    {
                        "primary artist" => "primaryArtist",
                        "featured artist" => "featuredArtist",
                        "producer" => "producer",
                        "composer" => "composer",
                        "lyricist" => "lyricist",
                        "primaryartist" => "primaryArtist",
                        "featuredartist" => "featuredArtist",
                        _ => null
                    };
                    
                    if (roleKey != null && contributorsDict.ContainsKey(roleKey))
                    {
                        contributorsDict[roleKey].Add(artistId.Value);
                    }
                }
                await contribReader.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            // Ignore if TrackContributors table doesn't exist or other errors
            _logger.LogWarning(ex, "Failed to fetch contributors for track {TrackId}", dbTrackId);
        }

        var response = new
        {
            trackId = dbTrackId,
            releaseId = dbReleaseId,
            trackNumber = dbTrackNumber,
            title = title,
            trackVersion = trackVersion,
            durationSeconds = durationSeconds,
            explicitFlag = explicitFlag,
            isrc = isrc,
            language = language,
            audioFileId,
            // alias for frontend convenience – same value as audioFileId
            fileId = audioFileId,
            contributors = new
            {
                primaryArtist = contributorsDict["primaryArtist"],
                featuredArtist = contributorsDict["featuredArtist"],
                producer = contributorsDict["producer"],
                composer = contributorsDict["composer"],
                lyricist = contributorsDict["lyricist"]
            }
        };

        return Ok(response);
    }

    [HttpPut("{trackId:int}")]
    public async Task<IActionResult> UpdateTrack(int trackId, [FromBody] UpdateTrackRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var releaseId = await GetReleaseIdForTrackAsync(trackId);
        if (releaseId == null)
            return NotFound(new { error = "Track not found." });

        var userId = GetUserId();
        var role = GetUserRole();

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_UpdateTrack", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Map to legacy stored procedure parameters
            cmd.Parameters.AddWithValue("@TrackId", trackId);
            cmd.Parameters.AddWithValue("@Title", (object?)request.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DurationSeconds", (object?)request.DurationSeconds ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExplicitFlag", (object?)request.ExplicitFlag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ISRC", string.IsNullOrWhiteSpace(request.ISRC) ? DBNull.Value : request.ISRC);
            cmd.Parameters.AddWithValue("@Language", string.IsNullOrWhiteSpace(request.Language) ? DBNull.Value : request.Language);
            cmd.Parameters.AddWithValue("@TrackVersion", string.IsNullOrWhiteSpace(request.TrackVersion) ? DBNull.Value : request.TrackVersion);
            // Stored procedure expects @ArtistId, not @PrimaryArtistId
            cmd.Parameters.AddWithValue("@ArtistId", (object?)request.PrimaryArtistId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return Ok(new { success = true, message = "Track updated." });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to update track {TrackId}", trackId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{trackId:int}")]
    public async Task<IActionResult> DeleteTrack(int trackId)
    {
        var releaseId = await GetReleaseIdForTrackAsync(trackId);
        if (releaseId == null)
            return NotFound(new { error = "Track not found." });

        var userId = GetUserId();
        var role = GetUserRole();

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_DeleteTrack", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@TrackId", trackId);
            cmd.Parameters.AddWithValue("@DeletedBy", userId);

            await cmd.ExecuteNonQueryAsync();
            return Ok(new { success = true, message = "Track deleted." });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to delete track {TrackId}", trackId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{trackId:int}/audio")]
    public async Task<IActionResult> AttachAudio(int trackId, [FromBody] TrackAudioRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var releaseId = await GetReleaseIdForTrackAsync(trackId);
        if (releaseId == null)
            return NotFound(new { error = "Track not found." });

        var userId = GetUserId();
        var role = GetUserRole();

        if (!await HasAccessToReleaseAsync(releaseId.Value, userId, role))
            return Forbid();

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_AttachTrackAudio", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@TrackId", trackId);
            cmd.Parameters.AddWithValue("@FileId", request.FileId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await cmd.ExecuteNonQueryAsync();
            return Ok(new { success = true, message = "Audio associated; QC re-run scheduled as needed.", fileId = request.FileId });
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to attach audio to track {TrackId}", trackId);
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
            if (trackArtistResult != null)
                return true;

            // Allow if the artist is trying to add themselves (check primaryArtistId in request)
            // Note: This check happens in the CreateTrack method itself since we need the request data
            return false;
        }

        return false;
    }

    #endregion
}

