using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/audit")]
    [Authorize(Policy = "SuperAdmin")]
    [Tags("Section 20 - Audit")]
    public class AuditController : ControllerBase
    {
        private readonly string _connStr;

        public AuditController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs(
            [FromQuery] string? entityType = null,
            [FromQuery] string? entityId = null,
            [FromQuery] string? action = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var offset = (page - 1) * pageSize;
                var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
                var toDate = to ?? DateTime.UtcNow;

                var sql = @"
                    SELECT AuditId, ActorUserId, ActorType, Action, TargetType, TargetId, DetailsJson, IpAddress, CreatedAt
                    FROM AuditLogs
                    WHERE CreatedAt BETWEEN @From AND @To";
                if (!string.IsNullOrEmpty(entityType))
                    sql += " AND TargetType = @EntityType";
                if (!string.IsNullOrEmpty(entityId))
                    sql += " AND TargetId = @EntityId";
                if (!string.IsNullOrEmpty(action))
                    sql += " AND Action = @Action";
                sql += " ORDER BY CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@From", fromDate);
                cmd.Parameters.AddWithValue("@To", toDate);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                if (!string.IsNullOrEmpty(entityType))
                    cmd.Parameters.AddWithValue("@EntityType", entityType);
                if (!string.IsNullOrEmpty(entityId))
                    cmd.Parameters.AddWithValue("@EntityId", entityId);
                if (!string.IsNullOrEmpty(action))
                    cmd.Parameters.AddWithValue("@Action", action);

                await using var reader = await cmd.ExecuteReaderAsync();
                var logs = new List<object>();
                while (await reader.ReadAsync())
                {
                    logs.Add(new
                    {
                        auditId = reader["AuditId"],
                        actorUserId = reader["ActorUserId"] == DBNull.Value ? null : reader["ActorUserId"],
                        actorType = reader["ActorType"] == DBNull.Value ? null : reader["ActorType"],
                        action = reader["Action"],
                        targetType = reader["TargetType"] == DBNull.Value ? null : reader["TargetType"],
                        targetId = reader["TargetId"] == DBNull.Value ? null : reader["TargetId"],
                        detailsJson = reader["DetailsJson"] == DBNull.Value ? null : reader["DetailsJson"],
                        ipAddress = reader["IpAddress"] == DBNull.Value ? null : reader["IpAddress"],
                        createdAt = reader["CreatedAt"]
                    });
                }

                await using var countCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM AuditLogs
                    WHERE CreatedAt BETWEEN @From AND @To" +
                    (!string.IsNullOrEmpty(entityType) ? " AND TargetType = @EntityType" : "") +
                    (!string.IsNullOrEmpty(entityId) ? " AND TargetId = @EntityId" : "") +
                    (!string.IsNullOrEmpty(action) ? " AND Action = @Action" : ""), conn);
                countCmd.Parameters.AddWithValue("@From", fromDate);
                countCmd.Parameters.AddWithValue("@To", toDate);
                if (!string.IsNullOrEmpty(entityType))
                    countCmd.Parameters.AddWithValue("@EntityType", entityType);
                if (!string.IsNullOrEmpty(entityId))
                    countCmd.Parameters.AddWithValue("@EntityId", entityId);
                if (!string.IsNullOrEmpty(action))
                    countCmd.Parameters.AddWithValue("@Action", action);
                var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

                return Ok(new { page, pageSize, total, logs });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

