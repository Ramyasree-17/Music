using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using System.Data;
using System.Security.Claims;
using System.Text;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/bulk-upload")]
    [Authorize]
    [ApiExplorerSettings(GroupName = "Bulk Upload")]
    public class BulkUploadController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BulkUploadController> _logger;
        private readonly string _connStr;

        // In-memory tracking (no database tables)
        private static readonly Dictionary<string, BulkUploadStats> _uploadStats = new();

        public BulkUploadController(
            IConfiguration configuration,
            IWebHostEnvironment env,
            ILogger<BulkUploadController> logger)
        {
            _configuration = configuration;
            _env = env;
            _logger = logger;
            _connStr = configuration.GetConnectionString("DefaultConnection")!;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "File is required" });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls" && extension != ".csv")
                return BadRequest(new { error = "Only Excel (.xlsx, .xls) and CSV (.csv) files are supported" });

            // Extract JWT claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var brandingIdClaim = User.FindFirst("BrandingId");
            
            int? userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out int uid) ? uid : null;
            int? brandingId = brandingIdClaim != null && int.TryParse(brandingIdClaim.Value, out int bid) ? bid : null;

            try
            {
                // Save file to temporary location
                var uploadsFolder = Path.Combine(_env.ContentRootPath, "uploads", "bulk");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Parse file to get row count
                var rows = await ParseFileAsync(filePath);
                var totalRows = rows.Count;

                if (totalRows == 0)
                {
                    _logger.LogWarning("File {FileName} contains no data rows after parsing. File may be empty or all rows were filtered out.", file.FileName);
                    return BadRequest(new { error = "File contains no data rows. Please ensure the file has valid data rows with ReleaseTitle and TrackTitle." });
                }

                // Initialize stats tracking
                var uploadId = Guid.NewGuid().ToString();
                _uploadStats[uploadId] = new BulkUploadStats
                {
                    TotalRows = totalRows,
                    ProcessedRows = 0,
                    SuccessfulRows = 0,
                    FailedRows = 0,
                    Status = "Processing"
                };

                // Process in background (non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessBulkUploadAsync(filePath, rows, userId, brandingId, uploadId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fatal error in background bulk upload processing: {Message}, Inner: {InnerMessage}", 
                            ex.Message, ex.InnerException?.Message);
                        if (_uploadStats.ContainsKey(uploadId))
                        {
                            _uploadStats[uploadId].Status = "Failed";
                        }
                    }
                });

                return Ok(new
                {
                    message = "Bulk upload started",
                    status = "Processing in background",
                    uploadId = uploadId,
                    totalRows = totalRows
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading bulk file: {Message}, Inner: {InnerMessage}", 
                    ex.Message, ex.InnerException?.Message);
                return StatusCode(500, new { error = $"Error processing file: {ex.Message}" });
            }
        }

        [HttpGet("status/{uploadId}")]
        public IActionResult GetUploadStatus(string uploadId)
        {
            if (!_uploadStats.ContainsKey(uploadId))
                return NotFound(new { error = "Upload not found" });

            var stats = _uploadStats[uploadId];
            
            // Return first 50 validation errors (to avoid huge response)
            var validationErrorsPreview = stats.ValidationErrorDetails
                .Take(50)
                .Select(e => new
                {
                    rowNumber = e.RowNumber,
                    releaseTitle = e.ReleaseTitle,
                    trackTitle = e.TrackTitle,
                    errors = e.Errors
                })
                .ToList();
            
            // Return first 20 database errors (to avoid huge response)
            var databaseErrorsPreview = stats.DatabaseErrorDetails
                .Take(20)
                .Select(e => new
                {
                    rowNumber = e.RowNumber,
                    releaseTitle = e.ReleaseTitle,
                    trackTitle = e.TrackTitle,
                    errorMessage = e.ErrorMessage,
                    innerErrorMessage = e.InnerErrorMessage,
                    sqlErrorNumber = e.SqlErrorNumber
                })
                .ToList();
            
            return Ok(new
            {
                uploadId = uploadId,
                totalRows = stats.TotalRows,
                processedRows = stats.ProcessedRows,
                successfulRows = stats.SuccessfulRows,
                failedRows = stats.FailedRows,
                validationErrors = stats.ValidationErrors,
                status = stats.Status,
                progressPercentage = stats.TotalRows > 0 
                    ? Math.Round((double)stats.ProcessedRows / stats.TotalRows * 100, 2) 
                    : 0,
                validationErrorDetails = validationErrorsPreview,
                hasMoreValidationErrors = stats.ValidationErrorDetails.Count > 50,
                totalValidationErrors = stats.ValidationErrorDetails.Count,
                databaseErrorDetails = databaseErrorsPreview,
                hasMoreDatabaseErrors = stats.DatabaseErrorDetails.Count > 20,
                totalDatabaseErrors = stats.DatabaseErrorDetails.Count
            });
        }

        [HttpGet("status/{uploadId}/errors")]
        public IActionResult GetValidationErrors(string uploadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            if (!_uploadStats.ContainsKey(uploadId))
                return NotFound(new { error = "Upload not found" });

            var stats = _uploadStats[uploadId];
            
            var totalErrors = stats.ValidationErrorDetails.Count;
            var skip = (page - 1) * pageSize;
            var errors = stats.ValidationErrorDetails
                .Skip(skip)
                .Take(pageSize)
                .Select(e => new
                {
                    rowNumber = e.RowNumber,
                    releaseTitle = e.ReleaseTitle,
                    trackTitle = e.TrackTitle,
                    errors = e.Errors
                })
                .ToList();
            
            return Ok(new
            {
                uploadId = uploadId,
                page = page,
                pageSize = pageSize,
                totalErrors = totalErrors,
                totalPages = (int)Math.Ceiling((double)totalErrors / pageSize),
                errors = errors
            });
        }

        // =====================================================
        // Background Processing
        // =====================================================
        private async Task ProcessBulkUploadAsync(string filePath, List<BulkUploadRow> rows, int? userId, int? brandingId, string uploadId)
        {
            BulkUploadStats? stats = null;
            
            try
            {
                stats = _uploadStats[uploadId];
                
                // Ensure stats exist and are initialized
                if (stats == null)
                {
                    _logger.LogError("Stats not found for upload {UploadId}", uploadId);
                    return;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var rowNumber = i + 1;

                    try
                    {
                        // Validate row before processing
                        var validation = ValidateRow(row, rowNumber, userId);
                        
                        if (!validation.IsValid)
                        {
                            // Skip invalid rows - log validation errors
                            stats.ValidationErrors++;
                            stats.FailedRows++;
                            
                            // Store detailed validation error
                            stats.ValidationErrorDetails.Add(new RowValidationError
                            {
                                RowNumber = rowNumber,
                                ReleaseTitle = row.ReleaseTitle ?? "N/A",
                                TrackTitle = row.TrackTitle ?? "N/A",
                                Errors = validation.Errors
                            });
                            
                            var errorMessages = string.Join("; ", validation.Errors);
                            _logger.LogWarning(
                                "Row {RowNumber} skipped due to validation errors: {Errors}. ReleaseTitle='{ReleaseTitle}', TrackTitle='{TrackTitle}'",
                                rowNumber, errorMessages, row.ReleaseTitle ?? "N/A", row.TrackTitle ?? "N/A");
                            
                            continue; // Skip to next row
                        }

                        // Process valid row
                        await ProcessRowAsync(row, userId, brandingId, rowNumber, validation);
                        stats.SuccessfulRows++;
                        _logger.LogInformation("Row {RowNumber} processed successfully: Release='{ReleaseTitle}', Track='{TrackTitle}'", 
                            rowNumber, row.ReleaseTitle, row.TrackTitle);
                    }
                catch (Exception ex)
                {
                    stats.FailedRows++;
                    
                    // Log detailed error information
                    var errorMessage = ex.Message;
                    var innerMessage = ex.InnerException?.Message ?? "No inner exception";
                    
                    // Store database error details
                    var dbError = new RowDatabaseError
                    {
                        RowNumber = rowNumber,
                        ReleaseTitle = row.ReleaseTitle ?? "N/A",
                        TrackTitle = row.TrackTitle ?? "N/A",
                        ErrorMessage = errorMessage,
                        InnerErrorMessage = ex.InnerException != null ? innerMessage : null
                    };
                    
                    // Extract SQL error number if it's a SqlException
                    if (ex is SqlException sqlEx)
                    {
                        dbError.SqlErrorNumber = sqlEx.Number;
                    }
                    
                    stats.DatabaseErrorDetails.Add(dbError);
                    
                    _logger.LogError(ex, 
                        "Error processing row {RowNumber}: Message={Message}, InnerMessage={InnerMessage}, " +
                        "ReleaseTitle={ReleaseTitle}, TrackTitle={TrackTitle}, LabelId={LabelId}",
                        rowNumber, errorMessage, innerMessage, 
                        row.ReleaseTitle ?? "N/A", row.TrackTitle ?? "N/A", row.LabelId);
                }
                finally
                {
                    stats.ProcessedRows++;
                }
            }

                // Mark as completed
                stats.Status = "Completed";
                _logger.LogInformation("Bulk upload {UploadId} completed. Total: {Total}, Success: {Success}, Failed: {Failed}, ValidationErrors: {ValidationErrors}", 
                    uploadId, stats.TotalRows, stats.SuccessfulRows, stats.FailedRows, stats.ValidationErrors);

                // Cleanup file after processing
                try
                {
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete temporary file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected errors to prevent background job from crashing
                _logger.LogError(ex, "Fatal error in bulk upload processing {UploadId}: {Message}, Inner: {InnerMessage}", 
                    uploadId, ex.Message, ex.InnerException?.Message ?? "No inner exception");
                
                if (stats != null)
                {
                    stats.Status = "Failed";
                }
            }
        }

        // =====================================================
        // Validate Row
        // =====================================================
        private ValidationResult ValidateRow(BulkUploadRow row, int rowNumber, int? userId)
        {
            var result = new ValidationResult { IsValid = true };

            // Clean and check ReleaseTitle
            var releaseTitle = row.ReleaseTitle?.Trim();
            if (string.IsNullOrWhiteSpace(releaseTitle) || 
                releaseTitle.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                releaseTitle.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                releaseTitle = null;
            }

            // Clean and check TrackTitle
            var trackTitle = row.TrackTitle?.Trim();
            if (string.IsNullOrWhiteSpace(trackTitle) || 
                trackTitle.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                trackTitle.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                trackTitle = null;
            }

            // Validate Release Title (required)
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                result.IsValid = false;
                result.Errors.Add("ReleaseTitle is required and cannot be empty or N/A");
            }

            // Validate Track Title (required)
            if (string.IsNullOrWhiteSpace(trackTitle))
            {
                result.IsValid = false;
                result.Errors.Add("TrackTitle is required and cannot be empty or N/A");
            }

            // Validate Track Number (if provided, must be numeric and > 0)
            if (row.TrackNumber.HasValue)
            {
                if (row.TrackNumber.Value <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"TrackNumber must be greater than 0, got: {row.TrackNumber.Value}");
                }
            }

            // Validate LabelId (if provided, must be > 0)
            if (row.LabelId.HasValue && row.LabelId.Value <= 0)
            {
                result.IsValid = false;
                result.Errors.Add($"LabelId must be greater than 0, got: {row.LabelId.Value}");
            }

            // Validate UserId from JWT
            if (!userId.HasValue || userId.Value <= 0)
            {
                result.IsValid = false;
                result.Errors.Add("UserId is required from JWT token");
            }

            // Validate DurationSeconds (if provided, must be numeric and >= 0)
            if (row.DurationSeconds.HasValue && row.DurationSeconds.Value < 0)
            {
                result.IsValid = false;
                result.Errors.Add($"DurationSeconds must be >= 0, got: {row.DurationSeconds.Value}");
            }

            // Validate boolean fields (already parsed, but check for consistency)
            // IsExplicit and IsInstrumental are nullable bools, so they're fine

            return result;
        }

        // =====================================================
        // Process Single Row (after validation)
        // =====================================================
        private async Task ProcessRowAsync(BulkUploadRow row, int? userId, int? brandingId, int rowNumber, ValidationResult validation)
        {
            // Safe defaults for required fields
            var labelId = row.LabelId ?? 1;  // Default to 1 if not in sheet
            var enterpriseId = 1;  // Default value
            var artistId = 1;  // Default value
            var fileTypeId = 1;  // Default value
            var trackNumber = row.TrackNumber ?? 1;  // Default to 1 if not in sheet

            // Extract artist ID from sheet if available (safely parsed)
            var primaryArtistIds = ParseArtistIds(row.PrimaryArtistIds);
            if (primaryArtistIds.Count > 0)
            {
                artistId = primaryArtistIds[0];
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Use transaction for atomicity
            using var transaction = conn.BeginTransaction();
            
            try
            {
                int releaseId = 0;
                int trackId = 0;

                // Step 1: Insert Release with all required NOT NULL fields
                try
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Releases (
                            LabelID, 
                            EnterpriseID, 
                            Title, 
                            IsDeleted, 
                            UpcProvided,
                            CreatedAt,
                            Status,
                            UPCCode,
                            PrimaryGenre,
                            SecondaryGenre,
                            DigitalReleaseDate,
                            OriginalReleaseDate,
                            Description,
                            CoverArtUrl
                        )
                        VALUES (
                            @LabelID, 
                            @EnterpriseID, 
                            @Title, 
                            @IsDeleted, 
                            @UpcProvided,
                            SYSUTCDATETIME(),
                            'Draft',
                            @UPCCode,
                            @PrimaryGenre,
                            @SecondaryGenre,
                            @DigitalReleaseDate,
                            @OriginalReleaseDate,
                            @Description,
                            @CoverArtUrl
                        );
                        SELECT SCOPE_IDENTITY();", conn, transaction);

                    cmd.Parameters.AddWithValue("@LabelID", labelId);
                    cmd.Parameters.AddWithValue("@EnterpriseID", enterpriseId);
                    cmd.Parameters.AddWithValue("@Title", row.ReleaseTitle);
                    cmd.Parameters.AddWithValue("@IsDeleted", false);
                    cmd.Parameters.AddWithValue("@UpcProvided", !string.IsNullOrWhiteSpace(row.UPCCode));
                    
                    // Optional fields
                    cmd.Parameters.AddWithValue("@UPCCode", (object?)row.UPCCode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrimaryGenre", (object?)row.PrimaryGenre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SecondaryGenre", (object?)row.SecondaryGenre ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DigitalReleaseDate", (object?)row.DigitalReleaseDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@OriginalReleaseDate", (object?)row.OriginalReleaseDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", (object?)row.ReleaseDescription ?? DBNull.Value);
                    
                    cmd.Parameters.AddWithValue("@CoverArtUrl", (object?)row.CoverArtUrl ?? DBNull.Value);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        releaseId = Convert.ToInt32(result);
                        _logger.LogDebug("Row {RowNumber}: Created Release with ID {ReleaseId}", rowNumber, releaseId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"INSERT INTO Releases returned null. Check required fields: LabelID, EnterpriseID, Title, IsDeleted, UpcProvided.");
                    }
                }
                catch (SqlException sqlEx)
                {
                    var errorMsg = $"Row {rowNumber}: SQL error creating Release. Error: {sqlEx.Message}";
                    if (sqlEx.InnerException != null)
                        errorMsg += $" Inner: {sqlEx.InnerException.Message}";
                    errorMsg += $" SQL Error Number: {sqlEx.Number}, State: {sqlEx.State}";
                    
                    _logger.LogError(sqlEx, errorMsg);
                    transaction.Rollback();
                    throw new Exception(errorMsg, sqlEx);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Row {rowNumber}: Failed to create Release. Error: {ex.Message}";
                    if (ex.InnerException != null)
                        errorMsg += $" Inner: {ex.InnerException.Message}";
                    
                    _logger.LogError(ex, errorMsg);
                    transaction.Rollback();
                    throw new Exception(errorMsg, ex);
                }

                // Step 2: Insert Track with all required NOT NULL fields
                try
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Tracks (
                            ReleaseID, 
                            TrackNumber, 
                            Title, 
                            ArtistID, 
                            IsDeleted, 
                            IsExplicit, 
                            IsInstrumental,
                            CreatedAt,
                            Status,
                            ISRC,
                            Language,
                            Genre
                        )
                        VALUES (
                            @ReleaseID, 
                            @TrackNumber, 
                            @Title, 
                            @ArtistID, 
                            @IsDeleted, 
                            @IsExplicit, 
                            @IsInstrumental,
                            SYSUTCDATETIME(),
                            'Active',
                            @ISRC,
                            @Language,
                            @Genre
                        );
                        SELECT SCOPE_IDENTITY();", conn, transaction);

                    cmd.Parameters.AddWithValue("@ReleaseID", releaseId);
                    cmd.Parameters.AddWithValue("@TrackNumber", trackNumber);
                    cmd.Parameters.AddWithValue("@Title", row.TrackTitle);
                    cmd.Parameters.AddWithValue("@ArtistID", artistId);
                    cmd.Parameters.AddWithValue("@IsDeleted", false);
                    cmd.Parameters.AddWithValue("@IsExplicit", row.IsExplicit ?? false);
                    cmd.Parameters.AddWithValue("@IsInstrumental", row.IsInstrumental ?? false);
                    cmd.Parameters.AddWithValue("@ISRC", (object?)row.ISRC ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Language", (object?)row.Language ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Genre", (object?)row.TrackGenre ?? DBNull.Value);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        trackId = Convert.ToInt32(result);
                        _logger.LogDebug("Row {RowNumber}: Created Track with ID {TrackId} for Release {ReleaseId}", rowNumber, trackId, releaseId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"INSERT INTO Tracks returned null. Check required fields: ReleaseID, TrackNumber, Title, ArtistID, IsDeleted, IsExplicit, IsInstrumental.");
                    }
                }
                catch (SqlException sqlEx)
                {
                    var errorMsg = $"Row {rowNumber}: SQL error creating Track. Error: {sqlEx.Message}";
                    if (sqlEx.InnerException != null)
                        errorMsg += $" Inner: {sqlEx.InnerException.Message}";
                    errorMsg += $" SQL Error Number: {sqlEx.Number}, State: {sqlEx.State}";
                    
                    _logger.LogError(sqlEx, errorMsg);
                    transaction.Rollback();
                    throw new Exception(errorMsg, sqlEx);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Row {rowNumber}: Failed to create Track. Error: {ex.Message}";
                    if (ex.InnerException != null)
                        errorMsg += $" Inner: {ex.InnerException.Message}";
                    
                    _logger.LogError(ex, errorMsg);
                    transaction.Rollback();
                    throw new Exception(errorMsg, ex);
                }

                // Step 3: Insert File record (always create, even if FileUrl is empty)
                try
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO Files (
                            ReleaseId, 
                            TrackId, 
                            FileTypeId,
                            Status,
                            CreatedAt,
                            CloudfrontUrl
                        )
                        VALUES (
                            @ReleaseId, 
                            @TrackId, 
                            @FileTypeId,
                            'Active',
                            SYSUTCDATETIME(),
                            @CloudfrontUrl
                        );
                        SELECT SCOPE_IDENTITY();", conn, transaction);

                    cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                    cmd.Parameters.AddWithValue("@TrackId", trackId);
                    cmd.Parameters.AddWithValue("@FileTypeId", fileTypeId);
                    cmd.Parameters.AddWithValue("@CloudfrontUrl", (object?)row.FileUrl ?? DBNull.Value);

                    var fileId = await cmd.ExecuteScalarAsync();
                    if (fileId != null && fileId != DBNull.Value)
                    {
                        _logger.LogDebug("Row {RowNumber}: Created File with ID {FileId} for Release {ReleaseId}, Track {TrackId}", 
                            rowNumber, fileId, releaseId, trackId);
                    }
                }
                catch (SqlException sqlEx)
                {
                    // Log but don't fail the row - file creation is less critical
                    _logger.LogWarning(sqlEx, "Row {RowNumber}: SQL error creating File. Error: {Message}, SQL Error Number: {Number}", 
                        rowNumber, sqlEx.Message, sqlEx.Number);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the row - file creation is optional
                    _logger.LogWarning(ex, "Row {RowNumber}: Failed to create File record. Error: {Message}. Continuing...", 
                        rowNumber, ex.Message);
                }

                // Commit transaction if all critical operations succeeded
                transaction.Commit();
                _logger.LogInformation("Row {RowNumber}: Successfully inserted Release {ReleaseId}, Track {TrackId}", 
                    rowNumber, releaseId, trackId);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Row {RowNumber}: Transaction rolled back due to error: {Message}", rowNumber, ex.Message);
                throw; // Re-throw to be caught by outer handler
            }
        }

        // =====================================================
        // File Parsing
        // =====================================================
        private async Task<List<BulkUploadRow>> ParseFileAsync(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".xlsx" || extension == ".xls")
                return await ParseExcelAsync(filePath);
            else
                return await ParseCsvAsync(filePath);
        }

        private async Task<List<BulkUploadRow>> ParseExcelAsync(string filePath)
        {
            var rows = new List<BulkUploadRow>();

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
                return rows;

            bool inDataSection = false;
            var metadataFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "description", "format_version", "total_releases", "total_tracks",
                "format-version", "total-releases", "total-tracks"
            };

            // Skip header row (row 1) and process from row 2
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var firstCell = GetCellValue(worksheet, row, "A");
                
                // Skip rows starting with '#'
                if (!string.IsNullOrWhiteSpace(firstCell) && firstCell.Trim().StartsWith("#"))
                {
                    // Check if this is a section header
                    if (firstCell.Trim().Equals("#release_info", StringComparison.OrdinalIgnoreCase) ||
                        firstCell.Trim().Equals("#track_info", StringComparison.OrdinalIgnoreCase))
                    {
                        inDataSection = true;
                        _logger.LogDebug("Found data section: {Section}", firstCell.Trim());
                    }
                    else
                    {
                        inDataSection = false;
                    }
                    continue;
                }

                // Skip metadata rows
                if (!string.IsNullOrWhiteSpace(firstCell))
                {
                    var trimmed = firstCell.Trim();
                    if (metadataFields.Contains(trimmed))
                    {
                        continue;
                    }
                }

                // Only process rows if we're in a data section
                if (!inDataSection)
                {
                    continue;
                }

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
                    DurationSeconds = GetIntValue(worksheet, row, "W"),
                    FileUrl = GetCellValue(worksheet, row, "X") // File URL column
                };

                // Skip rows where both ReleaseTitle and TrackTitle are empty or N/A
                var releaseTitle = bulkRow.ReleaseTitle?.Trim();
                var trackTitle = bulkRow.TrackTitle?.Trim();
                
                if (string.IsNullOrWhiteSpace(releaseTitle) || 
                    releaseTitle.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                    releaseTitle.Equals("NA", StringComparison.OrdinalIgnoreCase))
                {
                    releaseTitle = null;
                }
                
                if (string.IsNullOrWhiteSpace(trackTitle) || 
                    trackTitle.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                    trackTitle.Equals("NA", StringComparison.OrdinalIgnoreCase))
                {
                    trackTitle = null;
                }

                // Skip if both are empty/null
                if (string.IsNullOrWhiteSpace(releaseTitle) && string.IsNullOrWhiteSpace(trackTitle))
                {
                    continue;
                }

                // Update the row with cleaned values
                bulkRow.ReleaseTitle = releaseTitle;
                bulkRow.TrackTitle = trackTitle;

                rows.Add(bulkRow);
            }

            _logger.LogInformation("Parsed {Count} data rows from Excel file", rows.Count);
            return await Task.FromResult(rows);
        }

        private async Task<List<BulkUploadRow>> ParseCsvAsync(string filePath)
        {
            var rows = new List<BulkUploadRow>();

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? line;
            Dictionary<string, int>? columnMap = null;
            bool headerFound = false;
            int lineNumber = 0;

            // Metadata fields to skip (before header row)
            var metadataFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "total_tracks", "total-releases", "total_releases",
                "#release_info", "#track_info", "release_info", "track_info"
            };

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Skip empty rows before header is found
                    if (!headerFound)
                        continue;
                    // After header, empty rows might indicate end of data
                    continue;
                }

                var trimmedLine = line.Trim();

                // Skip metadata rows before header
                if (!headerFound)
                {
                    var firstValue = trimmedLine.Split(',')[0].Trim().ToLowerInvariant();
                    
                    // Check if this is the header row (starts with "#action")
                    if (trimmedLine.StartsWith("#action", StringComparison.OrdinalIgnoreCase))
                    {
                        // Parse header row to create column map
                        var headerValues = ParseCsvLine(line);
                        columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        
                        for (int i = 0; i < headerValues.Count; i++)
                        {
                            var columnName = headerValues[i].Trim();
                            if (!string.IsNullOrWhiteSpace(columnName))
                            {
                                // Store column name (with or without #) and its index
                                columnMap[columnName] = i;
                                // Also store without # prefix for easier lookup
                                if (columnName.StartsWith("#"))
                                {
                                    columnMap[columnName.Substring(1)] = i;
                                }
                            }
                        }
                        
                        headerFound = true;
                        _logger.LogInformation("Found header row at line {LineNumber} with {ColumnCount} columns", lineNumber, columnMap.Count);
                        continue; // Skip header row itself
                    }
                    
                    // Skip metadata rows
                    if (metadataFields.Contains(firstValue) || 
                        firstValue.StartsWith("#release_info", StringComparison.OrdinalIgnoreCase) ||
                        firstValue.StartsWith("#track_info", StringComparison.OrdinalIgnoreCase) ||
                        firstValue.StartsWith("total_", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Skipping metadata row at line {LineNumber}: {Value}", lineNumber, firstValue);
                        continue;
                    }
                    
                    // Skip any other rows starting with # before header
                    if (trimmedLine.StartsWith("#"))
                    {
                        continue;
                    }
                }

                // After header is found, parse data rows
                if (headerFound && columnMap != null)
                {
                    var rowValues = ParseCsvLine(line);
                    
                    // Skip rows with no data
                    if (rowValues.Count == 0)
                    {
                        continue;
                    }

                    // Helper function to get value by column name
                    string? GetColumnValue(string columnName)
                    {
                        if (columnMap.TryGetValue(columnName, out int index) && index < rowValues.Count)
                        {
                            var value = rowValues[index]?.Trim();
                            // Skip placeholder values
                            if (string.IsNullOrWhiteSpace(value) || 
                                value.Equals("string", StringComparison.OrdinalIgnoreCase) ||
                                value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                                value.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                                value.Equals("NA", StringComparison.OrdinalIgnoreCase))
                            {
                                return null;
                            }
                            return value;
                        }
                        return null;
                    }

                    var bulkRow = new BulkUploadRow();

                    // Map Release fields
                    bulkRow.ReleaseTitle = GetColumnValue("#title") ?? GetColumnValue("title");
                    bulkRow.UPCCode = GetColumnValue("#upc") ?? GetColumnValue("upc");
                    bulkRow.PrimaryGenre = GetColumnValue("#primary_genre") ?? GetColumnValue("primary_genre");
                    bulkRow.DigitalReleaseDate = GetDateTimeValueFromString(GetColumnValue("#digital_release") ?? GetColumnValue("digital_release"));
                    bulkRow.OriginalReleaseDate = GetDateTimeValueFromString(GetColumnValue("#original_release") ?? GetColumnValue("original_release"));
                    bulkRow.ReleaseDescription = GetColumnValue("#p_line") ?? GetColumnValue("p_line");
                    bulkRow.CoverArtUrl = GetColumnValue("#cover_url") ?? GetColumnValue("cover_url");

                    // Map Track fields
                    bulkRow.TrackTitle = GetColumnValue("#track_title") ?? GetColumnValue("track_title");
                    bulkRow.ISRC = GetColumnValue("#isrc") ?? GetColumnValue("isrc");
                    bulkRow.TrackGenre = GetColumnValue("#primary_genre") ?? GetColumnValue("primary_genre");
                    bulkRow.Language = GetColumnValue("#language") ?? GetColumnValue("language");
                    
                    // Map explicit_lyrics to IsExplicit
                    var explicitValue = GetColumnValue("#explicit_lyrics") ?? GetColumnValue("explicit_lyrics");
                    if (!string.IsNullOrWhiteSpace(explicitValue))
                    {
                        bulkRow.IsExplicit = GetBoolValueFromString(explicitValue);
                    }

                    // Map File fields
                    bulkRow.FileUrl = GetColumnValue("#audio_url") ?? GetColumnValue("audio_url");

                    // Clean and validate titles
                    var releaseTitle = bulkRow.ReleaseTitle?.Trim();
                    var trackTitle = bulkRow.TrackTitle?.Trim();
                    
                    if (string.IsNullOrWhiteSpace(releaseTitle) || 
                        releaseTitle.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                        releaseTitle.Equals("NA", StringComparison.OrdinalIgnoreCase))
                    {
                        releaseTitle = null;
                    }
                    
                    if (string.IsNullOrWhiteSpace(trackTitle) || 
                        trackTitle.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                        trackTitle.Equals("NA", StringComparison.OrdinalIgnoreCase))
                    {
                        trackTitle = null;
                    }

                    // Skip if both titles are empty (invalid row)
                    if (string.IsNullOrWhiteSpace(releaseTitle) && string.IsNullOrWhiteSpace(trackTitle))
                    {
                        _logger.LogDebug("Skipping empty data row at line {LineNumber} (both titles empty)", lineNumber);
                        continue;
                    }
                    
                    // If only one title exists, use it for both
                    if (string.IsNullOrWhiteSpace(releaseTitle) && !string.IsNullOrWhiteSpace(trackTitle))
                    {
                        releaseTitle = trackTitle;
                        _logger.LogDebug("Row {LineNumber}: Using TrackTitle as ReleaseTitle", lineNumber);
                    }
                    else if (!string.IsNullOrWhiteSpace(releaseTitle) && string.IsNullOrWhiteSpace(trackTitle))
                    {
                        trackTitle = releaseTitle;
                        _logger.LogDebug("Row {LineNumber}: Using ReleaseTitle as TrackTitle", lineNumber);
                    }

                    // Update the row with cleaned values
                    bulkRow.ReleaseTitle = releaseTitle;
                    bulkRow.TrackTitle = trackTitle;

                    // Handle UPC code - convert scientific notation to string
                    if (!string.IsNullOrWhiteSpace(bulkRow.UPCCode))
                    {
                        // If UPC is in scientific notation (e.g., 1.23456E+12), convert it
                        if (double.TryParse(bulkRow.UPCCode, System.Globalization.NumberStyles.Float, 
                            System.Globalization.CultureInfo.InvariantCulture, out double upcValue))
                        {
                            // Convert to long to remove decimal, then to string
                            bulkRow.UPCCode = ((long)upcValue).ToString();
                        }
                    }

                    rows.Add(bulkRow);
                    _logger.LogDebug("Added data row at line {LineNumber}: Release='{Release}', Track='{Track}'", 
                        lineNumber, releaseTitle ?? "N/A", trackTitle ?? "N/A");
                }
            }

            if (!headerFound)
            {
                _logger.LogWarning("Header row starting with '#action' not found in CSV file");
            }

            _logger.LogInformation("Parsed {Count} data rows from CSV file (processed {Lines} total lines)", rows.Count, lineNumber);
            return rows;
        }

        // =====================================================
        // Helper Methods
        // =====================================================
        private string? GetCellValue(ExcelWorksheet worksheet, int row, string column)
        {
            var cell = worksheet.Cells[$"{column}{row}"];
            return cell.Value?.ToString()?.Trim();
        }

        private int? GetIntValue(ExcelWorksheet worksheet, int row, string column)
        {
            var value = GetCellValue(worksheet, row, column);
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            if (int.TryParse(value, out int result))
                return result;
            
            // Try parsing as double first (Excel sometimes stores numbers as doubles)
            if (double.TryParse(value, out double doubleValue))
            {
                return (int)Math.Round(doubleValue);
            }
            
            return null;
        }

        private DateTime? GetDateTimeValue(ExcelWorksheet worksheet, int row, string column)
        {
            var cell = worksheet.Cells[$"{column}{row}"];
            if (cell.Value is DateTime dt) return dt;
            if (cell.Value is double d) return DateTime.FromOADate(d);
            if (DateTime.TryParse(cell.Value?.ToString(), out DateTime parsed)) return parsed;
            return null;
        }

        private bool? GetBoolValue(ExcelWorksheet worksheet, int row, string column)
        {
            var value = GetCellValue(worksheet, row, column);
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            // Try standard bool parsing
            if (bool.TryParse(value, out bool result))
                return result;
            
            // Try common string representations
            var lowerValue = value.ToLower().Trim();
            if (lowerValue == "yes" || lowerValue == "y" || lowerValue == "1" || lowerValue == "true")
                return true;
            
            if (lowerValue == "no" || lowerValue == "n" || lowerValue == "0" || lowerValue == "false")
                return false;
            
            // Try parsing as number (1 = true, 0 = false)
            if (int.TryParse(value, out int intValue))
                return intValue != 0;
            
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
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            if (int.TryParse(value, out int result))
                return result;
            
            // Try parsing as double first
            if (double.TryParse(value, out double doubleValue))
            {
                return (int)Math.Round(doubleValue);
            }
            
            return null;
        }

        private DateTime? GetDateTimeValue(List<string> values, int index)
        {
            var value = GetValue(values, index);
            return DateTime.TryParse(value, out DateTime result) ? result : null;
        }

        private bool? GetBoolValue(List<string> values, int index)
        {
            var value = GetValue(values, index);
            return GetBoolValueFromString(value);
        }

        private DateTime? GetDateTimeValueFromString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            if (DateTime.TryParse(value, out DateTime result))
                return result;
            
            return null;
        }

        private bool? GetBoolValueFromString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            // Try standard bool parsing
            if (bool.TryParse(value, out bool result))
                return result;
            
            // Try common string representations
            var lowerValue = value.ToLower().Trim();
            if (lowerValue == "yes" || lowerValue == "y" || lowerValue == "1" || lowerValue == "true")
                return true;
            
            if (lowerValue == "no" || lowerValue == "n" || lowerValue == "0" || lowerValue == "false")
                return false;
            
            // Try parsing as number (1 = true, 0 = false)
            if (int.TryParse(value, out int intValue))
                return intValue != 0;
            
            return null;
        }

        private List<int> ParseArtistIds(string? idsString)
        {
            if (string.IsNullOrWhiteSpace(idsString))
                return new List<int>();

            var artistIds = new List<int>();
            var parts = idsString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (int.TryParse(trimmed, out int artistId) && artistId > 0)
                {
                    artistIds.Add(artistId);
                }
                // Silently skip invalid artist IDs (non-numeric or <= 0)
            }
            
            return artistIds;
        }

        private async Task LinkArtistsToTrack(SqlConnection conn, SqlTransaction transaction, int trackId, BulkUploadRow row)
        {
            // Link primary artists
            var primaryIds = ParseArtistIds(row.PrimaryArtistIds);
            foreach (var artistId in primaryIds)
            {
                await LinkArtist(conn, transaction, trackId, artistId, "Primary");
            }

            // Link featured artists
            var featuredIds = ParseArtistIds(row.FeaturedArtistIds);
            foreach (var artistId in featuredIds)
            {
                await LinkArtist(conn, transaction, trackId, artistId, "Featured");
            }

            // Link composers
            var composerIds = ParseArtistIds(row.ComposerIds);
            foreach (var artistId in composerIds)
            {
                await LinkArtist(conn, transaction, trackId, artistId, "Composer");
            }

            // Link lyricists
            var lyricistIds = ParseArtistIds(row.LyricistIds);
            foreach (var artistId in lyricistIds)
            {
                await LinkArtist(conn, transaction, trackId, artistId, "Lyricist");
            }

            // Link producers
            var producerIds = ParseArtistIds(row.ProducerIds);
            foreach (var artistId in producerIds)
            {
                await LinkArtist(conn, transaction, trackId, artistId, "Producer");
            }
        }

        private async Task LinkArtist(SqlConnection conn, SqlTransaction transaction, int trackId, int artistId, string role)
        {
            try
            {
                using var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM TrackArtists WHERE TrackID = @TrackID AND ArtistID = @ArtistID AND Role = @Role)
                    INSERT INTO TrackArtists (TrackID, ArtistID, Role, CreatedAt)
                    VALUES (@TrackID, @ArtistID, @Role, SYSUTCDATETIME())", conn, transaction);

                cmd.Parameters.AddWithValue("@TrackID", trackId);
                cmd.Parameters.AddWithValue("@ArtistID", artistId);
                cmd.Parameters.AddWithValue("@Role", role);

                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Silently ignore duplicate or constraint errors
            }
        }
    }

    // =====================================================
    // In-Memory Stats Tracking
    // =====================================================
    public class BulkUploadStats
    {
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int FailedRows { get; set; }
        public int ValidationErrors { get; set; }
        public string Status { get; set; } = "Pending";
        public List<RowValidationError> ValidationErrorDetails { get; set; } = new();
        public List<RowDatabaseError> DatabaseErrorDetails { get; set; } = new();
    }

    // =====================================================
    // Validation Result
    // =====================================================
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // =====================================================
    // Row Validation Error
    // =====================================================
    public class RowValidationError
    {
        public int RowNumber { get; set; }
        public string ReleaseTitle { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }

    // =====================================================
    // Row Database Error
    // =====================================================
    public class RowDatabaseError
    {
        public int RowNumber { get; set; }
        public string ReleaseTitle { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? InnerErrorMessage { get; set; }
        public int? SqlErrorNumber { get; set; }
    }
}
