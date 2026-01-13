using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers;

    [ApiController]
    [Route("api/qc")]
    [Authorize]
    [Tags("Section 9 - QC (Quality Control)")]
public class QcController : ControllerBase
{
    private readonly string _connStr;

    public QcController(IConfiguration cfg)
    {
        _connStr = cfg.GetConnectionString("DefaultConnection")!;
    }

    /// <summary>
    /// Section 9.1 - Get consolidated QC status for a release.
    /// </summary>
    [HttpGet("status/{releaseId:int}")]
    public async Task<IActionResult> GetStatus(int releaseId)
    {
        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            string? qcStatus = null;

            // Get current QCStatusId from Releases
            await using (var statusCmd = new SqlCommand(
                             "SELECT QCStatusId FROM Releases WHERE ReleaseId = @ReleaseId;",
                             conn))
            {
                statusCmd.Parameters.AddWithValue("@ReleaseId", releaseId);
                var statusObj = await statusCmd.ExecuteScalarAsync();
                if (statusObj == null)
                    return NotFound(new { error = "Release not found" });

                qcStatus = statusObj == DBNull.Value ? null : statusObj.ToString();
            }

            // Get latest QC history entry (any level)
            await using var historyCmd = new SqlCommand(@"
                SELECT TOP 1 QcId, Level, Result, Score, PayloadJson, Notes, CreatedByUserId, CreatedBy, CreatedAt
                FROM QCHistory
                WHERE ReleaseId = @ReleaseId
                ORDER BY CreatedAt DESC;", conn);
            historyCmd.Parameters.AddWithValue("@ReleaseId", releaseId);

            await using var reader = await historyCmd.ExecuteReaderAsync();
            object? latest = null;
            if (await reader.ReadAsync())
            {
                latest = new
                {
                    qcId = reader["QcId"],
                    level = reader["Level"]?.ToString(),
                    result = reader["Result"]?.ToString(),
                    score = reader["Score"] == DBNull.Value ? null : reader["Score"],
                    payloadJson = reader["PayloadJson"] == DBNull.Value ? null : reader["PayloadJson"],
                    notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"],
                    createdByUserId = reader["CreatedByUserId"] == DBNull.Value ? null : reader["CreatedByUserId"],
                    createdBy = reader["CreatedBy"]?.ToString(),
                    createdAt = reader["CreatedAt"]
                };
            }

            return Ok(new
            {
                releaseId,
                qcStatus = qcStatus ?? "PENDING",
                latest,
                nextStep = qcStatus switch
                {
                    "LABEL_PENDING" => "Label review required",
                    "LABEL_APPROVED" => "Enterprise review required",
                    "ENTERPRISE_PENDING" => "Enterprise review required",
                    "ENTERPRISE_APPROVED" => "Tunewave review required",
                    "TUNEWAVE_PENDING" => "Tunewave review required",
                    "APPROVED_FINAL" => "Approved",
                    "LABEL_REJECTED" => "Rejected at label level",
                    "ENTERPRISE_REJECTED" => "Rejected at enterprise level",
                    "REJECTED_FINAL" => "Rejected at Tunewave (final)",
                    _ => "AI QC or submission pending"
                }
            });
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

    /// <summary>
    /// Label approves QC (first level).
    /// </summary>
    [HttpPost("label/{releaseId:int}/approve")]
    public async Task<IActionResult> LabelApprove(int releaseId, [FromBody] QcActionDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_LabelApprove", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@ReviewerUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", dto.Notes ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Label QC approved; queued for Enterprise review"
            });
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

    /// <summary>
    /// Label rejects QC (first level).
    /// </summary>
    [HttpPost("label/{releaseId:int}/reject")]
    public async Task<IActionResult> LabelReject(int releaseId, [FromBody] QcActionDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_LabelReject", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@ReviewerUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", dto.Notes ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Label QC rejected"
            });
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

    /// <summary>
    /// Section 9.3 - Enterprise approves QC (second level).
    /// </summary>
    [HttpPost("enterprise/{releaseId:int}/approve")]
    public async Task<IActionResult> EnterpriseApprove(int releaseId, [FromBody] QcActionDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_EnterpriseApprove", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@ReviewerUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", dto.Notes ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Enterprise QC approved; queued for Tunewave review"
            });
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

    /// <summary>
    /// Section 9.3 - Enterprise rejects QC (second level).
    /// </summary>
    [HttpPost("enterprise/{releaseId:int}/reject")]
    public async Task<IActionResult> EnterpriseReject(int releaseId, [FromBody] QcActionDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_EnterpriseReject", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@ReviewerUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", dto.Notes ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Enterprise QC rejected"
            });
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

    /// <summary>
    /// Section 9.4 - Tunewave approves QC.
    /// </summary>
    [HttpPost("tunewave/{releaseId:int}/approve")]
    public async Task<IActionResult> TunewaveApprove(int releaseId, [FromBody] QcActionDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_TunewaveApprove", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@ReviewerUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", dto.Notes ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Final QC approved. Delivery scheduled."
            });
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

    /// <summary>
    /// Section 9.4 - Tunewave rejects QC.
    /// </summary>
    [HttpPost("tunewave/{releaseId:int}/reject")]
    public async Task<IActionResult> TunewaveReject(int releaseId, [FromBody] QcActionDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_TunewaveReject", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@ReviewerUserId", userId);
            cmd.Parameters.AddWithValue("@Notes", dto.Notes ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                success = true,
                message = "Final QC rejected."
            });
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

    /// <summary>
    /// Section 9.5 - Enterprise QC queue for current user's enterprise(s).
    /// </summary>
    [HttpGet("queue/enterprise")]
    public async Task<IActionResult> GetEnterpriseQueue()
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role);

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Determine enterprises for this user
            var enterpriseIds = new List<int>();
            bool isSuperAdmin = string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (isSuperAdmin)
            {
                // SuperAdmin sees all enterprises
                await using (var entCmd = new SqlCommand(@"
                    SELECT EnterpriseID
                    FROM Enterprises
                    WHERE IsDeleted = 0 OR IsDeleted IS NULL;", conn))
                {
                    await using var entReader = await entCmd.ExecuteReaderAsync();
                    while (await entReader.ReadAsync())
                    {
                        enterpriseIds.Add(entReader.GetInt32(0));
                    }
                }
            }
            else
            {
                // Regular users see only their enterprises
                await using (var entCmd = new SqlCommand(@"
                    SELECT EnterpriseID
                    FROM EnterpriseUserRoles
                    WHERE UserID = @UserId;", conn))
                {
                    entCmd.Parameters.AddWithValue("@UserId", userId);
                    await using var entReader = await entCmd.ExecuteReaderAsync();
                    while (await entReader.ReadAsync())
                    {
                        enterpriseIds.Add(entReader.GetInt32(0));
                    }
                }
            }

            if (!enterpriseIds.Any())
            {
                return Ok(new { enterpriseId = (int?)null, queue = Array.Empty<object>(), count = 0 });
            }

            // For simplicity use the first enterpriseId
            var enterpriseId = enterpriseIds.First();

            await using var cmd = new SqlCommand("sp_QC_GetEnterpriseQueue", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);

            await using var reader = await cmd.ExecuteReaderAsync();
            var queue = new List<object>();
            while (await reader.ReadAsync())
            {
                queue.Add(new
                {
                    releaseId = reader["ReleaseId"],
                    title = reader["Title"],
                    submittedAt = reader["SubmittedAt"],
                    aiScore = reader["AiScore"] == DBNull.Value ? null : reader["AiScore"],
                    flagsSummary = reader["FlagsSummary"] == DBNull.Value ? null : reader["FlagsSummary"]
                });
            }

            return Ok(new
            {
                enterpriseId,
                queue,
                count = queue.Count
            });
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

    /// <summary>
    /// Label-level QC queue for current user's labels.
    /// Similar to enterprise queue but scoped to labels the user has roles on.
    /// </summary>
    [HttpGet("queue/label")]
    public async Task<IActionResult> GetLabelQueue()
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role);

            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Determine labels for this user
            var labelIds = new List<int>();
            bool isSuperAdmin = string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (isSuperAdmin)
            {
                // SuperAdmin sees all labels
                await using (var lblCmd = new SqlCommand(@"
                    SELECT LabelID
                    FROM Labels
                    WHERE IsDeleted = 0 OR IsDeleted IS NULL;", conn))
                {
                    await using var lblReader = await lblCmd.ExecuteReaderAsync();
                    while (await lblReader.ReadAsync())
                    {
                        labelIds.Add(lblReader.GetInt32(0));
                    }
                }
            }
            else
            {
                // Regular users see only their labels
                await using (var lblCmd = new SqlCommand(@"
                    SELECT LabelID
                    FROM UserLabelRoles
                    WHERE UserID = @UserId;", conn))
                {
                    lblCmd.Parameters.AddWithValue("@UserId", userId);
                    await using var lblReader = await lblCmd.ExecuteReaderAsync();
                    while (await lblReader.ReadAsync())
                    {
                        labelIds.Add(lblReader.GetInt32(0));
                    }
                }
            }

            if (!labelIds.Any())
            {
                return Ok(new { labelId = (int?)null, queue = Array.Empty<object>(), count = 0 });
            }

            // For simplicity use the first labelId
            var labelId = labelIds.First();

            await using var cmd = new SqlCommand("sp_QC_GetLabelQueue", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@LabelId", labelId);

            await using var reader = await cmd.ExecuteReaderAsync();
            var queue = new List<object>();
            while (await reader.ReadAsync())
            {
                var releaseIdObj = reader["ReleaseId"];
                var titleObj = reader["Title"];
                var coverArtUrlObj = reader["CoverArtUrl"];
                var submittedAtObj = reader["SubmittedAt"];
                var aiScoreObj = reader["AiScore"];
                var flagsSummaryObj = reader["FlagsSummary"];
                var hasAudioObj = reader["HasAudio"];

                queue.Add(new
                {
                    releaseId = releaseIdObj,
                    title = titleObj,
                    coverArtUrl = coverArtUrlObj == DBNull.Value ? null : coverArtUrlObj,
                    submittedAt = submittedAtObj,
                    aiScore = aiScoreObj == DBNull.Value ? null : aiScoreObj,
                    flagsSummary = flagsSummaryObj == DBNull.Value ? null : flagsSummaryObj,
                    hasAudio = hasAudioObj != DBNull.Value && Convert.ToBoolean(hasAudioObj)
                });
            }

            return Ok(new
            {
                labelId,
                queue,
                count = queue.Count
            });
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

    /// <summary>
    /// Section 9.6 - Global Tunewave QC queue.
    /// </summary>
    [HttpGet("queue/tunewave")]
    public async Task<IActionResult> GetTunewaveQueue()
    {
        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_QC_GetTunewaveQueue", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            await using var reader = await cmd.ExecuteReaderAsync();
            var queue = new List<object>();
            while (await reader.ReadAsync())
            {
                queue.Add(new
                {
                    releaseId = reader["ReleaseId"],
                    enterpriseId = reader["EnterpriseId"],
                    title = reader["Title"],
                    submittedAt = reader["SubmittedAt"],
                    aiScore = reader["AiScore"] == DBNull.Value ? null : reader["AiScore"],
                    flagsSummary = reader["FlagsSummary"] == DBNull.Value ? null : reader["FlagsSummary"]
                });
            }

            return Ok(new
            {
                queue,
                count = queue.Count
            });
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

    /// <summary>
    /// Section 9.7 - Full QC history for a release.
    /// </summary>
    [HttpGet("history/{releaseId:int}")]
    public async Task<IActionResult> GetHistory(int releaseId)
    {
        try
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT QcId, ReleaseId, Level, Result, Score, PayloadJson, Notes,
                       CreatedByUserId, CreatedBy, CreatedAt
                FROM QCHistory
                WHERE ReleaseId = @ReleaseId
                ORDER BY CreatedAt ASC;", conn);
            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

            await using var reader = await cmd.ExecuteReaderAsync();
            var history = new List<object>();
            while (await reader.ReadAsync())
            {
                history.Add(new
                {
                    qcId = reader["QcId"],
                    level = reader["Level"]?.ToString(),
                    result = reader["Result"]?.ToString(),
                    score = reader["Score"] == DBNull.Value ? null : reader["Score"],
                    payloadJson = reader["PayloadJson"] == DBNull.Value ? null : reader["PayloadJson"],
                    notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"],
                    createdByUserId = reader["CreatedByUserId"] == DBNull.Value ? null : reader["CreatedByUserId"],
                    createdBy = reader["CreatedBy"]?.ToString(),
                    createdAt = reader["CreatedAt"]
                });
            }

            return Ok(new
            {
                releaseId,
                history
            });
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


