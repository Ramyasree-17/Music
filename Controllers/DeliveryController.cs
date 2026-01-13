using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/delivery")]
    [Authorize]
    [Tags("Section 10 - Delivery")]
    public class DeliveryController : ControllerBase
    {
        private readonly string _connStr;

        public DeliveryController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("{releaseId:int}/generate")]
        [Authorize(Policy = "SystemOrQc")]
        public async Task<IActionResult> Generate(int releaseId, [FromBody] GenerateDeliveryRequest dto)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Validate release status
                await using var checkCmd = new SqlCommand(@"
                    SELECT Status, QCStatusId FROM Releases WHERE ReleaseId = @ReleaseId", conn);
                checkCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                await using var reader = await checkCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new { error = "Release not found" });

                var qcStatus = reader["QCStatusId"]?.ToString();
                // Check if QC is approved (APPROVED_FINAL means Tunewave QC approved)
                if (qcStatus != "APPROVED_FINAL" && !dto.ForceRebuild)
                    return BadRequest(new { error = "Release must be QC_APPROVED (APPROVED_FINAL) to generate delivery packages. Current QC status: " + (qcStatus ?? "NULL") });

                await reader.CloseAsync();

                var dsps = dto.Dsps ?? new List<string> { "spotify", "apple", "youtube_music" };
                var packages = new List<object>();

                await using var tx = conn.BeginTransaction();
                try
                {
                    foreach (var dsp in dsps)
                    {
                        long packageId = 0;
                        await using var cmd = new SqlCommand(@"
                            INSERT INTO DeliveryPackages (ReleaseId, DspName, Status, CreatedAt, UpdatedAt)
                            OUTPUT INSERTED.PackageId, INSERTED.DspName, INSERTED.Status
                            VALUES (@ReleaseId, @DspName, 'IN_QUEUE', SYSUTCDATETIME(), SYSUTCDATETIME())", conn, tx);
                        cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                        cmd.Parameters.AddWithValue("@DspName", dsp);

                        await using var pkgReader = await cmd.ExecuteReaderAsync();
                        if (await pkgReader.ReadAsync())
                        {
                            packageId = pkgReader.GetInt64(0);
                            packages.Add(new
                            {
                                packageId = packageId,
                                dsp = pkgReader["DspName"],
                                status = pkgReader["Status"]
                            });
                        }
                        await pkgReader.CloseAsync();

                        // Enqueue job
                        if (packageId > 0)
                        {
                            await using var jobCmd = new SqlCommand(@"
                                INSERT INTO Jobs (JobType, PayloadJson, Status, CreatedAt, UpdatedAt)
                                VALUES ('Delivery', @Payload, 'Queued', SYSUTCDATETIME(), SYSUTCDATETIME())", conn, tx);
                            jobCmd.Parameters.AddWithValue("@Payload", $"{{\"packageId\":{packageId},\"dsp\":\"{dsp}\"}}");
                            await jobCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                return Accepted(new { message = "Delivery packages created", packages });
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

        [HttpPost("{releaseId:int}/redeliver")]
        public async Task<IActionResult> Redeliver(int releaseId, [FromBody] RedeliverRequest dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check user has access to release's label
                await using var accessCmd = new SqlCommand(@"
                    SELECT r.LabelID FROM Releases r
                    INNER JOIN UserLabelRoles ulr ON r.LabelID = ulr.LabelId
                    WHERE r.ReleaseId = @ReleaseId AND ulr.UserID = @UserId", conn);
                accessCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                accessCmd.Parameters.AddWithValue("@UserId", userId);
                var hasAccess = await accessCmd.ExecuteScalarAsync() != null;

                if (!hasAccess && !User.IsInRole("SuperAdmin"))
                    return Forbid();

                var dsps = dto.Dsps ?? new List<string>();
                if (!dsps.Any())
                {
                    await using var getDspsCmd = new SqlCommand(@"
                        SELECT DISTINCT DspName FROM DeliveryPackages WHERE ReleaseId = @ReleaseId", conn);
                    getDspsCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                    await using var reader = await getDspsCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        dsps.Add(reader["DspName"].ToString()!);
                    await reader.CloseAsync();
                }

                var redeliverPackages = new List<long>();

                await using var tx = conn.BeginTransaction();
                try
                {
                    foreach (var dsp in dsps)
                    {
                        if (dto.ForceRebuild)
                        {
                            // Cancel old packages
                            await using var cancelCmd = new SqlCommand(@"
                                UPDATE DeliveryPackages SET Status = 'CANCELLED' 
                                WHERE ReleaseId = @ReleaseId AND DspName = @DspName AND Status NOT IN ('DELIVERED', 'TAKEDOWNED')", conn, tx);
                            cancelCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            cancelCmd.Parameters.AddWithValue("@DspName", dsp);
                            await cancelCmd.ExecuteNonQueryAsync();

                            // Create new package
                            await using var newCmd = new SqlCommand(@"
                                INSERT INTO DeliveryPackages (ReleaseId, DspName, Status, CreatedAt, UpdatedAt)
                                OUTPUT INSERTED.PackageId
                                VALUES (@ReleaseId, @DspName, 'IN_QUEUE', SYSUTCDATETIME(), SYSUTCDATETIME())", conn, tx);
                            newCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            newCmd.Parameters.AddWithValue("@DspName", dsp);
                            var newPkgId = await newCmd.ExecuteScalarAsync();
                            if (newPkgId != null)
                                redeliverPackages.Add(Convert.ToInt64(newPkgId));
                        }
                        else
                        {
                            // Requeue existing packages
                            await using var requeueCmd = new SqlCommand(@"
                                UPDATE DeliveryPackages 
                                SET Status = 'IN_QUEUE', NextAttemptAt = NULL, UpdatedAt = SYSUTCDATETIME()
                                OUTPUT INSERTED.PackageId
                                WHERE ReleaseId = @ReleaseId AND DspName = @DspName AND Status IN ('DELIVERY_FAILED', 'RETRY_SCHEDULED')", conn, tx);
                            requeueCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                            requeueCmd.Parameters.AddWithValue("@DspName", dsp);
                            await using var reader = await requeueCmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                                redeliverPackages.Add(reader.GetInt64(0));
                            await reader.CloseAsync();
                        }
                    }

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                return Accepted(new { message = "Redelivery scheduled", redeliverPackages });
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

        [HttpGet("{releaseId:int}/status")]
        public async Task<IActionResult> GetStatus(int releaseId, [FromQuery] string? dsp = null)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check access
                await using var accessCmd = new SqlCommand(@"
                    SELECT r.LabelID FROM Releases r
                    LEFT JOIN UserLabelRoles ulr ON r.LabelID = ulr.LabelId AND ulr.UserID = @UserId
                    WHERE r.ReleaseId = @ReleaseId", conn);
                accessCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                accessCmd.Parameters.AddWithValue("@UserId", userId);
                var hasAccess = await accessCmd.ExecuteScalarAsync() != null;

                if (!hasAccess && !User.IsInRole("SuperAdmin"))
                    return Forbid();

                // Check if DeliveredAt column exists
                var hasDeliveredAtColumn = false;
                await using (var checkCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM sys.columns 
                    WHERE object_id = OBJECT_ID('DeliveryPackages') AND name = 'DeliveredAt'", conn))
                {
                    hasDeliveredAtColumn = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                }

                var deliveredAtColumn = hasDeliveredAtColumn ? "DeliveredAt" : "UpdatedAt";
                var sql = $@"
                    SELECT PackageId, DspName, Status, Attempts, NextAttemptAt, ExternalId, {deliveredAtColumn} AS DeliveredAt
                    FROM DeliveryPackages
                    WHERE ReleaseId = @ReleaseId";
                if (!string.IsNullOrEmpty(dsp))
                    sql += " AND DspName = @DspName";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                if (!string.IsNullOrEmpty(dsp))
                    cmd.Parameters.AddWithValue("@DspName", dsp);

                await using var reader = await cmd.ExecuteReaderAsync();
                var packages = new List<object>();
                while (await reader.ReadAsync())
                {
                    packages.Add(new
                    {
                        packageId = reader["PackageId"],
                        dsp = reader["DspName"],
                        status = reader["Status"],
                        attempts = reader["Attempts"],
                        lastAttemptAt = reader["NextAttemptAt"] == DBNull.Value ? null : reader["NextAttemptAt"],
                        externalId = reader["ExternalId"] == DBNull.Value ? null : reader["ExternalId"],
                        deliveredAt = reader["DeliveredAt"] == DBNull.Value ? null : reader["DeliveredAt"]
                    });
                }

                return Ok(new { releaseId, packages });
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

        [HttpGet("logs/{packageId:long}")]
        [Authorize(Policy = "SupportOrSuperAdmin")]
        public async Task<IActionResult> GetLogs(long packageId)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT AttemptNumber, CreatedAt, RequestJson, ResponseJson, StatusCode, Message
                    FROM DeliveryLogs
                    WHERE PackageId = @PackageId
                    ORDER BY AttemptNumber ASC", conn);
                cmd.Parameters.AddWithValue("@PackageId", packageId);

                await using var reader = await cmd.ExecuteReaderAsync();
                var logs = new List<object>();
                while (await reader.ReadAsync())
                {
                    logs.Add(new
                    {
                        attemptNumber = reader["AttemptNumber"],
                        timestamp = reader["CreatedAt"],
                        requestJson = reader["RequestJson"] == DBNull.Value ? null : reader["RequestJson"],
                        responseJson = reader["ResponseJson"] == DBNull.Value ? null : reader["ResponseJson"],
                        statusCode = reader["StatusCode"] == DBNull.Value ? null : reader["StatusCode"],
                        message = reader["Message"] == DBNull.Value ? null : reader["Message"]
                    });
                }

                return Ok(new { packageId, logs });
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

        [HttpPost("callback/{dsp}")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(string dsp)
        {
            // TODO: Implement DSP callback verification and handling
            // This should verify HMAC signature and update package status
            return Ok(new { message = "Callback received" });
        }
    }
}

