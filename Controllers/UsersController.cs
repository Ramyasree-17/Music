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
        private readonly IAddressRepository _addressRepo;
        private readonly PasswordService _passwordService;
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public UsersController(
            IUserRepository repo,
            IAddressRepository addressRepo,
            PasswordService passwordService,
            IConfiguration cfg)
        {
            _repo = repo;
            _addressRepo = addressRepo;
            _passwordService = passwordService;
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        // ================= GET CURRENT USER =================

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT TOP 1
                        UserID,
                        FullName,
                        Email,
                        Mobile,
                        CountryCode,
                        Gender,
                        DateOfBirth,
                        Role,
                        Status
                    FROM Users
                    WHERE UserID = @UserId", conn);

                cmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await reader.ReadAsync())
                    return NotFound(new { error = "User not found" });

                object? GetNullable(string column)
                {
                    try
                    {
                        var idx = reader.GetOrdinal(column);
                        return reader.IsDBNull(idx) ? null : reader.GetValue(idx);
                    }
                    catch
                    {
                        return null;
                    }
                }

                return Ok(new
                {
                    id = Convert.ToInt32(reader["UserID"]),
                    fullName = GetNullable("FullName")?.ToString(),
                    email = GetNullable("Email")?.ToString(),
                    mobile = GetNullable("Mobile")?.ToString(),
                    countryCode = GetNullable("CountryCode")?.ToString(),
                    gender = GetNullable("Gender")?.ToString(),
                    dateOfBirth = GetNullable("DateOfBirth"),
                    role = GetNullable("Role")?.ToString(),
                    status = GetNullable("Status")?.ToString()
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

        // ================= UPDATE PROFILE =================

        [HttpPut("update-profile")]
        [HttpPost("update-profile")] // Production fallback
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

                await _repo.LogAuditAsync(userId, "Users.UpdateProfile",
                    "Profile updated", "User", userId.ToString(), GetClientIp());

                return Ok(new { message = "Profile updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ================= ADDRESS =================

        [HttpGet("address")]
        public async Task<IActionResult> GetAddress()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var address = await _addressRepo.GetByOwnerAsync(userId);

                if (address == null)
                    return NotFound(new { error = "Address not found" });

                return Ok(new
                {
                    address.AddressLine1,
                    address.AddressLine2,
                    address.City,
                    address.State,
                    address.Country,
                    address.Pincode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("address")]
        public async Task<IActionResult> AddAddress([FromBody] AddressUpsertRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var exists = await _addressRepo.ExistsAsync(userId);
                if (exists)
                    return Conflict(new { error = "Address already exists. Use update." });

                var rows = await _addressRepo.UpsertAsync(userId, dto);

                await _repo.LogAuditAsync(userId, "Users.AddAddress",
                    "Address added", "User", userId.ToString(), GetClientIp());

                return Ok(new { message = "Address added successfully", rowsAffected = rows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("address")]
        [HttpPost("address/update")] // Production-safe fallback
        public async Task<IActionResult> UpdateAddress([FromBody] AddressUpsertRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var exists = await _addressRepo.ExistsAsync(userId);
                if (!exists)
                    return NotFound(new { error = "Address not found" });

                var rows = await _addressRepo.UpsertAsync(userId, dto);

                await _repo.LogAuditAsync(userId, "Users.UpdateAddress",
                    "Address updated", "User", userId.ToString(), GetClientIp());

                return Ok(new { message = "Address updated successfully", rowsAffected = rows });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ================= CHANGE PASSWORD =================

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (dto.NewPassword != dto.ConfirmPassword)
                    return BadRequest(new { error = "Passwords do not match" });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                var success = await _repo.ChangePasswordAsync(userId, dto.OldPassword, dto.NewPassword);

                if (!success)
                    return Unauthorized(new { error = "Invalid current password" });

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ================= HELPER =================

       


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