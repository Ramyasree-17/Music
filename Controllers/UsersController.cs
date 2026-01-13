using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Repositories;
using TunewaveAPIDB1.Services;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Tags("Section 2 - User & Identity")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _repo;
        private readonly PasswordService _passwordService;
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public UsersController(
            IUserRepository repo,
            PasswordService passwordService,
            IConfiguration cfg)
        {
            _repo = repo;
            _passwordService = passwordService;
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var user = await _repo.GetUserProfileAsync(userId);

                if (user == null)
                    return NotFound(new { error = "User not found" });

                return Ok(user);
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

        [HttpPost("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var success = await _repo.UpdateProfileAsync(userId, dto);

                if (!success)
                    return NotFound(new { error = "User not found" });

                await _repo.LogAuditAsync(userId, "Users.UpdateProfile", "Profile updated", "User", userId.ToString(), GetClientIp());

                return Ok(new { message = "Profile updated successfully" });
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

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (!string.IsNullOrWhiteSpace(dto.ConfirmPassword) &&
                    !string.Equals(dto.NewPassword, dto.ConfirmPassword, StringComparison.Ordinal))
                    return BadRequest(new { error = "New password and confirm password do not match" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                // Get current password hash
                string currentPasswordHash;
                using (var conn = new SqlConnection(_connStr))
                {
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand("SELECT PasswordHash FROM Users WHERE UserID = @UserId", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null)
                        return NotFound(new { error = "User not found" });
                    currentPasswordHash = result.ToString()!;
                }

                // Verify current password
                if (!_passwordService.Verify(dto.OldPassword, currentPasswordHash))
                    return Unauthorized(new { error = "Current password is incorrect" });

                // Hash new password
                var newPasswordHash = _passwordService.Hash(dto.NewPassword);

                // Update password
                var success = await _repo.ChangePasswordAsync(userId, currentPasswordHash, newPasswordHash);

                if (!success)
                    return StatusCode(500, new { error = "Failed to update password" });

                await _repo.LogAuditAsync(userId, "Users.ChangePassword", "Password changed", "User", userId.ToString(), GetClientIp());

                return Ok(new { message = "Password changed successfully" });
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

        [HttpGet("entities")]
        public async Task<IActionResult> GetEntities()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var entities = await _repo.GetUserEntitiesAsync(userId);
                return Ok(entities);
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

        [HttpPost("switch-entity")]
        public async Task<IActionResult> SwitchEntity([FromBody] SwitchEntityRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var normalizedType = dto.EntityType?.Trim().ToLowerInvariant() ?? string.Empty;

                if (normalizedType is not ("label" or "enterprise" or "artist"))
                    return BadRequest(new { error = "entityType must be label, enterprise, or artist" });

                var hasAccess = await _repo.UserHasAccessToEntityAsync(userId, normalizedType, dto.EntityId);
                if (!hasAccess)
                    return StatusCode(403, new { error = "You do not have access to this entity" });

                var activeEntity = $"{normalizedType}:{dto.EntityId}";

                await _repo.LogAuditAsync(userId, "Users.SwitchEntity",
                    $"Switched to {activeEntity}", normalizedType, dto.EntityId.ToString(), GetClientIp());

                return Ok(new { success = true, activeEntity });
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

        [HttpGet("activity-logs")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetActivityLogs([FromQuery] int userId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                if (userId <= 0)
                    return BadRequest(new { error = "userId is required" });

                var logs = await _repo.GetActivityLogsAsync(userId, from?.ToUniversalTime(), to?.ToUniversalTime());
                return Ok(new { userId, logs });
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

        private string? GetClientIp()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}

