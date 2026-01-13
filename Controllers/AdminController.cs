using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Services;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "SuperAdmin")]
    [Tags("Section 17 - Admin / SuperAdmin")]
    public class AdminController : ControllerBase
    {
        private readonly string _connStr;
        private readonly PasswordService _passwordService;

        public AdminController(IConfiguration cfg, PasswordService passwordService)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
            _passwordService = passwordService;
        }

        [HttpPost("create-superadmin")]
        public async Task<IActionResult> CreateSuperAdmin([FromBody] CreateSuperAdminDto dto)
        {
            try
            {
                var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Create user with empty password (they'll use forgot password to set)
                    await using var userCmd = new SqlCommand(@"
                        INSERT INTO Users (FullName, Email, PasswordHash, Role, Status, IsActive, CreatedAt, UpdatedAt)
                        OUTPUT INSERTED.UserID
                        VALUES (@FullName, @Email, @PasswordHash, 'SuperAdmin', 'Active', 1, SYSUTCDATETIME(), SYSUTCDATETIME())", conn, tx);
                    userCmd.Parameters.AddWithValue("@FullName", dto.FullName);
                    userCmd.Parameters.AddWithValue("@Email", dto.Email);
                    userCmd.Parameters.AddWithValue("@PasswordHash", "");

                    var userId = await userCmd.ExecuteScalarAsync();

                    // Audit
                    await using var auditCmd = new SqlCommand(@"
                        INSERT INTO AuditLogs (ActorUserId, ActorType, Action, TargetType, TargetId, DetailsJson, CreatedAt)
                        VALUES (@ActorId, 'User', 'CreateSuperAdmin', 'User', @UserId, @Details, SYSUTCDATETIME())", conn, tx);
                    auditCmd.Parameters.AddWithValue("@ActorId", actorId);
                    auditCmd.Parameters.AddWithValue("@UserId", userId);
                    auditCmd.Parameters.AddWithValue("@Details", $"{{\"email\":\"{dto.Email}\",\"mfaEnabled\":{dto.MfaEnabled}}}");
                    await auditCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return StatusCode(201, new { userId = Convert.ToInt32(userId), email = dto.Email });
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

        [HttpPost("delete-enterprise")]
        public async Task<IActionResult> DeleteEnterprise([FromBody] DeleteEntityDto dto)
        {
            try
            {
                var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Soft delete
                    await using var deleteCmd = new SqlCommand(@"
                        UPDATE Enterprises 
                        SET Status = 'Suspended', UpdatedAt = SYSUTCDATETIME()
                        WHERE EnterpriseId = @EnterpriseId", conn, tx);
                    deleteCmd.Parameters.AddWithValue("@EnterpriseId", dto.EntityId);
                    await deleteCmd.ExecuteNonQueryAsync();

                    // Audit
                    await using var auditCmd = new SqlCommand(@"
                        INSERT INTO AuditLogs (ActorUserId, ActorType, Action, TargetType, TargetId, DetailsJson, CreatedAt)
                        VALUES (@ActorId, 'User', 'DeleteEnterprise', 'Enterprise', @EntityId, @Details, SYSUTCDATETIME())", conn, tx);
                    auditCmd.Parameters.AddWithValue("@ActorId", actorId);
                    auditCmd.Parameters.AddWithValue("@EntityId", dto.EntityId);
                    auditCmd.Parameters.AddWithValue("@Details", $"{{\"reason\":\"{dto.Reason}\",\"archiveData\":{dto.ArchiveData}}}");
                    await auditCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return Accepted(new { message = "Enterprise deleted", enterpriseId = dto.EntityId });
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

        [HttpPost("delete-label")]
        public async Task<IActionResult> DeleteLabel([FromBody] DeleteEntityDto dto)
        {
            try
            {
                var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    await using var deleteCmd = new SqlCommand(@"
                        UPDATE Labels 
                        SET Status = 'Suspended', UpdatedAt = SYSUTCDATETIME()
                        WHERE LabelID = @LabelId", conn, tx);
                    deleteCmd.Parameters.AddWithValue("@LabelId", dto.EntityId);
                    await deleteCmd.ExecuteNonQueryAsync();

                    await using var auditCmd = new SqlCommand(@"
                        INSERT INTO AuditLogs (ActorUserId, ActorType, Action, TargetType, TargetId, DetailsJson, CreatedAt)
                        VALUES (@ActorId, 'User', 'DeleteLabel', 'Label', @EntityId, @Details, SYSUTCDATETIME())", conn, tx);
                    auditCmd.Parameters.AddWithValue("@ActorId", actorId);
                    auditCmd.Parameters.AddWithValue("@EntityId", dto.EntityId);
                    auditCmd.Parameters.AddWithValue("@Details", $"{{\"reason\":\"{dto.Reason}\"}}");
                    await auditCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return Accepted(new { message = "Label deleted", labelId = dto.EntityId });
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

        [HttpPost("delete-user")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteEntityDto dto)
        {
            try
            {
                var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    await using var deleteCmd = new SqlCommand(@"
                        UPDATE Users 
                        SET Status = 'Suspended', IsActive = 0, UpdatedAt = SYSUTCDATETIME()
                        WHERE UserID = @UserId", conn, tx);
                    deleteCmd.Parameters.AddWithValue("@UserId", dto.EntityId);
                    await deleteCmd.ExecuteNonQueryAsync();

                    await using var auditCmd = new SqlCommand(@"
                        INSERT INTO AuditLogs (ActorUserId, ActorType, Action, TargetType, TargetId, DetailsJson, CreatedAt)
                        VALUES (@ActorId, 'User', 'DeleteUser', 'User', @EntityId, @Details, SYSUTCDATETIME())", conn, tx);
                    auditCmd.Parameters.AddWithValue("@ActorId", actorId);
                    auditCmd.Parameters.AddWithValue("@EntityId", dto.EntityId);
                    auditCmd.Parameters.AddWithValue("@Details", $"{{\"reason\":\"{dto.Reason}\"}}");
                    await auditCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return Accepted(new { message = "User deleted", userId = dto.EntityId });
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

        [HttpGet("system-analytics")]
        public async Task<IActionResult> GetSystemAnalytics()
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Get active users (last 30 days)
                await using var usersCmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT UserID) FROM Users
                    WHERE LastLoginAt >= DATEADD(day, -30, SYSUTCDATETIME())", conn);
                var activeUsers = Convert.ToInt32(await usersCmd.ExecuteScalarAsync() ?? 0);

                // Get monthly new releases
                await using var releasesCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM Releases
                    WHERE CreatedAt >= DATEADD(month, -1, SYSUTCDATETIME())", conn);
                var monthlyReleases = Convert.ToInt32(await releasesCmd.ExecuteScalarAsync() ?? 0);

                // Get QC queue length
                await using var qcCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM QCQueue WHERE Status = 'PENDING'", conn);
                var qcQueueLength = Convert.ToInt32(await qcCmd.ExecuteScalarAsync() ?? 0);

                // Get delivery queue length
                await using var deliveryCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM DeliveryPackages WHERE Status IN ('IN_QUEUE', 'RETRY_SCHEDULED')", conn);
                var deliveryQueueLength = Convert.ToInt32(await deliveryCmd.ExecuteScalarAsync() ?? 0);

                // Get support tickets
                await using var supportCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM SupportTickets WHERE Status IN ('OPEN', 'IN_PROGRESS')", conn);
                var openTickets = Convert.ToInt32(await supportCmd.ExecuteScalarAsync() ?? 0);

                // Get pending payout amount
                await using var payoutCmd = new SqlCommand(@"
                    SELECT SUM(Amount) FROM PayoutTransactions WHERE Status = 'PENDING'", conn);
                var pendingPayout = await payoutCmd.ExecuteScalarAsync();
                var pendingPayoutAmount = pendingPayout == DBNull.Value ? 0m : Convert.ToDecimal(pendingPayout);

                return Ok(new SystemAnalyticsResponse
                {
                    ActiveUsersLast30Days = activeUsers,
                    MonthlyNewReleases = monthlyReleases,
                    MonthlyRevenue = new Dictionary<string, decimal>(), // TODO: Calculate from ledger
                    OpenSupportTickets = openTickets,
                    QcQueueLength = qcQueueLength,
                    DeliveryQueueLength = deliveryQueueLength,
                    SearchAvgLatencyMs = 0, // TODO: Track from search logs
                    S3StorageBytes = 0, // TODO: Get from S3
                    PendingPayoutAmount = pendingPayoutAmount
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("fix-password-hash")]
        public async Task<IActionResult> FixPasswordHash([FromBody] FixPasswordHashDto dto)
        {
            try
            {
                var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Validate input
                if (!dto.UserId.HasValue && string.IsNullOrWhiteSpace(dto.Email))
                    return BadRequest(new { error = "Either UserId or Email must be provided" });

                // Hash the plain text password
                var hashedPassword = _passwordService.Hash(dto.PlainTextPassword);

                // Update password hash
                string updateSql;
                if (dto.UserId.HasValue)
                {
                    updateSql = "UPDATE Users SET PasswordHash = @PasswordHash, UpdatedAt = SYSUTCDATETIME() WHERE UserID = @UserId";
                }
                else
                {
                    updateSql = "UPDATE Users SET PasswordHash = @PasswordHash, UpdatedAt = SYSUTCDATETIME() WHERE Email = @Email";
                }

                await using var updateCmd = new SqlCommand(updateSql, conn);
                
                if (dto.UserId.HasValue)
                    updateCmd.Parameters.AddWithValue("@UserId", dto.UserId.Value);
                else
                    updateCmd.Parameters.AddWithValue("@Email", dto.Email);
                
                updateCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);

                var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    return NotFound(new { error = "User not found or no changes made" });

                // Get the updated user ID for audit
                int targetUserId = 0;
                if (dto.UserId.HasValue)
                {
                    targetUserId = dto.UserId.Value;
                }
                else
                {
                    await using var getIdCmd = new SqlCommand("SELECT UserID FROM Users WHERE Email = @Email", conn);
                    getIdCmd.Parameters.AddWithValue("@Email", dto.Email);
                    var userIdResult = await getIdCmd.ExecuteScalarAsync();
                    if (userIdResult != null)
                        targetUserId = Convert.ToInt32(userIdResult);
                }

                // Audit
                await using var auditCmd = new SqlCommand(@"
                    INSERT INTO AuditLogs (ActorUserId, ActorType, Action, TargetType, TargetId, DetailsJson, CreatedAt)
                    VALUES (@ActorId, 'User', 'FixPasswordHash', 'User', @TargetId, @Details, SYSUTCDATETIME())", conn);
                auditCmd.Parameters.AddWithValue("@ActorId", actorId);
                auditCmd.Parameters.AddWithValue("@TargetId", targetUserId);
                auditCmd.Parameters.AddWithValue("@Details", $"{{\"email\":\"{dto.Email ?? ""}\",\"fixed\":true}}");
                await auditCmd.ExecuteNonQueryAsync();

                return Ok(new { 
                    message = "Password hash fixed successfully", 
                    userId = targetUserId, 
                    email = dto.Email,
                    passwordHash = hashedPassword  // Return hash for SQL use
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

