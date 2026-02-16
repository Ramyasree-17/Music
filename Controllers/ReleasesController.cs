
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/releases")]
    [Tags("Section 6 - Releases")]
    [Authorize]
    public class ReleasesController : ControllerBase
    {
        private static readonly string[] AllowedContributorRoles =
        {
            "Primary Artist",
            "Featured Artist",
            "Producer",
            "Director",
            "Composer",
            "Lyricist"
        };

        private readonly IConfiguration _cfg;
        private readonly string _connStr;
        private readonly IWebHostEnvironment _env;

        public ReleasesController(IConfiguration cfg, IWebHostEnvironment env)
        {
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
            _env = env;
        }

        private Dictionary<string, List<int>> ConvertContributorsDtoToDictionary(ContributorsDto? contributors)
        {
            var result = new Dictionary<string, List<int>>();

            if (contributors == null)
            {
                // Return empty lists for all roles
                result["Primary Artist"] = new List<int>();
                result["Featured Artist"] = new List<int>();
                result["Producer"] = new List<int>();
                result["Director"] = new List<int>();
                result["Composer"] = new List<int>();
                result["Lyricist"] = new List<int>();
                return result;
            }

            result["Primary Artist"] = contributors.PrimaryArtist ?? new List<int>();
            result["Featured Artist"] = contributors.FeaturedArtist ?? new List<int>();
            result["Producer"] = contributors.Producer ?? new List<int>();
            result["Director"] = contributors.Director ?? new List<int>();
            result["Composer"] = contributors.Composer ?? new List<int>();
            result["Lyricist"] = contributors.Lyricist ?? new List<int>();

            return result;
        }

        private Dictionary<string, List<int>> ConvertContributorListToDictionary(List<ContributorDto>? contributors)
        {
            var result = new Dictionary<string, List<int>>
            {
                ["Primary Artist"] = new List<int>(),
                ["Featured Artist"] = new List<int>(),
                ["Producer"] = new List<int>(),
                ["Director"] = new List<int>(),
                ["Composer"] = new List<int>(),
                ["Lyricist"] = new List<int>()
            };

            if (contributors == null || !contributors.Any())
                return result;

            foreach (var contributor in contributors.Where(c => c.ArtistId > 0))
            {
                var role = contributor.Role?.Trim();
                if (!string.IsNullOrEmpty(role) && result.ContainsKey(role))
                {
                    result[role].Add(contributor.ArtistId);
                }
            }

            return result;
        }

        private Dictionary<string, List<int>> ParseContributorsFromFormData()
        {
            var result = new Dictionary<string, List<int>>();
            var formKeys = Request.Form.Keys.ToList();

            // Debug: Log all form keys to see what we're receiving
            // This helps identify if field names are being sent correctly

            // Map all possible field name variations to role names
            var fieldMappings = new Dictionary<string, string>
            {
                // Contributors.* format
                { "Contributors.PrimaryArtist", "Primary Artist" },
                { "Contributors.FeaturedArtist", "Featured Artist" },
                { "Contributors.Producer", "Producer" },
                { "Contributors.Director", "Director" },
                { "Contributors.Composer", "Composer" },
                { "Contributors.Lyricist", "Lyricist" },
                // Direct property names (camelCase)
                { "primaryArtist", "Primary Artist" },
                { "featuredArtist", "Featured Artist" },
                { "producer", "Producer" },
                { "director", "Director" },
                { "composer", "Composer" },
                { "lyricist", "Lyricist" },
                // PascalCase
                { "PrimaryArtist", "Primary Artist" },
                { "FeaturedArtist", "Featured Artist" },
                { "Producer", "Producer" },
                { "Director", "Director" },
                { "Composer", "Composer" },
                { "Lyricist", "Lyricist" }
            };

            // Helper method to parse a value and extract artist IDs
            List<int> ParseArtistIds(string value)
            {
                var artistIds = new List<int>();
                if (string.IsNullOrWhiteSpace(value))
                    return artistIds;

                var trimmed = value.Trim();

                // Handle Swagger's format: "Contributors.PrimaryArtist: [1]" or "PrimaryArtist: [1]"
                // Extract the array part if it exists
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0)
                {
                    trimmed = trimmed.Substring(colonIndex + 1).Trim();
                }

                // Handle [1] or [1,2] format
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    trimmed = trimmed.Trim('[', ']');
                }

                // Parse comma-separated values or single value
                foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), out var artistId) && artistId > 0)
                    {
                        artistIds.Add(artistId);
                    }
                }

                return artistIds;
            }

            // Parse Contributors.* fields and direct property names
            foreach (var mapping in fieldMappings)
            {
                // Skip if we already have this role
                if (result.ContainsKey(mapping.Value))
                    continue;

                var fieldKey = formKeys.FirstOrDefault(k => k.Equals(mapping.Key, StringComparison.OrdinalIgnoreCase));
                if (fieldKey != null)
                {
                    var formValues = Request.Form[fieldKey];
                    var allArtistIds = new List<int>();

                    foreach (var value in formValues)
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            var parsedIds = ParseArtistIds(value);
                            allArtistIds.AddRange(parsedIds);
                        }
                    }

                    if (allArtistIds.Any())
                    {
                        result[mapping.Value] = allArtistIds;
                    }
                }
            }

            // Also check for direct role-named fields (e.g., "Primary Artist"=[1])
            foreach (var role in AllowedContributorRoles)
            {
                if (!result.ContainsKey(role))
                {
                    var matchingKey = formKeys.FirstOrDefault(k => k.Equals(role, StringComparison.OrdinalIgnoreCase));
                    if (matchingKey != null)
                    {
                        var formValues = Request.Form[matchingKey];
                        var allArtistIds = new List<int>();

                        foreach (var value in formValues)
                        {
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                var parsedIds = ParseArtistIds(value);
                                allArtistIds.AddRange(parsedIds);
                            }
                        }

                        if (allArtistIds.Any())
                        {
                            result[role] = allArtistIds;
                        }
                    }
                }
            }

            return result;
        }

        private Dictionary<string, List<int>> ParseContributorsDictionary(string json)
        {
            var allowedRoles = new HashSet<string>
            {
                "Primary Artist",
                "Featured Artist",
                "Producer",
                "Director",
                "Composer",
                "Lyricist"
            };

            var result = new Dictionary<string, List<int>>();

            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new Exception("Invalid ContributorsJson format. Expected object with role keys.");

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var role = property.Name.Trim();

                if (!allowedRoles.Contains(role))
                    throw new Exception($"Invalid role: {role}. Allowed roles are: {string.Join(", ", allowedRoles)}");

                var artistIds = new List<int>();

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var artistElement in property.Value.EnumerateArray())
                    {
                        if (artistElement.TryGetInt32(out int artistId) && artistId > 0)
                        {
                            artistIds.Add(artistId);
                        }
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Null)
                {
                    // null values are allowed, will result in empty list
                    artistIds = new List<int>();
                }

                result[role] = artistIds;
            }

            return result;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateReleaseDto dto)
        {
            try
            {
                // Remove ModelState errors for optional fields (Title and PrimaryGenre are now optional)
                ModelState.Remove("Title");
                ModelState.Remove("PrimaryGenre");

                // Remove Contributors binding errors - we'll parse from form data manually
                var contributorsKeys = ModelState.Keys.Where(k => k.StartsWith("Contributors.", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var key in contributorsKeys)
                {
                    ModelState.Remove(key);
                }

                // Remove DistributionOption binding errors - handle empty strings gracefully
                var distributionKeys = ModelState.Keys.Where(k => k.StartsWith("DistributionOption.", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var key in distributionKeys)
                {
                    ModelState.Remove(key);
                }

                // Handle empty SelectedStoreIds - if it's null, set to empty list
                if (dto.DistributionOption != null && dto.DistributionOption.SelectedStoreIds == null)
                {
                    dto.DistributionOption.SelectedStoreIds = new List<int>();
                }

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Always try to parse from form data first (form data takes precedence)
                // This handles cases where Swagger sends fields like primaryArtist=[1] directly
                var contributorsDict = ParseContributorsFromFormData();

                // If form data parsing didn't find anything, try DTO
                if (contributorsDict == null || !contributorsDict.Values.Any(v => v != null && v.Any(id => id > 0)))
                {
                    if (dto.Contributors != null && dto.Contributors.Any())
                    {
                        var dtoDict = ConvertContributorListToDictionary(dto.Contributors);
                        // Merge DTO values into form data results (form data takes precedence)
                        if (contributorsDict == null)
                            contributorsDict = new Dictionary<string, List<int>>();
                        
                        foreach (var kvp in dtoDict)
                        {
                            // Only add if form data doesn't already have this role
                            if (!contributorsDict.ContainsKey(kvp.Key) || !contributorsDict[kvp.Key].Any(id => id > 0))
                            {
                                if (kvp.Value != null && kvp.Value.Any(id => id > 0))
                                {
                                    contributorsDict[kvp.Key] = kvp.Value.Where(id => id > 0).ToList();
                                }
                            }
                        }
                    }
                }

                // Manual validation for mandatory fields (Title and PrimaryGenre are now optional - no validation)
                if (dto.LabelId <= 0)
                    return BadRequest(new { error = "LabelId is required and must be greater than 0" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Handle cover art file upload
                string? coverArtUrl = null;

                if (dto.CoverArtFile != null && dto.CoverArtFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var ext = Path.GetExtension(dto.CoverArtFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                        return BadRequest(new { error = "Invalid image format" });

                    const long maxSize = 10 * 1024 * 1024;

                    if (dto.CoverArtFile.Length > maxSize)
                        return BadRequest(new { error = "Image too large" });

                    string root = _env.WebRootPath!;
                    string folder = Path.Combine(root, "images");

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string fileName = $"{Guid.NewGuid():N}{ext}";
                    string path = Path.Combine(folder, fileName);

                    using var stream = new FileStream(path, FileMode.Create);
                    await dto.CoverArtFile.CopyToAsync(stream);

                    coverArtUrl = "/images/" + fileName;
                }

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
                    // Title is validated above, but handle nullable type
                    cmd.Parameters.AddWithValue("@Title", string.IsNullOrWhiteSpace(dto.Title) ? (object)DBNull.Value : dto.Title);
                    cmd.Parameters.AddWithValue("@LabelId", dto.LabelId);
                    cmd.Parameters.AddWithValue("@CreatedBy", userId);

                    // Optional parameters (mapping new and legacy fields)
                    cmd.Parameters.AddWithValue("@Description",
                        string.IsNullOrWhiteSpace(dto.Description) ? DBNull.Value : dto.Description);
                    cmd.Parameters.AddWithValue("@CoverArtUrl",
                        string.IsNullOrEmpty(coverArtUrl) ? DBNull.Value : coverArtUrl);

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

                    // Create contributors from Dictionary format
                    var contributorsInserted = 0;
                    if (contributorsDict != null && contributorsDict.Any())
                    {
                        // Check if ReleaseContributors table exists
                        var tableExistsCmd = new SqlCommand("SELECT OBJECT_ID('dbo.ReleaseContributors', 'U')", conn, transaction);
                        var tableExists = await tableExistsCmd.ExecuteScalarAsync();

                        if (tableExists == DBNull.Value || tableExists == null)
                        {
                            transaction.Rollback();
                            return BadRequest(new { error = "ReleaseContributors table does not exist in the database. Please run the database migration script." });
                        }

                        // Collect all artist IDs for validation
                        var allArtistIds = new List<int>();
                        foreach (var roleGroup in contributorsDict)
                        {
                            var role = roleGroup.Key;
                            var artistIds = roleGroup.Value;

                            // Validate role
                            if (!AllowedContributorRoles.Contains(role))
                            {
                                transaction.Rollback();
                                return BadRequest(new { error = $"Invalid role: {role}. Allowed roles are: {string.Join(", ", AllowedContributorRoles)}" });
                            }

                            if (artistIds != null && artistIds.Any())
                            {
                                // Filter valid artist IDs (> 0)
                                var validArtistIds = artistIds.Where(a => a > 0).ToList();
                                allArtistIds.AddRange(validArtistIds);
                            }
                        }

                        // Validate all artist IDs exist
                        if (allArtistIds.Any())
                        {
                            var uniqueArtistIds = allArtistIds.Distinct().ToList();
                            var paramNames = string.Join(",", uniqueArtistIds.Select((_, i) => $"@ArtistId{i}"));
                            using var validateContribCmd = new SqlCommand(
                                $"SELECT ArtistId FROM Artists WHERE ArtistId IN ({paramNames})",
                                conn, transaction);

                            for (int i = 0; i < uniqueArtistIds.Count; i++)
                            {
                                validateContribCmd.Parameters.AddWithValue($"@ArtistId{i}", uniqueArtistIds[i]);
                            }

                            using var contribReader = await validateContribCmd.ExecuteReaderAsync();
                            var existingContribArtistIds = new HashSet<int>();
                            while (await contribReader.ReadAsync())
                            {
                                existingContribArtistIds.Add(contribReader.GetInt32(0));
                            }
                            await contribReader.CloseAsync();

                            var missingContribArtistIds = uniqueArtistIds.Where(id => !existingContribArtistIds.Contains(id)).ToList();
                            if (missingContribArtistIds.Any())
                            {
                                transaction.Rollback();
                                return BadRequest(new { error = $"The following artist IDs do not exist: {string.Join(", ", missingContribArtistIds)}" });
                            }

                            // Insert contributors
                            foreach (var roleGroup in contributorsDict)
                            {
                                var role = roleGroup.Key;
                                var artistIds = roleGroup.Value;

                                if (artistIds == null || !artistIds.Any())
                                    continue;

                                foreach (var artistId in artistIds.Where(a => a > 0))
                                {
                                    try
                                    {
                                        using var contribCmd = new SqlCommand(
                                            "INSERT INTO ReleaseContributors (ReleaseID, ArtistID, Role) VALUES (@ReleaseId, @ArtistId, @Role)",
                                            conn, transaction);
                                        contribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                                        contribCmd.Parameters.AddWithValue("@ArtistId", artistId);
                                        contribCmd.Parameters.AddWithValue("@Role", role);
                                        var rowsAffected = await contribCmd.ExecuteNonQueryAsync();

                                        if (rowsAffected > 0)
                                        {
                                            contributorsInserted++;
                                        }
                                        else
                                        {
                                            throw new Exception($"Failed to insert contributor with ArtistId {artistId}");
                                        }
                                    }
                                    catch (SqlException sqlEx)
                                    {
                                        transaction.Rollback();
                                        return BadRequest(new { error = $"Database error while inserting contributor: {sqlEx.Message}" });
                                    }
                                    catch (Exception ex)
                                    {
                                        transaction.Rollback();
                                        return BadRequest(new { error = $"Error inserting contributor: {ex.Message}" });
                                    }
                                }
                            }
                        }
                    }

                    // Create distribution options
                    if (dto.DistributionOption?.DistributionType == "Manual" && dto.DistributionOption.SelectedStoreIds?.Any() == true)
                    {
                        foreach (var storeId in dto.DistributionOption.SelectedStoreIds)
                        {
                            using var storeCmd = new SqlCommand(
                                "INSERT INTO ReleaseStores (ReleaseId, StoreId) VALUES (@ReleaseId, @StoreId)",
                                conn, transaction);
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

                    var totalContributorsReceived = 0;
                    if (contributorsDict != null)
                    {
                        totalContributorsReceived = contributorsDict.Values
                            .Where(v => v != null)
                            .SelectMany(v => v)
                            .Count(a => a > 0);
                    }

                    // Filter out 0 values and empty lists from response
                    var filteredContributors = contributorsDict != null
                        ? contributorsDict
                            .Where(kvp => kvp.Value != null && kvp.Value.Any(id => id > 0))
                            .ToDictionary(
                                k => k.Key,
                                v => v.Value.Where(id => id > 0).ToList()
                            )
                        : null;

                    // Debug: Get all form keys that contain contributor-related fields
                    var allFormKeys = Request.Form.Keys.Where(k =>
                        k.Contains("contributor", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("artist", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("primaryArtist", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("featuredArtist", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("producer", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("director", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("composer", StringComparison.OrdinalIgnoreCase) ||
                        k.Equals("lyricist", StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    var response = new
                    {
                        message = "Release created successfully",
                        releaseId = releaseId,
                        trackIds = allTrackIds.Any() ? allTrackIds : null,
                        contributorsInserted = contributorsInserted,
                        contributorsReceived = totalContributorsReceived,
                        contributorsParsed = filteredContributors,
                        debugFormKeys = allFormKeys,
                        debugFormValues = allFormKeys.ToDictionary(
                            k => k,
                            k => Request.Form[k].ToString()
                        )
                    };

                    return StatusCode(201, response);
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
                        SELECT ReleaseID, Title, LabelID, Description, ReleaseDate, Genre, CoverArtUrl, Status, CreatedAt, UpdatedAt
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
                            r.CreatedAt,
                            r.UpdatedAt
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
                            r.CreatedAt,
                            r.UpdatedAt
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
                        SELECT ReleaseID, Title, LabelID, Description, ReleaseDate, Genre, CoverArtUrl, Status, CreatedAt, UpdatedAt
                        FROM Releases
                        WHERE CreatedBy = @UserId AND (IsDeleted = 0 OR IsDeleted IS NULL)
                        ORDER BY CreatedAt DESC", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                }

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                // Helper to safely read columns that might not exist yet
                object? SafeRead(SqlDataReader rdr, string columnName)
                {
                    try
                    {
                        var ordinal = rdr.GetOrdinal(columnName);
                        return rdr.IsDBNull(ordinal) ? null : rdr[ordinal];
                    }
                    catch
                    {
                        return null;
                    }
                }

                // First, read all releases into memory
                var releasesData = new List<Dictionary<string, object?>>();
                while (await reader.ReadAsync())
                {
                    releasesData.Add(new Dictionary<string, object?>
                    {
                        ["ReleaseID"] = reader["ReleaseID"],
                        ["Title"] = reader["Title"],
                        ["TitleVersion"] = SafeRead(reader, "TitleVersion"),
                        ["LabelID"] = reader["LabelID"],
                        ["Description"] = SafeRead(reader, "Description"),
                        ["CoverArtUrl"] = SafeRead(reader, "CoverArtUrl"),
                        ["PrimaryGenre"] = SafeRead(reader, "PrimaryGenre") ?? SafeRead(reader, "Genre"),
                        ["SecondaryGenre"] = SafeRead(reader, "SecondaryGenre"),
                        ["DigitalReleaseDate"] = SafeRead(reader, "DigitalReleaseDate") ?? SafeRead(reader, "ReleaseDate"),
                        ["OriginalReleaseDate"] = SafeRead(reader, "OriginalReleaseDate"),
                        ["HasUPC"] = SafeRead(reader, "HasUPC"),
                        ["UPCCode"] = SafeRead(reader, "UPCCode"),
                        ["Status"] = reader["Status"],
                        ["CreatedAt"] = reader["CreatedAt"],
                        ["UpdatedAt"] = SafeRead(reader, "UpdatedAt")
                    });
                }
                await reader.CloseAsync();

                // Now build the response with trackIds and contributors
                var list = new List<object>();
                foreach (var release in releasesData)
                {
                    var releaseId = Convert.ToInt32(release["ReleaseID"]);

                    // Get trackIds for this release
                    var trackIds = new List<int>();
                    using (var tcmd = new SqlCommand(
                        "SELECT TrackId FROM Tracks WHERE ReleaseId = @ReleaseId AND (IsDeleted = 0 OR IsDeleted IS NULL)",
                        conn))
                    {
                        tcmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                        using var tr = await tcmd.ExecuteReaderAsync();
                        while (await tr.ReadAsync())
                            trackIds.Add(tr.GetInt32(0));
                        await tr.CloseAsync();
                    }

                    // Get contributors for this release in grouped format
                    var contributorsDict = new Dictionary<string, List<int>>
                    {
                        { "Primary Artist", new List<int>() },
                        { "Featured Artist", new List<int>() },
                        { "Producer", new List<int>() },
                        { "Director", new List<int>() },
                        { "Composer", new List<int>() },
                        { "Lyricist", new List<int>() }
                    };

                    try
                    {
                        // Check if ReleaseContributors table exists
                        var contributorTableExistsCmd = new SqlCommand("SELECT OBJECT_ID('dbo.ReleaseContributors', 'U')", conn);
                        var contributorTableObjId = await contributorTableExistsCmd.ExecuteScalarAsync();
                        if (contributorTableObjId != DBNull.Value && contributorTableObjId != null)
                        {
                            using (var ccmd = new SqlCommand(
                                "SELECT ArtistID, Role FROM ReleaseContributors WHERE ReleaseID = @ReleaseId",
                                conn))
                            {
                                ccmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                                using var cr = await ccmd.ExecuteReaderAsync();
                                while (await cr.ReadAsync())
                                {
                                    var artistId = cr.GetInt32(0);
                                    var contributorRole = cr.IsDBNull(1) ? null : cr.GetString(1);

                                    if (!string.IsNullOrEmpty(contributorRole) && contributorsDict.ContainsKey(contributorRole))
                                    {
                                        contributorsDict[contributorRole].Add(artistId);
                                    }
                                }
                                await cr.CloseAsync();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore if ReleaseContributors table doesn't exist or other errors
                    }

                    // Return only specified fields for GET /api/releases
                    list.Add(new
                    {
                        releaseId = releaseId,
                        title = release["Title"],
                        labelId = release["LabelID"],
                        coverArtUrl = release["CoverArtUrl"],
                        primaryGenre = release["PrimaryGenre"],
                        digitalReleaseDate = release["DigitalReleaseDate"],
                        originalReleaseDate = release["OriginalReleaseDate"],
                        upcCode = release["UPCCode"],
                        status = release["Status"],
                        createdAt = release["CreatedAt"],
                        updatedAt = release["UpdatedAt"]
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
                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                // NOTE: We intentionally avoid stored procedure sp_GetReleaseById here because DB instances
                // can have older versions that still reference removed Artists columns (StageName/DisplayName),
                // causing runtime "Invalid column name" errors.
                var releaseColumns = await GetTableColumnsAsync(conn, "Releases");
                var selectCols = new List<string>
                {
                    "ReleaseID",
                    "Title",
                    "LabelID",
                    "Status",
                    "CreatedAt",
                    // legacy columns
                    "Description",
                    "CoverArtUrl",
                    "Genre",
                    "ReleaseDate",
                    // new columns (may not exist in older DBs)
                    "TitleVersion",
                    "PrimaryGenre",
                    "SecondaryGenre",
                    "DigitalReleaseDate",
                    "OriginalReleaseDate",
                    "HasUPC",
                    "UPCCode",
                    "UpdatedAt"
                };

                var projected = selectCols
                    .Where(c => releaseColumns.Contains(c))
                    .Select(c => $"r.[{c}]");

                // Always require these core columns
                if (!releaseColumns.Contains("ReleaseID") || !releaseColumns.Contains("Title") || !releaseColumns.Contains("LabelID"))
                    return StatusCode(500, new { error = "Database schema error: Releases table missing core columns." });

                var sql = $@"
                    SELECT {string.Join(", ", projected)}
                    FROM Releases r
                    WHERE r.ReleaseID = @ReleaseId
                    {(releaseColumns.Contains("IsDeleted") ? "AND (r.IsDeleted = 0 OR r.IsDeleted IS NULL)" : "")};";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    // Helper function to safely read column values that might not exist in SELECT projection
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

                    var title = SafeRead("Title")?.ToString();
                    var titleVersion = SafeRead("TitleVersion");
                    var labelId = Convert.ToInt32(SafeRead("LabelID") ?? 0);
                    var description = SafeRead("Description");
                    var coverArtUrl = SafeRead("CoverArtUrl");
                    var primaryGenre = SafeRead("PrimaryGenre") ?? SafeRead("Genre");
                    var secondaryGenre = SafeRead("SecondaryGenre");
                    var digitalReleaseDate = SafeRead("DigitalReleaseDate") ?? SafeRead("ReleaseDate");
                    var originalReleaseDate = SafeRead("OriginalReleaseDate");
                    var hasUPC = SafeRead("HasUPC");
                    var upcCode = SafeRead("UPCCode");
                    var status = SafeRead("Status")?.ToString();
                    var createdAt = SafeRead("CreatedAt");
                    var updatedAt = SafeRead("UpdatedAt");

                    await reader.CloseAsync();

                    // Get contributors in grouped format
                    var contributorsDict = new Dictionary<string, List<int>>
                    {
                        { "Primary Artist", new List<int>() },
                        { "Featured Artist", new List<int>() },
                        { "Producer", new List<int>() },
                        { "Director", new List<int>() },
                        { "Composer", new List<int>() },
                        { "Lyricist", new List<int>() }
                    };

                    try
                    {
                        var contributorTableExistsCmd = new SqlCommand("SELECT OBJECT_ID('dbo.ReleaseContributors', 'U')", conn);
                        var contributorTableObjId = await contributorTableExistsCmd.ExecuteScalarAsync();
                        if (contributorTableObjId != DBNull.Value && contributorTableObjId != null)
                        {
                            using var contribCmd = new SqlCommand(@"
                                SELECT ArtistID, Role
                                FROM ReleaseContributors
                                WHERE ReleaseID = @ReleaseId;", conn);
                            contribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            using var contribReader = await contribCmd.ExecuteReaderAsync();
                            while (await contribReader.ReadAsync())
                            {
                                var artistId = contribReader.GetInt32(0);
                                var role = contribReader.IsDBNull(1) ? null : contribReader.GetString(1);

                                if (!string.IsNullOrEmpty(role) && contributorsDict.ContainsKey(role))
                                {
                                    contributorsDict[role].Add(artistId);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore contributors if DB schema differs
                    }

                    // Convert to ContributorsDto format with empty arrays (not null)
                    var contributorsResponse = new ContributorsDto
                    {
                        PrimaryArtist = contributorsDict.ContainsKey("Primary Artist") ? contributorsDict["Primary Artist"] : new List<int>(),
                        FeaturedArtist = contributorsDict.ContainsKey("Featured Artist") ? contributorsDict["Featured Artist"] : new List<int>(),
                        Producer = contributorsDict.ContainsKey("Producer") ? contributorsDict["Producer"] : new List<int>(),
                        Director = contributorsDict.ContainsKey("Director") ? contributorsDict["Director"] : new List<int>(),
                        Composer = contributorsDict.ContainsKey("Composer") ? contributorsDict["Composer"] : new List<int>(),
                        Lyricist = contributorsDict.ContainsKey("Lyricist") ? contributorsDict["Lyricist"] : new List<int>()
                    };

                    // Get trackIds only (no detailed track info)
                    var trackIds = new List<int>();
                    using var trackIdCmd = new SqlCommand(
                        "SELECT TrackId FROM Tracks WHERE ReleaseId = @ReleaseId AND (IsDeleted = 0 OR IsDeleted IS NULL)",
                        conn);
                    trackIdCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                    using var trackIdReader = await trackIdCmd.ExecuteReaderAsync();
                    while (await trackIdReader.ReadAsync())
                    {
                        trackIds.Add(trackIdReader.GetInt32(0));
                    }
                    await trackIdReader.CloseAsync();

                    // Get distribution options
                    var selectedStoreIds = new List<int>();
                    string? distributionType = null;

                    try
                    {
                        // Check if ReleaseStores table exists
                        var releaseStoresTableExistsCmd = new SqlCommand("SELECT OBJECT_ID('dbo.ReleaseStores', 'U')", conn);
                        var releaseStoresTableObjId = await releaseStoresTableExistsCmd.ExecuteScalarAsync();

                        if (releaseStoresTableObjId != DBNull.Value && releaseStoresTableObjId != null)
                        {
                            using var storeCmd = new SqlCommand(
                                "SELECT StoreId FROM ReleaseStores WHERE ReleaseId = @ReleaseId",
                                conn);
                            storeCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            using var storeReader = await storeCmd.ExecuteReaderAsync();
                            while (await storeReader.ReadAsync())
                            {
                                selectedStoreIds.Add(storeReader.GetInt32(0));
                            }
                            await storeReader.CloseAsync();

                            // Determine distribution type
                            if (selectedStoreIds.Any())
                            {
                                distributionType = "Manual";
                            }
                            else
                            {
                                // If no stores selected, it might be "SelectAll" or not set
                                distributionType = null; // Optional - can be null
                            }
                        }
                    }
                    catch
                    {
                        // Ignore if ReleaseStores table doesn't exist
                    }

                    // Return all fields for GET /api/releases/{releaseId}
                    return Ok(new
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
                        updatedAt = updatedAt,
                        trackIds = trackIds.Any() ? trackIds : null,
                        contributors = contributorsResponse,
                        distributionOption = distributionType != null ? new
                        {
                            distributionType = distributionType,
                            selectedStoreIds = selectedStoreIds.Any() ? selectedStoreIds : null
                        } : null
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

        private static async Task<HashSet<string>> GetTableColumnsAsync(SqlConnection conn, string tableName)
        {
            using var cmd = new SqlCommand(@"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName;", conn);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            using var reader = await cmd.ExecuteReaderAsync();
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                cols.Add(reader.GetString(0));
            }
            return cols;
        }

        private static object? SafeReadFromReader(SqlDataReader reader, string columnName)
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

        // Internal update implementation used by POST /api/releases/{releaseId}
        private async Task<IActionResult> UpdateReleaseInternal(int releaseId, [FromForm] UpdateReleaseDto dto)
        {
            try
            {
                // Remove Contributors binding errors - we'll parse from form data manually
                var contributorsKeys = ModelState.Keys.Where(k => k.StartsWith("Contributors.", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var key in contributorsKeys)
                {
                    ModelState.Remove(key);
                }

                // Remove DistributionOption binding errors - handle empty strings gracefully
                var distributionKeys = ModelState.Keys.Where(k => k.StartsWith("DistributionOption.", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var key in distributionKeys)
                {
                    ModelState.Remove(key);
                }

                // Handle empty SelectedStoreIds - if it's null, set to empty list
                if (dto.DistributionOption != null && dto.DistributionOption.SelectedStoreIds == null)
                {
                    dto.DistributionOption.SelectedStoreIds = new List<int>();
                }

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Convert Contributors to Dictionary for processing
                Dictionary<string, List<int>>? contributorsDict = null;
                
                // If Contributors list is provided directly, convert it
                if (dto.Contributors != null && dto.Contributors.Any())
                {
                    contributorsDict = ConvertContributorListToDictionary(dto.Contributors);
                    // Check if any contributors actually have values
                    var hasAnyContributors = contributorsDict.Values.Any(v => v != null && v.Any());
                    if (!hasAnyContributors)
                    {
                        // If Contributors list is empty, try to parse from form data
                        contributorsDict = ParseContributorsFromFormData();
                    }
                }
                else
                {
                    // If Contributors is null, try to parse from form data
                    contributorsDict = ParseContributorsFromFormData();
                }

                // Handle cover art file upload (same logic as Create endpoint)
                string? coverArtUrl = dto.CoverArtUrl; // Keep existing URL if no new file uploaded

                if (dto.CoverArtFile != null && dto.CoverArtFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var ext = Path.GetExtension(dto.CoverArtFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(ext))
                        return BadRequest(new { error = "Invalid image format" });

                    const long maxSize = 10 * 1024 * 1024;

                    if (dto.CoverArtFile.Length > maxSize)
                        return BadRequest(new { error = "Image too large" });

                    string root = _env.WebRootPath!;
                    string folder = Path.Combine(root, "images");

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string fileName = $"{Guid.NewGuid():N}{ext}";
                    string path = Path.Combine(folder, fileName);

                    using var stream = new FileStream(path, FileMode.Create);
                    await dto.CoverArtFile.CopyToAsync(stream);

                    coverArtUrl = "/images/" + fileName;
                }

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
                    cmd.Parameters.AddWithValue("@CoverArtUrl", string.IsNullOrWhiteSpace(coverArtUrl) ? DBNull.Value : coverArtUrl);
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

                    // Update contributors if provided (Dictionary format)
                    if (contributorsDict != null && contributorsDict.Any())
                    {
                        // Collect all artist IDs for validation
                        var allArtistIds = new List<int>();
                        foreach (var roleGroup in contributorsDict)
                        {
                            var role = roleGroup.Key;
                            var artistIds = roleGroup.Value;

                            // Validate role
                            if (!AllowedContributorRoles.Contains(role))
                            {
                                transaction.Rollback();
                                return BadRequest(new { error = $"Invalid role: {role}. Allowed roles are: {string.Join(", ", AllowedContributorRoles)}" });
                            }

                            if (artistIds != null && artistIds.Any())
                            {
                                // Filter valid artist IDs (> 0)
                                var validArtistIds = artistIds.Where(a => a > 0).ToList();
                                allArtistIds.AddRange(validArtistIds);
                            }
                        }

                        // Validate all artist IDs exist
                        if (allArtistIds.Any())
                        {
                            var uniqueArtistIds = allArtistIds.Distinct().ToList();
                            var paramNames = string.Join(",", uniqueArtistIds.Select((_, i) => $"@ArtistId{i}"));
                            using var validateContribCmd = new SqlCommand(
                                $"SELECT ArtistId FROM Artists WHERE ArtistId IN ({paramNames})",
                                conn, transaction);

                            for (int i = 0; i < uniqueArtistIds.Count; i++)
                            {
                                validateContribCmd.Parameters.AddWithValue($"@ArtistId{i}", uniqueArtistIds[i]);
                            }

                            using var contribReader = await validateContribCmd.ExecuteReaderAsync();
                            var existingContribArtistIds = new HashSet<int>();
                            while (await contribReader.ReadAsync())
                            {
                                existingContribArtistIds.Add(contribReader.GetInt32(0));
                            }
                            await contribReader.CloseAsync();

                            var missingContribArtistIds = uniqueArtistIds.Where(id => !existingContribArtistIds.Contains(id)).ToList();
                            if (missingContribArtistIds.Any())
                            {
                                transaction.Rollback();
                                return BadRequest(new { error = $"The following artist IDs do not exist: {string.Join(", ", missingContribArtistIds)}" });
                            }
                        }

                        // Delete existing contributors
                        using var delContribCmd = new SqlCommand(
                            "DELETE FROM ReleaseContributors WHERE ReleaseID = @ReleaseId",
                            conn, transaction);
                        delContribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                        await delContribCmd.ExecuteNonQueryAsync();

                        // Insert new contributors
                        foreach (var roleGroup in contributorsDict)
                        {
                            var role = roleGroup.Key;
                            var artistIds = roleGroup.Value;

                            if (artistIds == null || !artistIds.Any())
                                continue;

                            foreach (var artistId in artistIds.Where(a => a > 0))
                            {
                                using var contribCmd = new SqlCommand(
                                    "INSERT INTO ReleaseContributors (ReleaseID, ArtistID, Role) VALUES (@ReleaseId, @ArtistId, @Role)",
                                    conn, transaction);
                                contribCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                                contribCmd.Parameters.AddWithValue("@ArtistId", artistId);
                                contribCmd.Parameters.AddWithValue("@Role", role);
                                await contribCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    // Update distribution options if provided
                    if (dto.DistributionOption != null)
                    {
                        using var delStoreCmd = new SqlCommand(
                            "DELETE FROM ReleaseStores WHERE ReleaseId = @ReleaseId",
                            conn, transaction);
                        delStoreCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                        await delStoreCmd.ExecuteNonQueryAsync();

                        if (dto.DistributionOption?.DistributionType == "Manual" && dto.DistributionOption.SelectedStoreIds?.Any() == true)
                        {
                            foreach (var storeId in dto.DistributionOption.SelectedStoreIds)
                            {
                                using var storeCmd = new SqlCommand(
                                    "INSERT INTO ReleaseStores (ReleaseId, StoreId) VALUES (@ReleaseId, @StoreId)",
                                    conn, transaction);
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
        public Task<IActionResult> UpdateRelease(int releaseId, [FromForm] UpdateReleaseDto dto)
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
        [HttpPost("{releaseId}/status")]
        public async Task<IActionResult> UpdateStatus(int releaseId, [FromBody] UpdateStatusDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Status))
                    return BadRequest(new { error = "Status is required" });

                // Allowed statuses
                var allowedStatuses = new[]
                {
            "Draft",
            "QC Pending",
            "Approved",
            "Live",
            "Takedown",
            "Rejected"
        };

                if (!allowedStatuses.Contains(dto.Status))
                    return BadRequest(new { error = "Invalid status value" });

                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand(@"
            UPDATE Releases
            SET Status = @Status,
                UpdatedAt = SYSDATETIMEOFFSET()
            WHERE ReleaseId = @ReleaseId
              AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

                cmd.Parameters.AddWithValue("@Status", dto.Status);
                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

                await conn.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound(new { error = "Release not found" });

                return Ok(new
                {
                    message = "Status updated successfully",
                    releaseId = releaseId,
                    newStatus = dto.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }

}