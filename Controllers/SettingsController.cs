using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/settings")]
    [Authorize]
    [Tags("Section 19 - Settings")]
    public class SettingsController : ControllerBase
    {
        private readonly string _connStr;

        public SettingsController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> Get(string key)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT SettingKey, SettingValue, Type, UpdatedAt
                    FROM Settings
                    WHERE SettingKey = @Key", conn);
                cmd.Parameters.AddWithValue("@Key", key);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound();

                return Ok(new SettingDto
                {
                    Key = reader["SettingKey"].ToString()!,
                    Value = reader["SettingValue"] == DBNull.Value ? null : reader["SettingValue"].ToString(),
                    Type = reader["Type"].ToString()!,
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("{key}")]
        [Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> Upsert(string key, [FromBody] UpdateSettingDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM Settings WHERE SettingKey = @Key)
                        UPDATE Settings 
                        SET SettingValue = @Value, Type = @Type, Description = @Description, UpdatedByUserId = @UserId, UpdatedAt = SYSUTCDATETIME()
                        WHERE SettingKey = @Key
                    ELSE
                        INSERT INTO Settings (SettingKey, SettingValue, Type, Description, UpdatedByUserId, UpdatedAt)
                        VALUES (@Key, @Value, @Type, @Description, @UserId, SYSUTCDATETIME())", conn);
                cmd.Parameters.AddWithValue("@Key", key);
                cmd.Parameters.AddWithValue("@Value", (object?)dto.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Type", dto.Type);
                cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", userId);

                await cmd.ExecuteNonQueryAsync();

                return Ok(new { key, value = dto.Value, message = "Setting updated" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

