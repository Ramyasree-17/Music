using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class ReleaseRepository : IReleaseRepository
    {
        private readonly string _conn;

        public ReleaseRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<int> CreateReleaseAsync(CreateReleaseDto dto)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                // Create release
                using var cmd = new SqlCommand("sp_CreateRelease", conn, transaction)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@Title", dto.Title);
                cmd.Parameters.AddWithValue("@TitleVersion", (object?)dto.TitleVersion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LabelId", dto.LabelId);
                cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CoverArtUrl", (object?)dto.CoverArtUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PrimaryGenre", dto.PrimaryGenre);
                cmd.Parameters.AddWithValue("@SecondaryGenre", (object?)dto.SecondaryGenre ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DigitalReleaseDate", (object?)dto.DigitalReleaseDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OriginalReleaseDate", (object?)dto.OriginalReleaseDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HasUPC", dto.HasUPC);
                cmd.Parameters.AddWithValue("@UPCCode", (object?)dto.UPCCode ?? DBNull.Value);

                var releaseId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // Tracks are now created via /api/tracks. This repository no longer creates tracks from the release DTO.
                transaction.Commit();
                return releaseId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<ReleaseResponseDto?> GetReleaseByIdAsync(int releaseId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_GetReleaseById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var release = new ReleaseResponseDto
                {
                    ReleaseId = Convert.ToInt32(reader["ReleaseID"]),
                    Title = reader["Title"]?.ToString() ?? string.Empty,
                    TitleVersion = reader["TitleVersion"]?.ToString(),
                    LabelId = Convert.ToInt32(reader["LabelID"]),
                    Description = reader["Description"]?.ToString(),
                    CoverArtUrl = reader["CoverArtUrl"]?.ToString(),
                    PrimaryGenre = reader["PrimaryGenre"]?.ToString() ?? reader["Genre"]?.ToString() ?? string.Empty,
                    SecondaryGenre = reader["SecondaryGenre"]?.ToString(),
                    DigitalReleaseDate = reader["DigitalReleaseDate"] != DBNull.Value ? Convert.ToDateTime(reader["DigitalReleaseDate"]) : 
                                       (reader["ReleaseDate"] != DBNull.Value ? Convert.ToDateTime(reader["ReleaseDate"]) : null),
                    OriginalReleaseDate = reader["OriginalReleaseDate"] != DBNull.Value ? Convert.ToDateTime(reader["OriginalReleaseDate"]) : null,
                    HasUPC = reader["HasUPC"] != DBNull.Value && Convert.ToBoolean(reader["HasUPC"]),
                    UPCCode = reader["UPCCode"]?.ToString(),
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                    Tracks = new List<TrackResponseDto>()
                };

                if (reader.NextResult())
                {
                    while (await reader.ReadAsync())
                    {
                        release.Tracks.Add(new TrackResponseDto
                        {
                            TrackId = Convert.ToInt32(reader["TrackID"]),
                            Title = reader["Title"]?.ToString() ?? string.Empty,
                            TrackVersion = reader["TrackVersion"]?.ToString(),
                            ISRC = reader["ISRC"]?.ToString(),
                            TrackNumber = reader["TrackNumber"] != DBNull.Value ? Convert.ToInt32(reader["TrackNumber"]) : null,
                            Language = reader["Language"]?.ToString(),
                            Lyrics = reader["Lyrics"]?.ToString(),
                            IsExplicit = reader["IsExplicit"] != DBNull.Value && Convert.ToBoolean(reader["IsExplicit"]),
                            IsInstrumental = reader["IsInstrumental"] != DBNull.Value && Convert.ToBoolean(reader["IsInstrumental"]),
                            PreviewStartTimeSeconds = reader["PreviewStartTimeSeconds"] != DBNull.Value ? Convert.ToInt32(reader["PreviewStartTimeSeconds"]) : null,
                            TrackGenre = reader["TrackGenre"]?.ToString(),
                            DurationSeconds = reader["DurationSeconds"] != DBNull.Value ? Convert.ToInt32(reader["DurationSeconds"]) : null
                        });
                    }
                }

                return release;
            }

            return null;
        }

        public async Task<bool> UpdateReleaseAsync(int releaseId, UpdateReleaseDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_UpdateRelease", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@Title", (object?)dto.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TitleVersion", (object?)dto.TitleVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CoverArtUrl", (object?)dto.CoverArtUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrimaryGenre", (object?)dto.PrimaryGenre ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SecondaryGenre", (object?)dto.SecondaryGenre ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DigitalReleaseDate", (object?)dto.DigitalReleaseDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OriginalReleaseDate", (object?)dto.OriginalReleaseDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@HasUPC", dto.HasUPC.HasValue ? (object)dto.HasUPC.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@UPCCode", (object?)dto.UPCCode ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> DeleteReleaseAsync(int releaseId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_DeleteRelease", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> SubmitReleaseAsync(int releaseId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_SubmitRelease", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> TakedownReleaseAsync(int releaseId, string reason)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_TakedownRelease", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@ReleaseId", releaseId);
            cmd.Parameters.AddWithValue("@Reason", reason);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }
    }
}

