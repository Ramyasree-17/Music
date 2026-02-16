using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    [ApiExplorerSettings(GroupName = "Whatsapp")]
    [Authorize]
    public class WhatsappController : ControllerBase
    {
        private readonly string _connStr;

        public WhatsappController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        // ✅ GET: api/whatsapp
        [HttpGet]
        public async Task<IActionResult> GetWhatsappConfig()
        {
            try
            {
                // ✅ Get BrandingId Automatically From Token
                var brandingIdClaim = User.FindFirst("BrandingId")?.Value;
                if (string.IsNullOrEmpty(brandingIdClaim) || !int.TryParse(brandingIdClaim, out int brandingId))
                    return BadRequest(new { error = "BrandingId not found in token" });

                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT Id, BrandingId, AppKey, AuthKey, CreatedAt
                    FROM WhatsappConfig
                    WHERE BrandingId = @BrandingId", conn);
                cmd.Parameters.AddWithValue("@BrandingId", brandingId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    // Return a default response instead of 404 when configuration doesn't exist
                    // This allows the frontend to know it needs to create a configuration
                    return Ok(new WhatsappResponseDto
                    {
                        Id = 0,
                        BrandingId = brandingId,
                        AppKey = string.Empty,
                        AuthKey = string.Empty,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                return Ok(new WhatsappResponseDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    BrandingId = Convert.ToInt32(reader["BrandingId"]),
                    AppKey = reader["AppKey"].ToString()!,
                    AuthKey = reader["AuthKey"].ToString()!,
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
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

        // ✅ POST: api/whatsapp
        [HttpPost]
        public async Task<IActionResult> CreateWhatsappConfig([FromBody] WhatsappDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // ✅ Get BrandingId Automatically From Token
                var brandingIdClaim = User.FindFirst("BrandingId")?.Value;
                if (string.IsNullOrEmpty(brandingIdClaim) || !int.TryParse(brandingIdClaim, out int brandingId))
                    return BadRequest(new { error = "BrandingId not found in token" });

                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check if BrandingId already exists
                await using var checkCmd = new SqlCommand(@"
                    SELECT COUNT(1) FROM WhatsappConfig WHERE BrandingId = @BrandingId", conn);
                checkCmd.Parameters.AddWithValue("@BrandingId", brandingId);
                var exists = (int)await checkCmd.ExecuteScalarAsync()! > 0;

                if (exists)
                    return BadRequest(new { error = "WhatsApp configuration already exists. Use PUT to update." });

                // Insert new configuration
                await using var insertCmd = new SqlCommand(@"
                    INSERT INTO WhatsappConfig (BrandingId, AppKey, AuthKey)
                    VALUES (@BrandingId, @AppKey, @AuthKey);
                    SELECT SCOPE_IDENTITY();", conn);
                insertCmd.Parameters.AddWithValue("@BrandingId", brandingId);
                insertCmd.Parameters.AddWithValue("@AppKey", dto.AppKey);
                insertCmd.Parameters.AddWithValue("@AuthKey", dto.AuthKey);

                var newId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());

                return Ok(new { 
                    message = "WhatsApp configuration created successfully",
                    id = newId,
                    brandingId = brandingId
                });
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627) // Unique constraint violation
                    return BadRequest(new { error = "WhatsApp configuration already exists" });
                
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ✅ PUT: api/whatsapp
        // ✅ POST: api/whatsapp/update (fallback for IIS deployments that block PUT)
        [HttpPut]
        [HttpPost("update")]
        public async Task<IActionResult> UpdateWhatsappConfig([FromBody] WhatsappDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // ✅ Get BrandingId Automatically From Token
                var brandingIdClaim = User.FindFirst("BrandingId")?.Value;
                if (string.IsNullOrEmpty(brandingIdClaim) || !int.TryParse(brandingIdClaim, out int brandingId))
                    return BadRequest(new { error = "BrandingId not found in token" });

                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    UPDATE WhatsappConfig
                    SET AppKey = @AppKey, AuthKey = @AuthKey
                    WHERE BrandingId = @BrandingId", conn);
                cmd.Parameters.AddWithValue("@BrandingId", brandingId);
                cmd.Parameters.AddWithValue("@AppKey", dto.AppKey);
                cmd.Parameters.AddWithValue("@AuthKey", dto.AuthKey);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    return NotFound(new { error = "WhatsApp configuration not found" });

                return Ok(new { 
                    message = "WhatsApp configuration updated successfully",
                    brandingId = brandingId
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
}


