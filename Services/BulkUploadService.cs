using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Services
{
    public class BulkUploadService
    {
        private readonly string _connStr;
        private readonly ILogger<BulkUploadService> _logger;

        public BulkUploadService(
            IConfiguration configuration,
            ILogger<BulkUploadService> logger)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<List<BulkUploadRow>> ParseFileAsync(string filePath)
        {
            var rows = new List<BulkUploadRow>();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".xlsx" || extension == ".xls")
            {
                return await ParseExcelAsync(filePath);
            }
            else if (extension == ".csv")
            {
                return await ParseCsvAsync(filePath);
            }
            else
            {
                throw new NotSupportedException($"File format {extension} is not supported. Please use .xlsx or .csv");
            }
        }

        private async Task<List<BulkUploadRow>> ParseExcelAsync(string filePath)
        {
            var rows = new List<BulkUploadRow>();

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
                throw new InvalidDataException("Excel file is empty");

            // Skip header row (row 1)
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var bulkRow = new BulkUploadRow
                {
                    ReleaseTitle = GetCellValue(worksheet, row, "A"),
                    ReleaseTitleVersion = GetCellValue(worksheet, row, "B"),
                    LabelId = GetIntValue(worksheet, row, "C"),
                    ReleaseDescription = GetCellValue(worksheet, row, "D"),
                    PrimaryGenre = GetCellValue(worksheet, row, "E"),
                    SecondaryGenre = GetCellValue(worksheet, row, "F"),
                    DigitalReleaseDate = GetDateTimeValue(worksheet, row, "G"),
                    OriginalReleaseDate = GetDateTimeValue(worksheet, row, "H"),
                    UPCCode = GetCellValue(worksheet, row, "I"),
                    TrackTitle = GetCellValue(worksheet, row, "J"),
                    TrackVersion = GetCellValue(worksheet, row, "K"),
                    PrimaryArtistIds = GetCellValue(worksheet, row, "L"),
                    FeaturedArtistIds = GetCellValue(worksheet, row, "M"),
                    ComposerIds = GetCellValue(worksheet, row, "N"),
                    LyricistIds = GetCellValue(worksheet, row, "O"),
                    ProducerIds = GetCellValue(worksheet, row, "P"),
                    ISRC = GetCellValue(worksheet, row, "Q"),
                    TrackNumber = GetIntValue(worksheet, row, "R"),
                    Language = GetCellValue(worksheet, row, "S"),
                    IsExplicit = GetBoolValue(worksheet, row, "T"),
                    IsInstrumental = GetBoolValue(worksheet, row, "U"),
                    TrackGenre = GetCellValue(worksheet, row, "V"),
                    DurationSeconds = GetIntValue(worksheet, row, "W")
                };

                rows.Add(bulkRow);
            }

            return await Task.FromResult(rows);
        }

        private async Task<List<BulkUploadRow>> ParseCsvAsync(string filePath)
        {
            var rows = new List<BulkUploadRow>();

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? line;
            bool isFirstLine = true;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    continue; // Skip header
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = ParseCsvLine(line);

                if (values.Count < 23) // Ensure we have enough columns
                    continue;

                var bulkRow = new BulkUploadRow
                {
                    ReleaseTitle = GetValue(values, 0),
                    ReleaseTitleVersion = GetValue(values, 1),
                    LabelId = GetIntValue(values, 2),
                    ReleaseDescription = GetValue(values, 3),
                    PrimaryGenre = GetValue(values, 4),
                    SecondaryGenre = GetValue(values, 5),
                    DigitalReleaseDate = GetDateTimeValue(values, 6),
                    OriginalReleaseDate = GetDateTimeValue(values, 7),
                    UPCCode = GetValue(values, 8),
                    TrackTitle = GetValue(values, 9),
                    TrackVersion = GetValue(values, 10),
                    PrimaryArtistIds = GetValue(values, 11),
                    FeaturedArtistIds = GetValue(values, 12),
                    ComposerIds = GetValue(values, 13),
                    LyricistIds = GetValue(values, 14),
                    ProducerIds = GetValue(values, 15),
                    ISRC = GetValue(values, 16),
                    TrackNumber = GetIntValue(values, 17),
                    Language = GetValue(values, 18),
                    IsExplicit = GetBoolValue(values, 19),
                    IsInstrumental = GetBoolValue(values, 20),
                    TrackGenre = GetValue(values, 21),
                    DurationSeconds = GetIntValue(values, 22)
                };

                rows.Add(bulkRow);
            }

            return rows;
        }

        private string? GetCellValue(ExcelWorksheet worksheet, int row, string column)
        {
            var cell = worksheet.Cells[$"{column}{row}"];
            return cell.Value?.ToString()?.Trim();
        }

        private int? GetIntValue(ExcelWorksheet worksheet, int row, string column)
        {
            var value = GetCellValue(worksheet, row, column);
            if (int.TryParse(value, out int result))
                return result;
            return null;
        }

        private DateTime? GetDateTimeValue(ExcelWorksheet worksheet, int row, string column)
        {
            var cell = worksheet.Cells[$"{column}{row}"];
            if (cell.Value is DateTime dt)
                return dt;
            if (cell.Value is double d)
                return DateTime.FromOADate(d);
            if (DateTime.TryParse(cell.Value?.ToString(), out DateTime parsed))
                return parsed;
            return null;
        }

        private bool? GetBoolValue(ExcelWorksheet worksheet, int row, string column)
        {
            var value = GetCellValue(worksheet, row, column);
            if (bool.TryParse(value, out bool result))
                return result;
            if (value?.ToLower() == "yes" || value?.ToLower() == "y" || value == "1")
                return true;
            if (value?.ToLower() == "no" || value?.ToLower() == "n" || value == "0")
                return false;
            return null;
        }

        private List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            values.Add(current.ToString().Trim());

            return values;
        }

        private string? GetValue(List<string> values, int index)
        {
            if (index < values.Count && !string.IsNullOrWhiteSpace(values[index]))
                return values[index].Trim();
            return null;
        }

        private int? GetIntValue(List<string> values, int index)
        {
            var value = GetValue(values, index);
            if (int.TryParse(value, out int result))
                return result;
            return null;
        }

        private DateTime? GetDateTimeValue(List<string> values, int index)
        {
            var value = GetValue(values, index);
            if (DateTime.TryParse(value, out DateTime result))
                return result;
            return null;
        }

        private bool? GetBoolValue(List<string> values, int index)
        {
            var value = GetValue(values, index);
            if (bool.TryParse(value, out bool result))
                return result;
            if (value?.ToLower() == "yes" || value?.ToLower() == "y" || value == "1")
                return true;
            if (value?.ToLower() == "no" || value?.ToLower() == "n" || value == "0")
                return false;
            return null;
        }

        public async Task<(int ReleaseId, int TrackId)> ProcessRowAsync(BulkUploadRow row, int jobId, int rowNumber)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(row.ReleaseTitle))
                throw new ArgumentException("ReleaseTitle is required");

            if (string.IsNullOrWhiteSpace(row.TrackTitle))
                throw new ArgumentException("TrackTitle is required");

            if (!row.LabelId.HasValue)
                throw new ArgumentException("LabelId is required");

            // Create Release
            int releaseId;
            using (var cmd = new SqlCommand(@"
                INSERT INTO Releases (Title, TitleVersion, LabelID, Description, PrimaryGenre, SecondaryGenre, 
                                     DigitalReleaseDate, OriginalReleaseDate, UPCCode, Status, CreatedAt)
                VALUES (@Title, @TitleVersion, @LabelID, @Description, @PrimaryGenre, @SecondaryGenre,
                        @DigitalReleaseDate, @OriginalReleaseDate, @UPCCode, 'Draft', SYSUTCDATETIME());
                SELECT SCOPE_IDENTITY();", conn))
            {
                cmd.Parameters.AddWithValue("@Title", row.ReleaseTitle);
                cmd.Parameters.AddWithValue("@TitleVersion", (object?)row.ReleaseTitleVersion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LabelID", row.LabelId.Value);
                cmd.Parameters.AddWithValue("@Description", (object?)row.ReleaseDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PrimaryGenre", (object?)row.PrimaryGenre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SecondaryGenre", (object?)row.SecondaryGenre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DigitalReleaseDate", (object?)row.DigitalReleaseDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OriginalReleaseDate", (object?)row.OriginalReleaseDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UPCCode", (object?)row.UPCCode ?? DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                releaseId = Convert.ToInt32(result);
            }

            // Parse artist IDs
            var primaryArtistIds = ParseArtistIds(row.PrimaryArtistIds);
            var featuredArtistIds = ParseArtistIds(row.FeaturedArtistIds);
            var composerIds = ParseArtistIds(row.ComposerIds);
            var lyricistIds = ParseArtistIds(row.LyricistIds);
            var producerIds = ParseArtistIds(row.ProducerIds);

            // Create Track
            int trackId;
            using (var cmd = new SqlCommand(@"
                INSERT INTO Tracks (ReleaseID, Title, TrackVersion, ISRC, TrackNumber, Language, 
                                   IsExplicit, IsInstrumental, TrackGenre, DurationSeconds, CreatedAt)
                VALUES (@ReleaseID, @Title, @TrackVersion, @ISRC, @TrackNumber, @Language,
                        @IsExplicit, @IsInstrumental, @TrackGenre, @DurationSeconds, SYSUTCDATETIME());
                SELECT SCOPE_IDENTITY();", conn))
            {
                cmd.Parameters.AddWithValue("@ReleaseID", releaseId);
                cmd.Parameters.AddWithValue("@Title", row.TrackTitle);
                cmd.Parameters.AddWithValue("@TrackVersion", (object?)row.TrackVersion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ISRC", (object?)row.ISRC ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TrackNumber", (object?)row.TrackNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Language", (object?)row.Language ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsExplicit", row.IsExplicit ?? false);
                cmd.Parameters.AddWithValue("@IsInstrumental", row.IsInstrumental ?? false);
                cmd.Parameters.AddWithValue("@TrackGenre", (object?)row.TrackGenre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DurationSeconds", (object?)row.DurationSeconds ?? DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                trackId = Convert.ToInt32(result);
            }

            // Link artists to track (simplified - you may need to adjust based on your schema)
            await LinkArtistsToTrack(conn, trackId, primaryArtistIds, "Primary");
            await LinkArtistsToTrack(conn, trackId, featuredArtistIds, "Featured");
            await LinkArtistsToTrack(conn, trackId, composerIds, "Composer");
            await LinkArtistsToTrack(conn, trackId, lyricistIds, "Lyricist");
            await LinkArtistsToTrack(conn, trackId, producerIds, "Producer");

            return (releaseId, trackId);
        }

        private List<int> ParseArtistIds(string? idsString)
        {
            if (string.IsNullOrWhiteSpace(idsString))
                return new List<int>();

            return idsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out int result) ? result : 0)
                .Where(id => id > 0)
                .ToList();
        }

        private async Task LinkArtistsToTrack(SqlConnection conn, int trackId, List<int> artistIds, string role)
        {
            foreach (var artistId in artistIds)
            {
                using var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM TrackArtists WHERE TrackID = @TrackID AND ArtistID = @ArtistID AND Role = @Role)
                    INSERT INTO TrackArtists (TrackID, ArtistID, Role, CreatedAt)
                    VALUES (@TrackID, @ArtistID, @Role, SYSUTCDATETIME())", conn);

                cmd.Parameters.AddWithValue("@TrackID", trackId);
                cmd.Parameters.AddWithValue("@ArtistID", artistId);
                cmd.Parameters.AddWithValue("@Role", role);

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}

