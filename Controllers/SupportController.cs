using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/support")]
    [Authorize]
    [Tags("Section 15 - Support")]
    public class SupportController : ControllerBase
    {
        private readonly string _connStr;

        public SupportController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("ticket")]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Create ticket
                    await using var ticketCmd = new SqlCommand(@"
                        INSERT INTO SupportTickets (TenantType, TenantId, Category, Subject, Description, CreatedByUserId, Status, Priority, CreatedAt, LastActivityAt)
                        OUTPUT INSERTED.TicketId
                        VALUES (@TenantType, @TenantId, @Category, @Subject, @Description, @UserId, 'OPEN', @Priority, SYSUTCDATETIME(), SYSUTCDATETIME())", conn, tx);
                    ticketCmd.Parameters.AddWithValue("@TenantType", (object?)dto.TenantType ?? DBNull.Value);
                    ticketCmd.Parameters.AddWithValue("@TenantId", (object?)dto.TenantId ?? DBNull.Value);
                    ticketCmd.Parameters.AddWithValue("@Category", dto.Category);
                    ticketCmd.Parameters.AddWithValue("@Subject", dto.Subject);
                    ticketCmd.Parameters.AddWithValue("@Description", dto.Description);
                    ticketCmd.Parameters.AddWithValue("@UserId", userId);
                    ticketCmd.Parameters.AddWithValue("@Priority", dto.Priority);

                    var ticketId = await ticketCmd.ExecuteScalarAsync();
                    if (ticketId == null)
                        return BadRequest(new { error = "Failed to create ticket" });

                    // Create initial message
                    await using var msgCmd = new SqlCommand(@"
                        INSERT INTO TicketMessages (TicketId, SenderUserId, Body, AttachmentsJson, CreatedAt)
                        VALUES (@TicketId, @UserId, @Description, @Attachments, SYSUTCDATETIME())", conn, tx);
                    msgCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    msgCmd.Parameters.AddWithValue("@UserId", userId);
                    msgCmd.Parameters.AddWithValue("@Description", dto.Description);
                    msgCmd.Parameters.AddWithValue("@Attachments", dto.Attachments != null ? System.Text.Json.JsonSerializer.Serialize(dto.Attachments.Select(a => new { fileId = a })) : DBNull.Value);
                    await msgCmd.ExecuteNonQueryAsync();

                    // Add participants
                    await using var partCmd = new SqlCommand(@"
                        INSERT INTO TicketParticipants (TicketId, UserId, Role, AddedAt)
                        VALUES (@TicketId, @UserId, 'Creator', SYSUTCDATETIME())", conn, tx);
                    partCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    partCmd.Parameters.AddWithValue("@UserId", userId);
                    await partCmd.ExecuteNonQueryAsync();

                    if (dto.CcUserIds != null)
                    {
                        foreach (var ccId in dto.CcUserIds)
                        {
                            partCmd.Parameters.Clear();
                            partCmd.Parameters.AddWithValue("@TicketId", ticketId);
                            partCmd.Parameters.AddWithValue("@UserId", ccId);
                            partCmd.CommandText = @"
                                INSERT INTO TicketParticipants (TicketId, UserId, Role, AddedAt)
                                VALUES (@TicketId, @UserId, 'CC', SYSUTCDATETIME())";
                            await partCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Create log
                    await using var logCmd = new SqlCommand(@"
                        INSERT INTO TicketLogs (TicketId, Action, ActorUserId, Details, CreatedAt)
                        VALUES (@TicketId, 'Created', @UserId, 'Ticket created', SYSUTCDATETIME())", conn, tx);
                    logCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    logCmd.Parameters.AddWithValue("@UserId", userId);
                    await logCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return StatusCode(201, new { ticketId = Convert.ToInt64(ticketId), status = "OPEN", createdAt = DateTime.UtcNow });
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("ticket/{ticketId:long}")]
        public async Task<IActionResult> GetTicket(long ticketId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check access
                await using var accessCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM TicketParticipants WHERE TicketId = @TicketId AND UserId = @UserId", conn);
                accessCmd.Parameters.AddWithValue("@TicketId", ticketId);
                accessCmd.Parameters.AddWithValue("@UserId", userId);
                var hasAccess = Convert.ToInt32(await accessCmd.ExecuteScalarAsync() ?? 0) > 0;

                if (!hasAccess && !User.IsInRole("SuperAdmin") && !User.IsInRole("Support"))
                    return Forbid();

                // Get ticket
                await using var ticketCmd = new SqlCommand(@"
                    SELECT TicketId, Subject, Category, Priority, Status, TenantType, TenantId, CreatedByUserId, CreatedAt, AssignedToUserId
                    FROM SupportTickets
                    WHERE TicketId = @TicketId", conn);
                ticketCmd.Parameters.AddWithValue("@TicketId", ticketId);
                await using var reader = await ticketCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound();

                var ticket = new
                {
                    ticketId = reader["TicketId"],
                    subject = reader["Subject"],
                    category = reader["Category"],
                    priority = reader["Priority"],
                    status = reader["Status"],
                    tenantType = reader["TenantType"] == DBNull.Value ? null : reader["TenantType"],
                    tenantId = reader["TenantId"] == DBNull.Value ? null : reader["TenantId"],
                    createdBy = reader["CreatedByUserId"],
                    createdAt = reader["CreatedAt"],
                    assignedTo = reader["AssignedToUserId"] == DBNull.Value ? null : reader["AssignedToUserId"]
                };
                await reader.CloseAsync();

                // Get messages
                await using var msgCmd = new SqlCommand(@"
                    SELECT MessageId, SenderUserId, Body, AttachmentsJson, CreatedAt, IsInternal
                    FROM TicketMessages
                    WHERE TicketId = @TicketId
                    ORDER BY CreatedAt ASC", conn);
                msgCmd.Parameters.AddWithValue("@TicketId", ticketId);
                await using var msgReader = await msgCmd.ExecuteReaderAsync();
                var messages = new List<object>();
                while (await msgReader.ReadAsync())
                {
                    messages.Add(new
                    {
                        messageId = msgReader["MessageId"],
                        senderUserId = msgReader["SenderUserId"],
                        body = msgReader["Body"],
                        attachments = msgReader["AttachmentsJson"] == DBNull.Value ? null : msgReader["AttachmentsJson"],
                        createdAt = msgReader["CreatedAt"],
                        isInternal = msgReader["IsInternal"]
                    });
                }

                return Ok(new { ticket, messages });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("ticket/{ticketId:long}/messages")]
        public async Task<IActionResult> AddMessage(long ticketId, [FromBody] AddMessageDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check access
                await using var accessCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM TicketParticipants WHERE TicketId = @TicketId AND UserId = @UserId", conn);
                accessCmd.Parameters.AddWithValue("@TicketId", ticketId);
                accessCmd.Parameters.AddWithValue("@UserId", userId);
                var hasAccess = Convert.ToInt32(await accessCmd.ExecuteScalarAsync() ?? 0) > 0;

                if (!hasAccess && !User.IsInRole("SuperAdmin") && !User.IsInRole("Support"))
                    return Forbid();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Add message
                    await using var msgCmd = new SqlCommand(@"
                        INSERT INTO TicketMessages (TicketId, SenderUserId, Body, AttachmentsJson, IsInternal, CreatedAt)
                        OUTPUT INSERTED.MessageId
                        VALUES (@TicketId, @UserId, @Body, @Attachments, @IsInternal, SYSUTCDATETIME())", conn, tx);
                    msgCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    msgCmd.Parameters.AddWithValue("@UserId", userId);
                    msgCmd.Parameters.AddWithValue("@Body", dto.Body);
                    msgCmd.Parameters.AddWithValue("@Attachments", dto.Attachments != null ? System.Text.Json.JsonSerializer.Serialize(dto.Attachments.Select(a => new { fileId = a })) : DBNull.Value);
                    msgCmd.Parameters.AddWithValue("@IsInternal", dto.IsInternal);
                    var messageId = await msgCmd.ExecuteScalarAsync();

                    // Update ticket activity
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE SupportTickets 
                        SET LastActivityAt = SYSUTCDATETIME(), Status = CASE WHEN Status = 'OPEN' THEN 'IN_PROGRESS' ELSE Status END
                        WHERE TicketId = @TicketId", conn, tx);
                    updateCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    await updateCmd.ExecuteNonQueryAsync();

                    // Log
                    await using var logCmd = new SqlCommand(@"
                        INSERT INTO TicketLogs (TicketId, Action, ActorUserId, Details, CreatedAt)
                        VALUES (@TicketId, 'MessageAdded', @UserId, 'New message added', SYSUTCDATETIME())", conn, tx);
                    logCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    logCmd.Parameters.AddWithValue("@UserId", userId);
                    await logCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return StatusCode(201, new { messageId = Convert.ToInt64(messageId) });
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("ticket/{ticketId:long}/close")]
        [Authorize(Policy = "SupportOrSuperAdmin")]
        public async Task<IActionResult> CloseTicket(long ticketId, [FromBody] CloseTicketDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Update ticket
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE SupportTickets 
                        SET Status = @Status, LastActivityAt = SYSUTCDATETIME()
                        WHERE TicketId = @TicketId", conn, tx);
                    updateCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    updateCmd.Parameters.AddWithValue("@Status", dto.CloseStatus);
                    await updateCmd.ExecuteNonQueryAsync();

                    // Log
                    await using var logCmd = new SqlCommand(@"
                        INSERT INTO TicketLogs (TicketId, Action, ActorUserId, Details, CreatedAt)
                        VALUES (@TicketId, 'Closed', @UserId, @Notes, SYSUTCDATETIME())", conn, tx);
                    logCmd.Parameters.AddWithValue("@TicketId", ticketId);
                    logCmd.Parameters.AddWithValue("@UserId", userId);
                    logCmd.Parameters.AddWithValue("@Notes", (object?)dto.ResolutionNotes ?? "Ticket closed");
                    await logCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return Ok(new { success = true, ticketId });
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

