using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/jobs")]
    [Authorize(Policy = "WorkerOrAdmin")]
    [Tags("Section 18 - Jobs")]
    public class JobsController : ControllerBase
    {
        private readonly string _connStr;

        public JobsController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue([FromQuery] string? jobType = null, [FromQuery] int limit = 50, [FromQuery] int? priority = null, [FromQuery] bool onlyDue = true)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var sql = @"
                    SELECT TOP (@Limit) JobId, JobType, Status, Attempts, NextRunAt, Priority, CreatedAt
                    FROM Jobs
                    WHERE Status IN ('Queued', 'RetryScheduled')";
                if (onlyDue)
                    sql += " AND (NextRunAt IS NULL OR NextRunAt <= SYSUTCDATETIME())";
                if (!string.IsNullOrEmpty(jobType))
                    sql += " AND JobType = @JobType";
                if (priority.HasValue)
                    sql += " AND Priority = @Priority";
                sql += " ORDER BY Priority ASC, CreatedAt ASC";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                if (!string.IsNullOrEmpty(jobType))
                    cmd.Parameters.AddWithValue("@JobType", jobType);
                if (priority.HasValue)
                    cmd.Parameters.AddWithValue("@Priority", priority.Value);

                await using var reader = await cmd.ExecuteReaderAsync();
                var jobs = new List<JobDto>();
                while (await reader.ReadAsync())
                {
                    jobs.Add(new JobDto
                    {
                        JobId = reader.GetInt64(reader.GetOrdinal("JobId")),
                        JobType = reader["JobType"].ToString()!,
                        Status = reader["Status"].ToString()!,
                        Attempts = reader.GetInt32(reader.GetOrdinal("Attempts")),
                        NextRunAt = reader["NextRunAt"] == DBNull.Value ? null : reader.GetDateTime(reader.GetOrdinal("NextRunAt")),
                        Priority = reader.GetByte(reader.GetOrdinal("Priority")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                    });
                }

                return Ok(new JobQueueResponse { Count = jobs.Count, Jobs = jobs });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("status/{jobId:long}")]
        public async Task<IActionResult> GetStatus(long jobId)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT JobId, JobType, Status, Attempts, LockedBy, LockedAt, LastError
                    FROM Jobs
                    WHERE JobId = @JobId", conn);
                cmd.Parameters.AddWithValue("@JobId", jobId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound();

                var job = new
                {
                    jobId = reader["JobId"],
                    jobType = reader["JobType"],
                    status = reader["Status"],
                    attempts = reader["Attempts"],
                    lockedBy = reader["LockedBy"] == DBNull.Value ? null : reader["LockedBy"],
                    lockedAt = reader["LockedAt"] == DBNull.Value ? null : reader["LockedAt"],
                    lastError = reader["LastError"] == DBNull.Value ? null : reader["LastError"]
                };
                await reader.CloseAsync();

                // Get attempts
                await using var attemptsCmd = new SqlCommand(@"
                    SELECT AttemptNumber, WorkerId, Success, CreatedAt
                    FROM JobAttempts
                    WHERE JobId = @JobId
                    ORDER BY AttemptNumber ASC", conn);
                attemptsCmd.Parameters.AddWithValue("@JobId", jobId);
                await using var attemptsReader = await attemptsCmd.ExecuteReaderAsync();
                var attempts = new List<object>();
                while (await attemptsReader.ReadAsync())
                {
                    attempts.Add(new
                    {
                        attemptNumber = attemptsReader["AttemptNumber"],
                        workerId = attemptsReader["WorkerId"] == DBNull.Value ? null : attemptsReader["WorkerId"],
                        success = attemptsReader["Success"],
                        startedAt = attemptsReader["CreatedAt"]
                    });
                }

                return Ok(new { job, attemptsLog = attempts });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("{jobId:long}/retry")]
        [Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> Retry(long jobId, [FromBody] RetryJobDto dto)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var sql = @"
                    UPDATE Jobs 
                    SET Status = 'Queued', NextRunAt = @NextRunAt";
                if (dto.ResetAttempts)
                    sql += ", Attempts = 0";
                sql += " WHERE JobId = @JobId";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@JobId", jobId);
                cmd.Parameters.AddWithValue("@NextRunAt", (object?)dto.NextRunAt ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound();

                return Accepted(new { message = "Job requeued", jobId });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

