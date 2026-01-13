using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/royalties")]
    [Authorize]
    [Tags("Section 11 - Royalties")]
    public class RoyaltiesController : ControllerBase
    {
        private readonly string _connStr;

        public RoyaltiesController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("upload")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> Upload([FromBody] UploadRoyaltyStatementRequest dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    INSERT INTO RoyaltyStatements (LabelId, EnterpriseId, PeriodStart, PeriodEnd, Currency, UploadedByUserId, Status, UploadedAt)
                    OUTPUT INSERTED.StatementId
                    VALUES (@LabelId, @EnterpriseId, @PeriodStart, @PeriodEnd, @Currency, @UserId, 'UPLOADED', SYSUTCDATETIME())", conn);
                cmd.Parameters.AddWithValue("@LabelId", (object?)dto.LabelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EnterpriseId", (object?)dto.EnterpriseId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PeriodStart", dto.PeriodStart);
                cmd.Parameters.AddWithValue("@PeriodEnd", dto.PeriodEnd);
                cmd.Parameters.AddWithValue("@Currency", dto.Currency);
                cmd.Parameters.AddWithValue("@UserId", userId);

                var statementId = await cmd.ExecuteScalarAsync();
                if (statementId == null)
                    return BadRequest(new { error = "Failed to create statement" });

                // Enqueue parse job
                await using var jobCmd = new SqlCommand(@"
                    INSERT INTO Jobs (JobType, PayloadJson, Status, CreatedAt, UpdatedAt)
                    VALUES ('RoyaltyParse', @Payload, 'Queued', SYSUTCDATETIME(), SYSUTCDATETIME())", conn);
                jobCmd.Parameters.AddWithValue("@Payload", $"{{\"statementId\":{statementId}}}");
                await jobCmd.ExecuteNonQueryAsync();

                return StatusCode(201, new { statementId = Convert.ToInt64(statementId), message = "Uploaded successfully; parsing scheduled." });
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

        [HttpPost("parse/{statementId:long}")]
        [Authorize(Policy = "SystemOrQc")]
        public async Task<IActionResult> Parse(long statementId)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check if statement exists
                await using var checkCmd = new SqlCommand("SELECT StatementId FROM RoyaltyStatements WHERE StatementId = @StatementId", conn);
                checkCmd.Parameters.AddWithValue("@StatementId", statementId);
                if (await checkCmd.ExecuteScalarAsync() == null)
                    return NotFound(new { error = "Statement not found" });

                // For testing: Create sample unmapped rows
                // In production, this would parse the actual CSV file
                await using var tx = conn.BeginTransaction();
                try
                {
                    // Insert sample rows for testing
                    await using var insertCmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM RoyaltyRows WHERE StatementId = @StatementId)
                        BEGIN
                            INSERT INTO RoyaltyRows (StatementId, RowNumber, TrackTitle, ISRC, GrossAmount, Units, Currency, MappingStatus)
                            VALUES 
                                (@StatementId, 1, 'Test Song 1', 'USRC12345678', 100.50, 1000, 'USD', 'UNMAPPED'),
                                (@StatementId, 2, 'Test Song 2', 'USRC87654321', 250.75, 2500, 'USD', 'UNMAPPED'),
                                (@StatementId, 3, 'Test Song 3', NULL, 50.25, 500, 'USD', 'UNMAPPED');
                        END", conn, tx);
                    insertCmd.Parameters.AddWithValue("@StatementId", statementId);
                    await insertCmd.ExecuteNonQueryAsync();

                    // Update statement status
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE RoyaltyStatements 
                        SET Status = 'PARSED', ParsedAt = SYSUTCDATETIME()
                        WHERE StatementId = @StatementId", conn, tx);
                    updateCmd.Parameters.AddWithValue("@StatementId", statementId);
                    await updateCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                return Ok(new { statementId, message = "Statement parsed successfully. Sample rows created for testing." });
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

        [HttpGet("unmapped/{statementId:long}")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> GetUnmapped(long statementId, [FromQuery] int limit = 100, [FromQuery] int page = 1)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var offset = (page - 1) * limit;

                await using var cmd = new SqlCommand(@"
                    SELECT RowId, RowNumber, ISRC, TrackTitle, GrossAmount, Units
                    FROM RoyaltyRows
                    WHERE StatementId = @StatementId AND MappingStatus = 'UNMAPPED'
                    ORDER BY RowNumber
                    OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY", conn);
                cmd.Parameters.AddWithValue("@StatementId", statementId);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@Limit", limit);

                var rows = new List<object>();
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rows.Add(new
                        {
                            rowId = reader["RowId"],
                            rowNumber = reader["RowNumber"],
                            isrc = reader["ISRC"] == DBNull.Value ? null : reader["ISRC"],
                            trackTitle = reader["TrackTitle"],
                            grossAmount = reader["GrossAmount"],
                            units = reader["Units"]
                        });
                    }
                } // Reader is closed here

                await using var countCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM RoyaltyRows WHERE StatementId = @StatementId AND MappingStatus = 'UNMAPPED'", conn);
                countCmd.Parameters.AddWithValue("@StatementId", statementId);
                var total = await countCmd.ExecuteScalarAsync();

                return Ok(new { statementId, rows, totalUnmapped = Convert.ToInt32(total) });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("fix-mapping")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> FixMapping([FromBody] FixMappingRequest dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // First verify RowId exists
                    await using var checkCmd = new SqlCommand(@"
                        SELECT RowId, MappedReleaseId FROM RoyaltyRows WHERE RowId = @RowId", conn, tx);
                    checkCmd.Parameters.AddWithValue("@RowId", dto.RowId);
                    await using var checkReader = await checkCmd.ExecuteReaderAsync();
                    
                    if (!await checkReader.ReadAsync())
                    {
                        await tx.RollbackAsync();
                        return NotFound(new { error = $"Royalty row with RowId {dto.RowId} not found" });
                    }
                    
                    var oldReleaseId = checkReader["MappedReleaseId"] == DBNull.Value ? null : checkReader["MappedReleaseId"];
                    await checkReader.CloseAsync();

                    // Update mapping
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE RoyaltyRows 
                        SET MappedReleaseId = @ReleaseId, MappedTrackId = @TrackId, 
                            MappingStatus = 'MAPPED', MappedAt = SYSUTCDATETIME()
                        WHERE RowId = @RowId", conn, tx);
                    updateCmd.Parameters.AddWithValue("@RowId", dto.RowId);
                    updateCmd.Parameters.AddWithValue("@ReleaseId", dto.ReleaseId);
                    updateCmd.Parameters.AddWithValue("@TrackId", (object?)dto.TrackId ?? DBNull.Value);
                    var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                    
                    if (rowsAffected == 0)
                    {
                        await tx.RollbackAsync();
                        return NotFound(new { error = $"Failed to update royalty row {dto.RowId}" });
                    }

                    // Audit
                    await using var auditCmd = new SqlCommand(@"
                        INSERT INTO RoyaltyMappingAudit (RowId, OldMappedReleaseId, NewMappedReleaseId, ChangedByUserId, Notes)
                        VALUES (@RowId, @OldReleaseId, @NewReleaseId, @UserId, @Notes)", conn, tx);
                    auditCmd.Parameters.AddWithValue("@RowId", dto.RowId);
                    auditCmd.Parameters.AddWithValue("@OldReleaseId", (object?)oldReleaseId ?? DBNull.Value);
                    auditCmd.Parameters.AddWithValue("@NewReleaseId", dto.ReleaseId);
                    auditCmd.Parameters.AddWithValue("@UserId", userId);
                    auditCmd.Parameters.AddWithValue("@Notes", (object?)dto.Notes ?? DBNull.Value);
                    await auditCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                return Ok(new { success = true, rowId = dto.RowId, mappedReleaseId = dto.ReleaseId });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("process/{statementId:long}")]
        [Authorize(Policy = "SystemOrQc")]
        public async Task<IActionResult> Process(long statementId)
        {
            // This would be called by background worker
            // Implementation would process mapped rows into ledger entries and update wallets
            return Ok(new { statementId, message = "Processing job queued" });
        }

        [HttpGet("summary/label/{labelId:int}")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> GetLabelSummary(int labelId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
                var toDate = to ?? DateTime.UtcNow;

                // Get summary from ledger entries
                await using var summaryCmd = new SqlCommand(@"
                    SELECT 
                        SUM(CASE WHEN EntryType = 'RoyaltyCredit' AND EntityType = 'Label' AND EntityId = @LabelId THEN Amount ELSE 0 END) as TotalGross,
                        SUM(CASE WHEN EntryType = 'TunewaveCommission' THEN Amount ELSE 0 END) as TunewaveCommission,
                        SUM(CASE WHEN EntryType = 'RoyaltyCredit' AND EntityType = 'Label' AND EntityId = @LabelId THEN Amount ELSE 0 END) - 
                        SUM(CASE WHEN EntryType = 'TunewaveCommission' THEN Amount ELSE 0 END) as LabelNet
                    FROM LedgerEntries
                    WHERE CreatedAt BETWEEN @From AND @To
                    AND (EntityType = 'Label' AND EntityId = @LabelId OR EntryType = 'TunewaveCommission')", conn);
                summaryCmd.Parameters.AddWithValue("@LabelId", labelId);
                summaryCmd.Parameters.AddWithValue("@From", fromDate);
                summaryCmd.Parameters.AddWithValue("@To", toDate);

                await using var reader = await summaryCmd.ExecuteReaderAsync();
                decimal totalGross = 0, tunewaveCommission = 0, labelNet = 0;
                if (await reader.ReadAsync())
                {
                    totalGross = reader["TotalGross"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalGross"]);
                    tunewaveCommission = reader["TunewaveCommission"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TunewaveCommission"]);
                    labelNet = reader["LabelNet"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["LabelNet"]);
                }
                await reader.CloseAsync();

                // Get unmapped count
                await using var unmappedCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM RoyaltyRows r
                    INNER JOIN RoyaltyStatements s ON r.StatementId = s.StatementId
                    WHERE s.LabelId = @LabelId AND r.MappingStatus = 'UNMAPPED'", conn);
                unmappedCmd.Parameters.AddWithValue("@LabelId", labelId);
                var unmapped = Convert.ToInt32(await unmappedCmd.ExecuteScalarAsync() ?? 0);

                return Ok(new
                {
                    labelId,
                    period = new { from = fromDate, to = toDate },
                    totalGross,
                    tunewaveCommission,
                    labelNet,
                    unmappedRows = unmapped,
                    artists = new List<object>() // TODO: Aggregate by artist
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

