using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/search")]
    [Authorize]
    [Tags("Section 16 - Search")]
    public class SearchController : ControllerBase
    {
        private readonly string _connStr;

        public SearchController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("global")]
        public async Task<IActionResult> Global([FromQuery] SearchQueryDto q)
        {
            try
            {
                // TODO: Integrate with Elasticsearch/OpenSearch
                // For now, return basic SQL-based search
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var results = new List<SearchResultDto>();
                var offset = (q.Page - 1) * q.Limit;

                if (q.Type == "all" || q.Type == "artists")
                {
                    await using var artistCmd = new SqlCommand(@"
                        SELECT TOP (@Limit) ArtistId, StageName, ClaimedUserId
                        FROM Artists
                        WHERE StageName LIKE @Query
                        ORDER BY StageName", conn);
                    artistCmd.Parameters.AddWithValue("@Query", $"%{q.Q}%");
                    artistCmd.Parameters.AddWithValue("@Limit", q.Limit);
                    await using var reader = await artistCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SearchResultDto
                        {
                            ArtistId = reader.GetInt32(0),
                            StageName = reader["StageName"].ToString(),
                            IsClaimed = reader["ClaimedUserId"] != DBNull.Value,
                            Score = 1.0
                        });
                    }
                }

                if (q.Type == "all" || q.Type == "releases")
                {
                    await using var releaseCmd = new SqlCommand(@"
                        SELECT TOP (@Limit) ReleaseId, Title, LabelID
                        FROM Releases
                        WHERE Title LIKE @Query
                        ORDER BY Title", conn);
                    releaseCmd.Parameters.AddWithValue("@Query", $"%{q.Q}%");
                    releaseCmd.Parameters.AddWithValue("@Limit", q.Limit);
                    await using var reader = await releaseCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SearchResultDto
                        {
                            ReleaseId = reader.GetInt32(0),
                            Title = reader["Title"].ToString()!,
                            LabelId = reader["LabelID"] == DBNull.Value ? null : reader.GetInt32(2),
                            Score = 1.0
                        });
                    }
                }

                // Log search token
                await using var logCmd = new SqlCommand(@"
                    INSERT INTO SearchTokens (Token, TokenType, CountHits, LastSearchedAt)
                    VALUES (@Token, 'global', 1, SYSUTCDATETIME())", conn);
                logCmd.Parameters.AddWithValue("@Token", q.Q.ToLower());
                try { await logCmd.ExecuteNonQueryAsync(); } catch { /* ignore duplicate */ }

                return Ok(new SearchResponse
                {
                    Q = q.Q,
                    Type = q.Type,
                    Page = q.Page,
                    PageSize = q.Limit,
                    TotalHits = results.Count,
                    Results = results,
                    TookMs = 0
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("artists")]
        public async Task<IActionResult> Artists([FromQuery] string q, [FromQuery] int limit = 20)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT TOP (@Limit) ArtistId, StageName, ClaimedUserId
                    FROM Artists
                    WHERE StageName LIKE @Query
                    ORDER BY StageName", conn);
                cmd.Parameters.AddWithValue("@Query", $"%{q}%");
                cmd.Parameters.AddWithValue("@Limit", limit);

                await using var reader = await cmd.ExecuteReaderAsync();
                var results = new List<SearchResultDto>();
                while (await reader.ReadAsync())
                {
                    results.Add(new SearchResultDto
                    {
                        ArtistId = reader.GetInt32(0),
                        StageName = reader["StageName"].ToString()!,
                        IsClaimed = reader["ClaimedUserId"] != DBNull.Value,
                        Score = 1.0
                    });
                }

                return Ok(new SearchResponse
                {
                    Q = q,
                    Type = "artists",
                    Page = 1,
                    PageSize = limit,
                    TotalHits = results.Count,
                    Results = results,
                    TookMs = 0
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("suggest")]
        public async Task<IActionResult> Suggest([FromQuery] string q, [FromQuery] string? type = null, [FromQuery] int limit = 10)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT TOP (@Limit) Token, TokenType, ReferenceId
                    FROM SearchTokens
                    WHERE Token LIKE @Query
                    ORDER BY CountHits DESC", conn);
                cmd.Parameters.AddWithValue("@Query", $"{q.ToLower()}%");
                cmd.Parameters.AddWithValue("@Limit", limit);

                await using var reader = await cmd.ExecuteReaderAsync();
                var suggestions = new List<SuggestionDto>();
                while (await reader.ReadAsync())
                {
                    suggestions.Add(new SuggestionDto
                    {
                        Token = reader["Token"].ToString()!,
                        Type = reader["TokenType"].ToString()!,
                        ReferenceId = reader["ReferenceId"] == DBNull.Value ? null : reader.GetInt32(2),
                        IsExactMatch = reader["Token"].ToString()!.Equals(q, StringComparison.OrdinalIgnoreCase)
                    });
                }

                return Ok(new SuggestResponse { Q = q, Suggestions = suggestions });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

