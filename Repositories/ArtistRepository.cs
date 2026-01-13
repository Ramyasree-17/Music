using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class ArtistRepository : IArtistRepository
    {
        private readonly string _conn;

        public ArtistRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<int> CreateArtistAsync(CreateArtistDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_CreateArtist", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistName", dto.ArtistName);
            cmd.Parameters.AddWithValue("@Bio", (object?)dto.Bio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ImageUrl", (object?)dto.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DateOfBirth", (object?)dto.DateOfBirth ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Country", (object?)dto.Country ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Genre", (object?)dto.Genre ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<ArtistResponseDto?> GetArtistByIdAsync(int artistId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_GetArtistById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistId", artistId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new ArtistResponseDto
                {
                    ArtistId = Convert.ToInt32(reader["ArtistID"]),
                    ArtistName = reader["ArtistName"]?.ToString() ?? string.Empty,
                    Bio = reader["Bio"]?.ToString(),
                    ImageUrl = reader["ImageUrl"]?.ToString(),
                    DateOfBirth = reader["DateOfBirth"] != DBNull.Value ? Convert.ToDateTime(reader["DateOfBirth"]) : null,
                    Country = reader["Country"]?.ToString(),
                    Genre = reader["Genre"]?.ToString(),
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                };
            }

            return null;
        }

        public async Task<bool> UpdateArtistAsync(int artistId, UpdateArtistDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_UpdateArtist", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistId", artistId);
            cmd.Parameters.AddWithValue("@ArtistName", (object?)dto.ArtistName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Bio", (object?)dto.Bio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ImageUrl", (object?)dto.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DateOfBirth", (object?)dto.DateOfBirth ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Country", (object?)dto.Country ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Genre", (object?)dto.Genre ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> ClaimArtistAsync(int artistId, int userId, string reason)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ClaimArtist", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistId", artistId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Reason", reason);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<bool> ReviewClaimAsync(int artistId, int claimId, bool approved, string? comments)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ReviewArtistClaim", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistId", artistId);
            cmd.Parameters.AddWithValue("@ClaimId", claimId);
            cmd.Parameters.AddWithValue("@Approved", approved);
            cmd.Parameters.AddWithValue("@Comments", (object?)comments ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> GrantAccessAsync(int artistId, int userId, string role)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_GrantArtistAccess", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistId", artistId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Role", role);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> RevokeAccessAsync(int artistId, int userId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_RevokeArtistAccess", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ArtistId", artistId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }
    }
}

