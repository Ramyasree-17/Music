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
    [Route("api/artists")]
    [Authorize]
    [Tags("Section 5 - Artists")]
    public class ArtistsController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;

        public ArtistsController(IConfiguration cfg)
        {
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateArtistDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                // Only LabelAdmin can create artists (not Artist role, not EnterpriseAdmin, not SuperAdmin)
                if (role != "LabelAdmin")
                {
                    return StatusCode(403, new { error = "Only LabelAdmin can create artists. You must be assigned to a label with LabelAdmin role." });
                }

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // If LabelId not provided or 0, automatically use the first label assigned to this user
                int? labelId = dto.LabelId;
                if (!labelId.HasValue || labelId.Value <= 0)
                {
                    using var lblCmd = new SqlCommand(@"
                        SELECT TOP 1 LabelID
                        FROM UserLabelRoles
                        WHERE UserID = @UserId AND Role = 'LabelAdmin'
                        ORDER BY LabelID;", conn);
                    lblCmd.Parameters.AddWithValue("@UserId", userId);

                    var lblResult = await lblCmd.ExecuteScalarAsync();
                    if (lblResult != null && lblResult != DBNull.Value)
                    {
                        labelId = Convert.ToInt32(lblResult);
                    }
                }

                // If still no label, do not allow creating artists – only label users can create artists
                if (!labelId.HasValue || labelId.Value <= 0)
                {
                    return BadRequest(new { error = "You must be assigned to a label with LabelAdmin role to create artists." });
                }

                using var cmd = new SqlCommand("sp_CreateArtist", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ArtistName", dto.ArtistName);
                cmd.Parameters.AddWithValue("@Bio", string.IsNullOrWhiteSpace(dto.Bio) ? DBNull.Value : dto.Bio);
                cmd.Parameters.AddWithValue("@ImageUrl", string.IsNullOrWhiteSpace(dto.ImageUrl) ? DBNull.Value : dto.ImageUrl);
                cmd.Parameters.AddWithValue("@DateOfBirth", (object?)dto.DateOfBirth ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Country", string.IsNullOrWhiteSpace(dto.Country) ? DBNull.Value : dto.Country);
                cmd.Parameters.AddWithValue("@Genre", string.IsNullOrWhiteSpace(dto.Genre) ? DBNull.Value : dto.Genre);
                cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(dto.Email) ? DBNull.Value : dto.Email);
                cmd.Parameters.AddWithValue("@LabelId", (object?)labelId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", userId);

                var result = await cmd.ExecuteScalarAsync();

                if (result == null || Convert.ToInt32(result) == 0)
                    return BadRequest(new { error = "Artist creation failed" });

                var newArtistId = Convert.ToInt32(result);

                // Ensure artist email exists as a User so they can log in
                if (!string.IsNullOrWhiteSpace(dto.Email))
                {
                    int artistUserId;

                    // 1) Check if user already exists
                    using (var getUserCmd = new SqlCommand(@"
                        SELECT TOP 1 UserID 
                        FROM Users 
                        WHERE Email = @Email;", conn))
                    {
                        getUserCmd.Parameters.AddWithValue("@Email", dto.Email);
                        var userResult = await getUserCmd.ExecuteScalarAsync();

                        if (userResult != null && userResult != DBNull.Value)
                        {
                            artistUserId = Convert.ToInt32(userResult);
                        }
                        else
                        {
                            // 2) Create new user with artist email (empty password, they'll use forgot password)
                            using var insertUserCmd = new SqlCommand(@"
                                INSERT INTO Users (
                                    FullName,
                                    Email,
                                    PasswordHash,
                                    Role,
                                    Status,
                                    IsActive,
                                    CreatedAt,
                                    UpdatedAt
                                )
                                VALUES (
                                    @FullName,
                                    @Email,
                                    '',              -- empty password, artist will set via forgot-password
                                    'Artist',
                                    'Active',
                                    1,
                                    SYSUTCDATETIME(),
                                    SYSUTCDATETIME()
                                );
                                SELECT SCOPE_IDENTITY();", conn);

                            insertUserCmd.Parameters.AddWithValue("@FullName", (object?)dto.StageName ?? dto.Email);
                            insertUserCmd.Parameters.AddWithValue("@Email", dto.Email);

                            var newUserIdObj = await insertUserCmd.ExecuteScalarAsync();
                            artistUserId = Convert.ToInt32(newUserIdObj);
                        }
                    }

                    // (Optional) You can later link artistUserId to this artist in a mapping table if needed
                }

                return StatusCode(201, new
                {
                    message = "Artist created successfully",
                    artistId = newArtistId
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

        [HttpGet]
        public async Task<IActionResult> GetArtists()
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_GetAllArtists", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    object? labelId = null;
                    try
                    {
                        if (!reader.IsDBNull("LabelId"))
                            labelId = reader["LabelId"];
                    }
                    catch
                    {
                        // LabelId column doesn't exist in result set
                        labelId = null;
                    }

                    list.Add(new
                    {
                        artistId = reader["ArtistID"],
                        artistName = reader["ArtistName"],
                        bio = reader["Bio"],
                        imageUrl = reader["ImageUrl"],
                        dateOfBirth = reader["DateOfBirth"],
                        country = reader["Country"],
                        genre = reader["Genre"],
                        labelId = labelId,
                        status = reader["Status"],
                        createdAt = reader["CreatedAt"]
                    });
                }

                return Ok(list);
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

        [HttpGet("{artistId}")]
        public async Task<IActionResult> GetArtist(int artistId)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_GetArtistById", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ArtistId", artistId);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    object? labelId = null;
                    try
                    {
                        if (!reader.IsDBNull("LabelId"))
                            labelId = reader["LabelId"];
                    }
                    catch
                    {
                        // LabelId column doesn't exist in result set
                        labelId = null;
                    }

                    var artist = new
                    {
                        artistId = reader["ArtistID"],
                        artistName = reader["ArtistName"],
                        bio = reader["Bio"],
                        imageUrl = reader["ImageUrl"],
                        dateOfBirth = reader["DateOfBirth"],
                        country = reader["Country"],
                        genre = reader["Genre"],
                        labelId = labelId,
                        status = reader["Status"],
                        createdAt = reader["CreatedAt"]
                    };

                    return Ok(artist);
                }

                return NotFound(new { error = "Artist not found" });
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

        [HttpPut("{artistId}")]
        public async Task<IActionResult> UpdateArtist(int artistId, [FromBody] UpdateArtistDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_UpdateArtist", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ArtistId", artistId);
                cmd.Parameters.AddWithValue("@ArtistName", string.IsNullOrWhiteSpace(dto.ArtistName) ? DBNull.Value : dto.ArtistName);
                cmd.Parameters.AddWithValue("@Bio", string.IsNullOrWhiteSpace(dto.Bio) ? DBNull.Value : dto.Bio);
                cmd.Parameters.AddWithValue("@ImageUrl", string.IsNullOrWhiteSpace(dto.ImageUrl) ? DBNull.Value : dto.ImageUrl);
                cmd.Parameters.AddWithValue("@DateOfBirth", (object?)dto.DateOfBirth ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Country", string.IsNullOrWhiteSpace(dto.Country) ? DBNull.Value : dto.Country);
                cmd.Parameters.AddWithValue("@Genre", string.IsNullOrWhiteSpace(dto.Genre) ? DBNull.Value : dto.Genre);

                await conn.OpenAsync();
                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound(new { error = "Artist not found" });

                return Ok(new { message = "Artist updated successfully" });
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

        [HttpPost("{artistId}/claim")]
        public async Task<IActionResult> ClaimArtist(int artistId, [FromBody] ClaimRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_ClaimArtist", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ArtistId", artistId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Reason", dto.Reason);

                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();

                if (result == null || Convert.ToInt32(result) == 0)
                    return BadRequest(new { error = "Failed to submit claim" });

                return Ok(new { message = "Claim submitted successfully", claimId = Convert.ToInt32(result) });
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

