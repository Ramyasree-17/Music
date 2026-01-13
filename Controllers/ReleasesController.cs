using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/releases")]
    [Tags("Section 6 - Releases")]
    [Authorize]
    public class ReleasesController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public ReleasesController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReleaseDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();
                try
                {
                    // Create release
                    // NOTE: Using backward-compatible parameters that exist in the current stored procedure
                    // Run Database_Scripts_Update_StoredProcedures.sql to add support for all new fields
                    using var cmd = new SqlCommand("sp_CreateRelease", conn, transaction)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    // Core required parameters (these should exist in the current stored procedure)
                    cmd.Parameters.AddWithValue("@Title", dto.Title);
                    cmd.Parameters.AddWithValue("@LabelId", dto.LabelId);
                    cmd.Parameters.AddWithValue("@CreatedBy", userId);
                    
                    // Optional parameters (mapping new and legacy fields)
                    cmd.Parameters.AddWithValue("@Description",
                        string.IsNullOrWhiteSpace(dto.Description) ? DBNull.Value : dto.Description);
                    cmd.Parameters.AddWithValue("@CoverArtUrl",
                        string.IsNullOrWhiteSpace(dto.CoverArtUrl) ? DBNull.Value : dto.CoverArtUrl);

                    // Legacy parameters used as fallbacks inside sp_CreateRelease
                    cmd.Parameters.AddWithValue("@Genre",
                        string.IsNullOrWhiteSpace(dto.PrimaryGenre) ? DBNull.Value : dto.PrimaryGenre);
                    cmd.Parameters.AddWithValue("@ReleaseDate",
                        (object?)dto.DigitalReleaseDate ?? DBNull.Value);

                    // New explicit parameters supported by updated sp_CreateRelease
                    cmd.Parameters.AddWithValue("@TitleVersion",
                        string.IsNullOrWhiteSpace(dto.TitleVersion) ? DBNull.Value : dto.TitleVersion);
                    cmd.Parameters.AddWithValue("@PrimaryGenre",
                        string.IsNullOrWhiteSpace(dto.PrimaryGenre) ? DBNull.Value : dto.PrimaryGenre);
                    cmd.Parameters.AddWithValue("@SecondaryGenre",
                        string.IsNullOrWhiteSpace(dto.SecondaryGenre) ? DBNull.Value : dto.SecondaryGenre);
                    cmd.Parameters.AddWithValue("@DigitalReleaseDate",
                        (object?)dto.DigitalReleaseDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriginalReleaseDate",
                        (object?)dto.OriginalReleaseDate ?? DBNull.Value);

                    var hasUpc = dto.HasUPC;
                    cmd.Parameters.AddWithValue("@HasUPC", hasUpc);
                    cmd.Parameters.AddWithValue("@UPCCode",
                        string.IsNullOrWhiteSpace(dto.UPCCode) ? DBNull.Value : dto.UPCCode);

                    var releaseId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    // Validate contributors - check if all artistIds exist (filter out invalid IDs like 0)
                    if (dto.Contributors != null && dto.Contributors.Any())
                    {
                        // Filter out invalid artist IDs (0 or negative)
                        var validContributors = dto.Contributors.Where(c => c.ArtistId > 0).ToList();
                        
                        if (validContributors.Any())
                        {
                            var contributorArtistIds = validContributors.Select(c => c.ArtistId).Distinct().ToList();
                            
                            // Use parameterized query to prevent SQL injection
                            var paramNames = string.Join(",", contributorArtistIds.Select((_, i) => $"@ArtistId{i}"));
                            using var validateContribCmd = new SqlCommand(
                                $"SELECT ArtistId FROM Artists WHERE ArtistId IN ({paramNames})", 
                                conn, transaction);
                            
                            for (int i = 0; i < contributorArtistIds.Count; i++)
                            {
                                validateContribCmd.Parameters.AddWithValue($"@ArtistId{i}", contributorArtistIds[i]);
                            }
                            
                            using var contribReader = await validateContribCmd.ExecuteReaderAsync();
                            var existingContribArtistIds = new HashSet<int>();
                            while (await contribReader.ReadAsync())
                            {
                                existingContribArtistIds.Add(contribReader.GetInt32(0));
                            }
                            await contribReader.CloseAsync();

                            var missingContribArtistIds = contributorArtistIds.Where(id => !existingContribArtistIds.Contains(id)).ToList();
                            if (missingContribArtistIds.Any())
                            {
                                transaction.Rollback();
                                return BadRequest(new { error = $"The following artist IDs do not exist: {string.Join(", ", missingContribArtistIds)}" });
                            }
                        }
                    }

                    // Create contributors (only valid ones with ArtistId > 0)
                    // NOTE: May need separate stored procedure or table inserts for ReleaseContributors
                    if (dto.Contributors != null)
                    {
                        var validContributors = dto.Contributors.Where(c => c.ArtistId > 0).ToList();
                        foreach (var contributor in validContributors)
                        {
                            using var contribCmd = new SqlCommand("sp_CreateReleaseContributor", conn, transaction)
                            {
                                CommandType = CommandType.StoredProcedure
                            };
                            contribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            contribCmd.Parameters.AddWithValue("@ArtistId", contributor.ArtistId);
                            contribCmd.Parameters.AddWithValue("@Role", contributor.Role);
                            await contribCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Create distribution options
                    // NOTE: May need separate stored procedure or table inserts for ReleaseDistributionStores
                    if (dto.DistributionOption.DistributionType == "Manual" && dto.DistributionOption.SelectedStoreIds.Any())
                    {
                        foreach (var storeId in dto.DistributionOption.SelectedStoreIds)
                        {
                            using var storeCmd = new SqlCommand("sp_CreateReleaseStore", conn, transaction)
                            {
                                CommandType = CommandType.StoredProcedure
                            };
                            storeCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            storeCmd.Parameters.AddWithValue("@StoreId", storeId);
                            await storeCmd.ExecuteNonQueryAsync();
                        }
                    }
                    // If "SelectAll", the stored procedure or application logic should handle all stores

                    // Handle TrackIds - validate and associate existing tracks with this release
                    var associatedTrackIds = new List<int>();
                    if (dto.TrackIds != null && dto.TrackIds.Any())
                    {
                        // Filter out invalid track IDs (0 or negative)
                        var validTrackIds = dto.TrackIds.Where(tid => tid > 0).Distinct().ToList();
                        
                        if (validTrackIds.Any())
                        {
                            // Validate that all track IDs exist and are not deleted
                            var paramNames = string.Join(",", validTrackIds.Select((_, i) => $"@TrackId{i}"));
                            using var validateTracksCmd = new SqlCommand(
                                $"SELECT TrackId, ReleaseId FROM Tracks WHERE TrackId IN ({paramNames}) AND (IsDeleted = 0 OR IsDeleted IS NULL)", 
                                conn, transaction);
                            
                            for (int i = 0; i < validTrackIds.Count; i++)
                            {
                                validateTracksCmd.Parameters.AddWithValue($"@TrackId{i}", validTrackIds[i]);
                            }
                            
                            using var tracksReader = await validateTracksCmd.ExecuteReaderAsync();
                            var existingTracks = new Dictionary<int, int?>(); // TrackId -> ReleaseId
                            while (await tracksReader.ReadAsync())
                            {
                                var trackId = tracksReader.GetInt32(0);
                                var existingReleaseId = tracksReader.IsDBNull(1) ? (int?)null : tracksReader.GetInt32(1);
                                existingTracks[trackId] = existingReleaseId;
                            }
                            await tracksReader.CloseAsync();

                            // Check for missing tracks
                            var missingTrackIds = validTrackIds.Where(id => !existingTracks.ContainsKey(id)).ToList();
                            var existingTrackIds = validTrackIds.Where(id => existingTracks.ContainsKey(id)).ToList();
                            if (missingTrackIds.Any())
                            {
                                transaction.Rollback();
                                var errorMsg = $"The following track IDs do not exist: {string.Join(", ", missingTrackIds)}";
                                if (existingTrackIds.Any())
                                {
                                    errorMsg += $". Valid track IDs found: {string.Join(", ", existingTrackIds)}";
                                }
                                return BadRequest(new { error = errorMsg });
                            }

                            // Check for TrackNumber conflicts before associating tracks
                            // Get TrackNumbers of tracks we want to associate
                            var trackNumbersToCheck = new Dictionary<int, int?>(); // TrackId -> TrackNumber
                            var paramNames2 = string.Join(",", validTrackIds.Select((_, i) => $"@TrackId2_{i}"));
                            using var getTrackNumbersCmd = new SqlCommand(
                                $"SELECT TrackId, TrackNumber FROM Tracks WHERE TrackId IN ({paramNames2}) AND (IsDeleted = 0 OR IsDeleted IS NULL)",
                                conn, transaction);
                            
                            for (int i = 0; i < validTrackIds.Count; i++)
                            {
                                getTrackNumbersCmd.Parameters.AddWithValue($"@TrackId2_{i}", validTrackIds[i]);
                            }
                            
                            using var trackNumbersReader = await getTrackNumbersCmd.ExecuteReaderAsync();
                            while (await trackNumbersReader.ReadAsync())
                            {
                                var tid = trackNumbersReader.GetInt32(0);
                                var trackNum = trackNumbersReader.IsDBNull(1) ? (int?)null : trackNumbersReader.GetInt32(1);
                                trackNumbersToCheck[tid] = trackNum;
                            }
                            await trackNumbersReader.CloseAsync();
                            
                            // Get existing TrackNumbers in the target release
                            using var getExistingTrackNumbersCmd = new SqlCommand(
                                "SELECT TrackNumber FROM Tracks WHERE ReleaseId = @ReleaseId AND (IsDeleted = 0 OR IsDeleted IS NULL) AND TrackNumber IS NOT NULL",
                                conn, transaction);
                            getExistingTrackNumbersCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            using var existingTrackNumbersReader = await getExistingTrackNumbersCmd.ExecuteReaderAsync();
                            var existingTrackNumbers = new HashSet<int>();
                            while (await existingTrackNumbersReader.ReadAsync())
                            {
                                existingTrackNumbers.Add(existingTrackNumbersReader.GetInt32(0));
                            }
                            await existingTrackNumbersReader.CloseAsync();
                            
                            // Find the next available TrackNumber
                            var getMaxTrackNumberCmd = new SqlCommand(
                                "SELECT ISNULL(MAX(TrackNumber), 0) FROM Tracks WHERE ReleaseId = @ReleaseId AND (IsDeleted = 0 OR IsDeleted IS NULL)",
                                conn, transaction);
                            getMaxTrackNumberCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            var maxTrackNumber = Convert.ToInt32(await getMaxTrackNumberCmd.ExecuteScalarAsync());
                            var nextAvailableTrackNumber = maxTrackNumber + 1;
                            
                            // First, resolve conflicts among tracks being associated
                            // Build a map of TrackNumber -> List of TrackIds that want to use it
                            var trackNumberConflicts = new Dictionary<int, List<int>>();
                            var tracksNeedingNumbers = new List<int>(); // Tracks with no TrackNumber
                            
                            foreach (var trackId in validTrackIds)
                            {
                                var trackNumber = trackNumbersToCheck.ContainsKey(trackId) ? trackNumbersToCheck[trackId] : null;
                                if (trackNumber.HasValue)
                                {
                                    if (!trackNumberConflicts.ContainsKey(trackNumber.Value))
                                    {
                                        trackNumberConflicts[trackNumber.Value] = new List<int>();
                                    }
                                    trackNumberConflicts[trackNumber.Value].Add(trackId);
                                }
                                else
                                {
                                    tracksNeedingNumbers.Add(trackId);
                                }
                            }
                            
                            // Assign final TrackNumbers to each track, resolving conflicts
                            var finalTrackNumbers = new Dictionary<int, int>(); // TrackId -> Final TrackNumber
                            var usedTrackNumbers = new HashSet<int>(existingTrackNumbers);
                            
                            // Process tracks that have TrackNumbers, resolving conflicts
                            foreach (var kvp in trackNumberConflicts)
                            {
                                var requestedTrackNumber = kvp.Key;
                                var trackIdsForThisNumber = kvp.Value;
                                
                                // First track can use the requested number if available
                                var firstTrackId = trackIdsForThisNumber[0];
                                if (!usedTrackNumbers.Contains(requestedTrackNumber))
                                {
                                    finalTrackNumbers[firstTrackId] = requestedTrackNumber;
                                    usedTrackNumbers.Add(requestedTrackNumber);
                                }
                                else
                                {
                                    // Conflict with existing track, assign next available
                                    finalTrackNumbers[firstTrackId] = nextAvailableTrackNumber;
                                    usedTrackNumbers.Add(nextAvailableTrackNumber);
                                    nextAvailableTrackNumber++;
                                }
                                
                                // Remaining tracks with same requested number get sequential numbers
                                for (int i = 1; i < trackIdsForThisNumber.Count; i++)
                                {
                                    finalTrackNumbers[trackIdsForThisNumber[i]] = nextAvailableTrackNumber;
                                    usedTrackNumbers.Add(nextAvailableTrackNumber);
                                    nextAvailableTrackNumber++;
                                }
                            }
                            
                            // Assign numbers to tracks that had no TrackNumber
                            foreach (var trackId in tracksNeedingNumbers)
                            {
                                finalTrackNumbers[trackId] = nextAvailableTrackNumber;
                                usedTrackNumbers.Add(nextAvailableTrackNumber);
                                nextAvailableTrackNumber++;
                            }
                            
                            // Now update all tracks with their final TrackNumbers
                            foreach (var trackId in validTrackIds)
                            {
                                // Check if track is already associated with this release
                                var currentReleaseId = existingTracks.ContainsKey(trackId) ? existingTracks[trackId] : null;
                                
                                // Only update if it's not already associated with this release
                                if (!currentReleaseId.HasValue || currentReleaseId.Value != releaseId)
                                {
                                    var originalTrackNumber = trackNumbersToCheck.ContainsKey(trackId) ? trackNumbersToCheck[trackId] : null;
                                    var finalTrackNumber = finalTrackNumbers[trackId];
                                    
                                    // Update track with ReleaseId and final TrackNumber
                                    using var updateTrackCmd = new SqlCommand(
                                        "UPDATE Tracks SET ReleaseId = @ReleaseId, TrackNumber = @TrackNumber WHERE TrackId = @TrackId",
                                        conn, transaction);
                                    updateTrackCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                                    updateTrackCmd.Parameters.AddWithValue("@TrackNumber", finalTrackNumber);
                                    updateTrackCmd.Parameters.AddWithValue("@TrackId", trackId);
                                    await updateTrackCmd.ExecuteNonQueryAsync();
                                }
                                associatedTrackIds.Add(trackId);
                            }
                        }
                    }

                    // Get all tracks for this release (including newly associated ones) before committing
                    var allTrackIds = new List<int>();
                    if (associatedTrackIds.Any())
                    {
                        allTrackIds.AddRange(associatedTrackIds);
                    }
                    
                    // Also get any other tracks that might already be associated with this release
                    using var getTracksCmd = new SqlCommand(
                        "SELECT TrackId FROM Tracks WHERE ReleaseId = @ReleaseId AND (IsDeleted = 0 OR IsDeleted IS NULL)",
                        conn, transaction);
                    getTracksCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                    using var allTracksReader = await getTracksCmd.ExecuteReaderAsync();
                    while (await allTracksReader.ReadAsync())
                    {
                        var trackId = allTracksReader.GetInt32(0);
                        if (!allTrackIds.Contains(trackId))
                        {
                            allTrackIds.Add(trackId);
                        }
                    }
                    await allTracksReader.CloseAsync();

                    transaction.Commit();

                    return StatusCode(201, new
                    {
                        message = "Release created successfully",
                        releaseId = releaseId,
                        trackIds = allTrackIds.Any() ? allTrackIds : null
                    });
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReleases()
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                SqlCommand cmd;

                if (role == "SuperAdmin")
                {
                    // SuperAdmin sees all releases
                    cmd = new SqlCommand(@"
                        SELECT ReleaseID, Title, LabelID, Description, ReleaseDate, Genre, CoverArtUrl, Status, CreatedAt
                        FROM Releases
                        WHERE IsDeleted = 0 OR IsDeleted IS NULL
                        ORDER BY CreatedAt DESC", conn);
                }
                else if (role == "EnterpriseAdmin")
                {
                    // EnterpriseAdmin sees releases from their enterprise
                    cmd = new SqlCommand(@"
                        SELECT DISTINCT
                            r.ReleaseID,
                            r.Title,
                            r.LabelID,
                            r.Description,
                            r.ReleaseDate,
                            r.Genre,
                            r.CoverArtUrl,
                            r.Status,
                            r.CreatedAt
                        FROM Releases r
                        INNER JOIN Labels l ON r.LabelID = l.LabelID
                        INNER JOIN EnterpriseUserRoles eur ON l.EnterpriseID = eur.EnterpriseID
                        WHERE eur.UserID = @UserId AND (r.IsDeleted = 0 OR r.IsDeleted IS NULL)
                        ORDER BY r.CreatedAt DESC", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                }
                else if (role == "LabelAdmin" || role == "LabelEditor")
                {
                    // LabelAdmin/Editor see releases from their labels
                    cmd = new SqlCommand(@"
                        SELECT DISTINCT
                            r.ReleaseID,
                            r.Title,
                            r.LabelID,
                            r.Description,
                            r.ReleaseDate,
                            r.Genre,
                            r.CoverArtUrl,
                            r.Status,
                            r.CreatedAt
                        FROM Releases r
                        INNER JOIN UserLabelRoles ulr ON r.LabelID = ulr.LabelID
                        WHERE ulr.UserID = @UserId AND (r.IsDeleted = 0 OR r.IsDeleted IS NULL)
                        ORDER BY r.CreatedAt DESC", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                }
                else
                {
                    // Regular users see releases they created or have access to
                    cmd = new SqlCommand(@"
                        SELECT ReleaseID, Title, LabelID, Description, ReleaseDate, Genre, CoverArtUrl, Status, CreatedAt
                        FROM Releases
                        WHERE CreatedBy = @UserId AND (IsDeleted = 0 OR IsDeleted IS NULL)
                        ORDER BY CreatedAt DESC", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                }

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    // Helper to safely read columns that might not exist yet
                    object? SafeRead(string columnName)
                    {
                        try
                        {
                            var ordinal = reader.GetOrdinal(columnName);
                            return reader.IsDBNull(ordinal) ? null : reader[ordinal];
                        }
                        catch
                        {
                            return null;
                        }
                    }

                    list.Add(new
                    {
                        releaseId = reader["ReleaseID"],
                        title = reader["Title"],
                        titleVersion = SafeRead("TitleVersion"),
                        labelId = reader["LabelID"],
                        description = SafeRead("Description"),
                        coverArtUrl = SafeRead("CoverArtUrl"),
                        primaryGenre = SafeRead("PrimaryGenre") ?? SafeRead("Genre"),
                        secondaryGenre = SafeRead("SecondaryGenre"),
                        digitalReleaseDate = SafeRead("DigitalReleaseDate") ?? SafeRead("ReleaseDate"),
                        originalReleaseDate = SafeRead("OriginalReleaseDate"),
                        hasUPC = SafeRead("HasUPC"),
                        upcCode = SafeRead("UPCCode"),
                        status = reader["Status"],
                        createdAt = reader["CreatedAt"]
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{releaseId}")]
        public async Task<IActionResult> GetRelease(int releaseId)
        {
            try
            {
                // NOTE: Stored procedure sp_GetReleaseById needs to be updated to return all new fields
                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_GetReleaseById", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // Helper function to safely read column values that might not exist yet
                    object? SafeRead(string columnName)
                    {
                        try
                        {
                            var ordinal = reader.GetOrdinal(columnName);
                            return reader.IsDBNull(ordinal) ? null : reader[ordinal];
                        }
                        catch
                        {
                            return null;
                        }
                    }

                    // Store release data first before reading next result sets
                    // Note: releaseId is already a method parameter, so we use it directly
                    var title = reader["Title"]?.ToString();
                    var titleVersion = SafeRead("TitleVersion");
                    var labelId = Convert.ToInt32(reader["LabelID"]);
                    var description = SafeRead("Description");
                    var coverArtUrl = SafeRead("CoverArtUrl");
                    var primaryGenre = SafeRead("PrimaryGenre") ?? SafeRead("Genre");
                    var secondaryGenre = SafeRead("SecondaryGenre");
                    var digitalReleaseDate = SafeRead("DigitalReleaseDate") ?? SafeRead("ReleaseDate");
                    var originalReleaseDate = SafeRead("OriginalReleaseDate");
                    var hasUPC = SafeRead("HasUPC");
                    var upcCode = SafeRead("UPCCode");
                    var status = reader["Status"]?.ToString();
                    var createdAt = reader["CreatedAt"];

                    // Get contributors (if available in next result set)
                    var contributors = new List<object>();
                    if (reader.NextResult())
                    {
                        try
                        {
                            while (await reader.ReadAsync())
                            {
                                try
                                {
                                    contributors.Add(new
                                    {
                                        artistId = reader["ArtistID"],
                                        role = SafeRead("Role")
                                    });
                                }
                                catch
                                {
                                    // Not a contributor row, might be tracks - break
                                    break;
                                }
                            }
                        }
                        catch { }
                    }

                    // Get tracks from stored procedure result set (if available)
                    var tracksFromSp = new List<object>();
                    if (reader.NextResult())
                    {
                        while (await reader.ReadAsync())
                        {
                            try
                            {
                                tracksFromSp.Add(new
                                {
                                    trackId = Convert.ToInt32(reader["TrackID"]),
                                    title = SafeRead("Title"),
                                    trackVersion = SafeRead("TrackVersion"),
                                    isrc = SafeRead("ISRC"),
                                    trackNumber = SafeRead("TrackNumber"),
                                    language = SafeRead("Language"),
                                    lyrics = SafeRead("Lyrics"),
                                    isExplicit = SafeRead("IsExplicit"),
                                    isInstrumental = SafeRead("IsInstrumental"),
                                    previewStartTimeSeconds = SafeRead("PreviewStartTimeSeconds"),
                                    trackGenre = SafeRead("TrackGenre"),
                                    durationSeconds = SafeRead("DurationSeconds")
                                });
                            }
                            catch { }
                        }
                    }
                    await reader.CloseAsync();
                    
                    // Always query tracks directly from database to ensure we get ALL tracks (including newly created ones)
                    // This ensures trackIds always includes the latest tracks associated with the release
                    using var tracksCmd = new SqlCommand(
                        @"SELECT 
                              t.TrackId, 
                              t.Title, 
                              t.TrackVersion, 
                              t.ISRC, 
                              t.TrackNumber, 
                              t.Language, 
                              t.Lyrics, 
                              t.IsExplicit, 
                              t.IsInstrumental, 
                              t.PreviewStartTimeSeconds, 
                              t.TrackGenre, 
                              t.DurationSeconds,
                              t.AudioFileId,
                              f.FileId       AS FileId,
                              f.CloudfrontUrl AS AudioCloudfrontUrl
                          FROM Tracks t
                          LEFT JOIN Files f ON f.FileId = t.AudioFileId
                          WHERE t.ReleaseId = @ReleaseId AND (t.IsDeleted = 0 OR t.IsDeleted IS NULL)
                          ORDER BY t.TrackNumber, t.TrackId",
                        conn);
                    tracksCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                    using var tracksReader = await tracksCmd.ExecuteReaderAsync();
                    
                    // Helper function for safe reading from tracks reader
                    object? SafeReadTracks(string columnName)
                    {
                        try
                        {
                            var ordinal = tracksReader.GetOrdinal(columnName);
                            return tracksReader.IsDBNull(ordinal) ? null : tracksReader[ordinal];
                        }
                        catch
                        {
                            return null;
                        }
                    }
                    
                    var tracks = new List<object>();
                    var trackIds = new List<int>();
                    
                    while (await tracksReader.ReadAsync())
                    {
                        try
                        {
                            var trackId = Convert.ToInt32(tracksReader["TrackId"]);
                            trackIds.Add(trackId);
                            
                            tracks.Add(new
                            {
                                trackId = trackId,
                                title = SafeReadTracks("Title"),
                                trackVersion = SafeReadTracks("TrackVersion"),
                                isrc = SafeReadTracks("ISRC"),
                                trackNumber = SafeReadTracks("TrackNumber"),
                                language = SafeReadTracks("Language"),
                                lyrics = SafeReadTracks("Lyrics"),
                                isExplicit = SafeReadTracks("IsExplicit") != null && Convert.ToBoolean(SafeReadTracks("IsExplicit")),
                                isInstrumental = SafeReadTracks("IsInstrumental") != null && Convert.ToBoolean(SafeReadTracks("IsInstrumental")),
                                previewStartTimeSeconds = SafeReadTracks("PreviewStartTimeSeconds"),
                                trackGenre = SafeReadTracks("TrackGenre"),
                                durationSeconds = SafeReadTracks("DurationSeconds"),
                                // File / audio information
                                audioFileId = SafeReadTracks("AudioFileId"),
                                fileId = SafeReadTracks("FileId"),
                                audioUrl = SafeReadTracks("AudioCloudfrontUrl")
                            });
                        }
                        catch { }
                    }

                    // Build release object with trackIds
                    var releaseWithTracks = new
                    {
                        releaseId = releaseId,
                        title = title,
                        titleVersion = titleVersion,
                        labelId = labelId,
                        description = description,
                        coverArtUrl = coverArtUrl,
                        primaryGenre = primaryGenre,
                        secondaryGenre = secondaryGenre,
                        digitalReleaseDate = digitalReleaseDate,
                        originalReleaseDate = originalReleaseDate,
                        hasUPC = hasUPC,
                        upcCode = upcCode,
                        status = status,
                        createdAt = createdAt,
                        trackIds = trackIds.Any() ? trackIds : null
                    };

                    return Ok(new 
                    { 
                        release = releaseWithTracks,
                        contributors = contributors.Any() ? contributors : null,
                        tracks = tracks.Any() ? tracks : null
                    });
                }

                return NotFound(new { error = "Release not found" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Internal update implementation used by POST /api/releases/{releaseId}
        private async Task<IActionResult> UpdateReleaseInternal(int releaseId, [FromBody] UpdateReleaseDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();
                try
                {
                    // NOTE: Stored procedure sp_UpdateRelease needs to be updated to accept all new parameters
                    using var cmd = new SqlCommand("sp_UpdateRelease", conn, transaction)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                    cmd.Parameters.AddWithValue("@Title", string.IsNullOrWhiteSpace(dto.Title) ? DBNull.Value : dto.Title);
                    cmd.Parameters.AddWithValue("@TitleVersion", string.IsNullOrWhiteSpace(dto.TitleVersion) ? DBNull.Value : dto.TitleVersion);
                    cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(dto.Description) ? DBNull.Value : dto.Description);
                    cmd.Parameters.AddWithValue("@CoverArtUrl", string.IsNullOrWhiteSpace(dto.CoverArtUrl) ? DBNull.Value : dto.CoverArtUrl);
                    cmd.Parameters.AddWithValue("@PrimaryGenre", string.IsNullOrWhiteSpace(dto.PrimaryGenre) ? DBNull.Value : dto.PrimaryGenre);
                    cmd.Parameters.AddWithValue("@SecondaryGenre", string.IsNullOrWhiteSpace(dto.SecondaryGenre) ? DBNull.Value : dto.SecondaryGenre);
                    cmd.Parameters.AddWithValue("@DigitalReleaseDate", (object?)dto.DigitalReleaseDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriginalReleaseDate", (object?)dto.OriginalReleaseDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@HasUPC", dto.HasUPC.HasValue ? (object)dto.HasUPC.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@UPCCode", string.IsNullOrWhiteSpace(dto.UPCCode) ? DBNull.Value : dto.UPCCode);

                    var rows = await cmd.ExecuteNonQueryAsync();

                    if (rows == 0)
                    {
                        transaction.Rollback();
                        return NotFound(new { error = "Release not found" });
                    }

                    // Update contributors if provided
                    if (dto.Contributors != null && dto.Contributors.Any())
                    {
                        // Filter out invalid artist IDs (0 or negative) before validation
                        var validContributors = dto.Contributors.Where(c => c.ArtistId > 0).ToList();
                        
                        if (validContributors.Any())
                        {
                            // Validate contributors - check if all artistIds exist
                            var contributorArtistIds = validContributors.Select(c => c.ArtistId).Distinct().ToList();
                            
                            // Use parameterized query to prevent SQL injection
                            var paramNames = string.Join(",", contributorArtistIds.Select((_, i) => $"@ArtistId{i}"));
                            using var validateContribCmd = new SqlCommand(
                                $"SELECT ArtistId FROM Artists WHERE ArtistId IN ({paramNames})", 
                                conn, transaction);
                            
                            for (int i = 0; i < contributorArtistIds.Count; i++)
                            {
                                validateContribCmd.Parameters.AddWithValue($"@ArtistId{i}", contributorArtistIds[i]);
                            }
                            
                            using var contribReader = await validateContribCmd.ExecuteReaderAsync();
                            var existingContribArtistIds = new HashSet<int>();
                            while (await contribReader.ReadAsync())
                            {
                                existingContribArtistIds.Add(contribReader.GetInt32(0));
                            }
                            await contribReader.CloseAsync();

                            var missingContribArtistIds = contributorArtistIds.Where(id => !existingContribArtistIds.Contains(id)).ToList();
                            if (missingContribArtistIds.Any())
                            {
                                transaction.Rollback();
                                return BadRequest(new { error = $"The following artist IDs do not exist: {string.Join(", ", missingContribArtistIds)}" });
                            }
                        }

                        // Delete existing contributors and add new ones (only valid ones)
                        using var delContribCmd = new SqlCommand("sp_DeleteReleaseContributors", conn, transaction)
                        {
                            CommandType = CommandType.StoredProcedure
                        };
                        delContribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                        await delContribCmd.ExecuteNonQueryAsync();

                        var contributorsToAdd = dto.Contributors.Where(c => c.ArtistId > 0).ToList();
                        foreach (var contributor in contributorsToAdd)
                        {
                            using var contribCmd = new SqlCommand("sp_CreateReleaseContributor", conn, transaction)
                            {
                                CommandType = CommandType.StoredProcedure
                            };
                            contribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            contribCmd.Parameters.AddWithValue("@ArtistId", contributor.ArtistId);
                            contribCmd.Parameters.AddWithValue("@Role", contributor.Role);
                            await contribCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Update distribution options if provided
                    if (dto.DistributionOption != null)
                    {
                        using var delStoreCmd = new SqlCommand("sp_DeleteReleaseStores", conn, transaction)
                        {
                            CommandType = CommandType.StoredProcedure
                        };
                        delStoreCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                        await delStoreCmd.ExecuteNonQueryAsync();

                        if (dto.DistributionOption.DistributionType == "Manual" && dto.DistributionOption.SelectedStoreIds.Any())
                        {
                            foreach (var storeId in dto.DistributionOption.SelectedStoreIds)
                            {
                                using var storeCmd = new SqlCommand("sp_CreateReleaseStore", conn, transaction)
                                {
                                    CommandType = CommandType.StoredProcedure
                                };
                                storeCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                                storeCmd.Parameters.AddWithValue("@StoreId", storeId);
                                await storeCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    transaction.Commit();
                    return Ok(new { message = "Release updated successfully" });
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Update release via POST /api/releases/{releaseId}
        [HttpPost("{releaseId}")]
        public Task<IActionResult> UpdateRelease(int releaseId, [FromBody] UpdateReleaseDto dto)
        {
            return UpdateReleaseInternal(releaseId, dto);
        }

        [HttpDelete("{releaseId}")]
        public async Task<IActionResult> DeleteRelease(int releaseId)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_DeleteRelease", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

                await conn.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound(new { error = "Release not found" });

                return Ok(new { message = "Release deleted successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{releaseId}/submit")]
        public async Task<IActionResult> SubmitRelease(int releaseId)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_SubmitRelease", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

                await conn.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound(new { error = "Release not found or already submitted" });

                return Ok(new { message = "Release submitted for QC successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{releaseId}/takedown")]
        public async Task<IActionResult> TakedownRelease(int releaseId, [FromBody] TakedownRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_TakedownRelease", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                cmd.Parameters.AddWithValue("@Reason", dto.Reason);

                await conn.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound(new { error = "Release not found or takedown request failed" });

                return Ok(new { message = "Takedown request submitted successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}

