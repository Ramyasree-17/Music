using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    [Tags("Section 14 - Notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly string _connStr;

        public NotificationsController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool onlyUnread = false, [FromQuery] int limit = 20, [FromQuery] int page = 1, [FromQuery] string? channel = null)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var offset = (page - 1) * limit;
                var sql = @"
                    SELECT NotificationId, Title, Message, Channel, IsRead, CreatedAt, PayloadJson
                    FROM Notifications
                    WHERE UserId = @UserId";
                if (onlyUnread)
                    sql += " AND IsRead = 0";
                if (!string.IsNullOrEmpty(channel))
                    sql += " AND Channel = @Channel";
                sql += " ORDER BY CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@Limit", limit);
                if (!string.IsNullOrEmpty(channel))
                    cmd.Parameters.AddWithValue("@Channel", channel);

                await using var reader = await cmd.ExecuteReaderAsync();
                var notifications = new List<object>();
                while (await reader.ReadAsync())
                {
                    notifications.Add(new
                    {
                        notificationId = reader["NotificationId"],
                        title = reader["Title"],
                        message = reader["Message"],
                        channel = reader["Channel"],
                        isRead = reader["IsRead"],
                        createdAt = reader["CreatedAt"],
                        payload = reader["PayloadJson"] == DBNull.Value ? null : reader["PayloadJson"]
                    });
                }

                await using var countCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM Notifications WHERE UserId = @UserId" + (onlyUnread ? " AND IsRead = 0" : ""), conn);
                countCmd.Parameters.AddWithValue("@UserId", userId);
                var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

                return Ok(new { page, pageSize = limit, total, notifications });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("{id:long}/read")]
        public async Task<IActionResult> MarkRead(long id)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    UPDATE Notifications 
                    SET IsRead = 1, ReadAt = SYSUTCDATETIME()
                    WHERE NotificationId = @Id AND UserId = @UserId", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@UserId", userId);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound();

                return Ok(new { success = true, notificationId = id, isRead = true });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("send-test")]
        [Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> SendTest([FromBody] SendTestNotificationDto dto)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    INSERT INTO Notifications (UserId, Title, Message, Channel, PayloadJson, TemplateKey, Status, CreatedAt)
                    OUTPUT INSERTED.NotificationId
                    VALUES (@UserId, @Title, @Message, @Channel, @Payload, @TemplateKey, 'PENDING', SYSUTCDATETIME())", conn);
                cmd.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd.Parameters.AddWithValue("@Title", dto.Title);
                cmd.Parameters.AddWithValue("@Message", dto.Message);
                cmd.Parameters.AddWithValue("@Channel", dto.Channel);
                cmd.Parameters.AddWithValue("@Payload", dto.Payload != null ? System.Text.Json.JsonSerializer.Serialize(dto.Payload) : DBNull.Value);
                cmd.Parameters.AddWithValue("@TemplateKey", (object?)dto.TemplateKey ?? DBNull.Value);

                var notificationId = await cmd.ExecuteScalarAsync();

                // Enqueue job
                await using var jobCmd = new SqlCommand(@"
                    INSERT INTO NotificationJobs (NotificationId, Channel, Status, CreatedAt)
                    VALUES (@NotificationId, @Channel, 'Queued', SYSUTCDATETIME())", conn);
                jobCmd.Parameters.AddWithValue("@NotificationId", notificationId);
                jobCmd.Parameters.AddWithValue("@Channel", dto.Channel);
                await jobCmd.ExecuteNonQueryAsync();

                return Accepted(new { message = "Notification queued", notificationId = Convert.ToInt64(notificationId) });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("send")]
        [Authorize(Policy = "SystemOrQc")]
        public async Task<IActionResult> Send([FromBody] SendNotificationDto dto)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var notificationIds = new List<long>();

                foreach (var recipientId in dto.Recipients)
                {
                    foreach (var channel in dto.Channels)
                    {
                        // Check preferences
                        await using var prefCmd = new SqlCommand(@"
                            SELECT Enabled FROM NotificationPreferences
                            WHERE UserId = @UserId AND Channel = @Channel AND (Topic IS NULL OR Topic = @Topic)", conn);
                        prefCmd.Parameters.AddWithValue("@UserId", recipientId);
                        prefCmd.Parameters.AddWithValue("@Channel", channel);
                        prefCmd.Parameters.AddWithValue("@Topic", (object?)dto.TemplateKey ?? DBNull.Value);
                        var enabled = await prefCmd.ExecuteScalarAsync();
                        if (enabled != null && Convert.ToBoolean(enabled) == false)
                            continue;

                        // Create notification
                        await using var notifCmd = new SqlCommand(@"
                            INSERT INTO Notifications (UserId, TenantType, TenantId, Title, Message, Channel, TemplateKey, PayloadJson, ClientReference, Status, CreatedAt)
                            OUTPUT INSERTED.NotificationId
                            VALUES (@UserId, @TenantType, @TenantId, @Title, @Message, @Channel, @TemplateKey, @Payload, @ClientRef, 'PENDING', SYSUTCDATETIME())", conn);
                        notifCmd.Parameters.AddWithValue("@UserId", recipientId);
                        notifCmd.Parameters.AddWithValue("@TenantType", (object?)dto.TenantType ?? DBNull.Value);
                        notifCmd.Parameters.AddWithValue("@TenantId", (object?)dto.TenantId ?? DBNull.Value);
                        notifCmd.Parameters.AddWithValue("@Title", ""); // TODO: Render from template
                        notifCmd.Parameters.AddWithValue("@Message", ""); // TODO: Render from template
                        notifCmd.Parameters.AddWithValue("@Channel", channel);
                        notifCmd.Parameters.AddWithValue("@TemplateKey", dto.TemplateKey);
                        notifCmd.Parameters.AddWithValue("@Payload", dto.TemplateData != null ? System.Text.Json.JsonSerializer.Serialize(dto.TemplateData) : DBNull.Value);
                        notifCmd.Parameters.AddWithValue("@ClientRef", (object?)dto.ClientReference ?? DBNull.Value);

                        var notifId = await notifCmd.ExecuteScalarAsync();
                        if (notifId != null)
                        {
                            notificationIds.Add(Convert.ToInt64(notifId));

                            // Enqueue job
                            await using var jobCmd = new SqlCommand(@"
                                INSERT INTO NotificationJobs (NotificationId, Channel, Status, CreatedAt)
                                VALUES (@NotificationId, @Channel, 'Queued', SYSUTCDATETIME())", conn);
                            jobCmd.Parameters.AddWithValue("@NotificationId", notifId);
                            jobCmd.Parameters.AddWithValue("@Channel", channel);
                            await jobCmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                return Accepted(new { queued = notificationIds.Count, notificationIds });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

