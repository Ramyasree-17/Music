using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _conn;

        public UserRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<UserMeResponseDto?> GetUserProfileAsync(int userId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_Users_GetProfile", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@UserId", userId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var user = new UserMeResponseDto
                {
                    UserID = Convert.ToInt32(reader["UserID"]),
                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                    Email = reader["Email"]?.ToString() ?? string.Empty,
                    Role = reader["Role"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    Memberships = new UserMembershipDto()
                };

                // Get memberships
                if (reader.NextResult())
                {
                    while (await reader.ReadAsync())
                    {
                        var entityType = reader["EntityType"]?.ToString();
                        if (entityType == "Enterprise")
                        {
                            user.Memberships.Enterprises.Add(new EnterpriseMembershipDto
                            {
                                EnterpriseId = Convert.ToInt32(reader["EntityId"]),
                                EnterpriseName = reader["EntityName"]?.ToString() ?? string.Empty,
                                Role = reader["Role"]?.ToString() ?? string.Empty,
                                IsDefault = false // Will be set below
                            });
                        }
                        else if (entityType == "Label")
                        {
                            user.Memberships.Labels.Add(new LabelMembershipDto
                            {
                                LabelId = Convert.ToInt32(reader["EntityId"]),
                                LabelName = reader["EntityName"]?.ToString() ?? string.Empty,
                                Role = reader["Role"]?.ToString() ?? string.Empty,
                                IsDefault = false // Will be set below
                            });
                        }
                        else if (entityType == "Artist")
                        {
                            user.Memberships.Artists.Add(new ArtistMembershipDto
                            {
                                ArtistId = Convert.ToInt32(reader["EntityId"]),
                                ArtistName = reader["EntityName"]?.ToString() ?? string.Empty,
                                Role = reader["Role"]?.ToString() ?? string.Empty,
                                IsDefault = false // Will be set below
                            });
                        }
                    }
                }

                // Set first entity as default in each category
                if (user.Memberships.Enterprises.Count > 0)
                    user.Memberships.Enterprises[0].IsDefault = true;
                if (user.Memberships.Labels.Count > 0)
                    user.Memberships.Labels[0].IsDefault = true;
                if (user.Memberships.Artists.Count > 0)
                    user.Memberships.Artists[0].IsDefault = true;

                return user;
            }

            return null;
        }

        public async Task<bool> UpdateProfileAsync(int userId, UpdateProfileRequestDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_Users_UpdateProfile", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@FullName", (object?)dto.FullName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Mobile", (object?)dto.Mobile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CountryCode", (object?)dto.CountryCode ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPasswordHash, string newPasswordHash)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_Users_ChangePassword", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CurrentPasswordHash", currentPasswordHash);
            cmd.Parameters.AddWithValue("@NewPasswordHash", newPasswordHash);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<object> GetUserEntitiesAsync(int userId)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();

            // Check if user is SuperAdmin
            string? userRole = null;
            using (var roleCmd = new SqlCommand("SELECT Role FROM Users WHERE UserID = @UserId", conn))
            {
                roleCmd.Parameters.AddWithValue("@UserId", userId);
                var roleResult = await roleCmd.ExecuteScalarAsync();
                userRole = roleResult?.ToString();
            }

            bool isSuperAdmin = string.Equals(userRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            var enterprises = new List<object>();
            var labels = new List<object>();
            var artists = new List<object>();

            // =====================================================
            // ENTERPRISES
            // =====================================================
            if (isSuperAdmin)
            {
                // SuperAdmin sees all enterprises
                using (var cmd = new SqlCommand(@"
                    SELECT e.EnterpriseID, e.EnterpriseName, 
                           COALESCE(eur.Role, 'SuperAdmin') AS Role
                    FROM Enterprises e
                    LEFT JOIN EnterpriseUserRoles eur ON e.EnterpriseID = eur.EnterpriseID AND eur.UserID = @UserId
                    WHERE e.IsDeleted = 0 OR e.IsDeleted IS NULL
                    ORDER BY e.EnterpriseID ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        enterprises.Add(new
                        {
                            enterpriseId = Convert.ToInt32(reader["EnterpriseID"]),
                            enterpriseName = reader["EnterpriseName"]?.ToString() ?? string.Empty,
                            role = reader["Role"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }
            else
            {
                // Regular users see only enterprises they're associated with
                using (var cmd = new SqlCommand(@"
                    SELECT e.EnterpriseID, e.EnterpriseName, eur.Role
                    FROM EnterpriseUserRoles eur
                    INNER JOIN Enterprises e ON e.EnterpriseID = eur.EnterpriseID
                    WHERE eur.UserID = @UserId
                    ORDER BY e.EnterpriseID ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        enterprises.Add(new
                        {
                            enterpriseId = Convert.ToInt32(reader["EnterpriseID"]),
                            enterpriseName = reader["EnterpriseName"]?.ToString() ?? string.Empty,
                            role = reader["Role"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }

            // =====================================================
            // LABELS
            // =====================================================
            if (isSuperAdmin)
            {
                // SuperAdmin sees all labels
                using (var cmd = new SqlCommand(@"
                    SELECT l.LabelID, l.LabelName, l.PlanTypeId, 
                           COALESCE(ulr.Role, 'SuperAdmin') AS Role
                    FROM Labels l
                    LEFT JOIN UserLabelRoles ulr ON l.LabelID = ulr.LabelID AND ulr.UserID = @UserId
                    WHERE l.IsDeleted = 0 OR l.IsDeleted IS NULL
                    ORDER BY l.LabelID ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        labels.Add(new
                        {
                            labelId = Convert.ToInt32(reader["LabelID"]),
                            labelName = reader["LabelName"]?.ToString() ?? string.Empty,
                            role = reader["Role"]?.ToString() ?? string.Empty,
                            planType = ResolvePlanType(reader["PlanTypeId"])
                        });
                    }
                }
            }
            else
            {
                // Regular users see only labels they're associated with
                using (var cmd = new SqlCommand(@"
                    SELECT l.LabelID, l.LabelName, l.PlanTypeId, ulr.Role
                    FROM UserLabelRoles ulr
                    INNER JOIN Labels l ON l.LabelID = ulr.LabelID
                    WHERE ulr.UserID = @UserId
                    ORDER BY l.LabelID ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        labels.Add(new
                        {
                            labelId = Convert.ToInt32(reader["LabelID"]),
                            labelName = reader["LabelName"]?.ToString() ?? string.Empty,
                            role = reader["Role"]?.ToString() ?? string.Empty,
                            planType = ResolvePlanType(reader["PlanTypeId"])
                        });
                    }
                }
            }

            // =====================================================
            // ARTISTS
            // =====================================================
            using (var cmd = new SqlCommand(@"
                SELECT a.ArtistID, a.ArtistName AS StageName,
                       CASE 
                           WHEN a.ClaimedUserId = @UserId THEN 'Claimed'
                           ELSE 'Member'
                       END AS Role
                FROM Artists a
                WHERE a.ClaimedUserId = @UserId
                   OR a.UserId = @UserId
                ORDER BY a.ArtistID ASC", conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    artists.Add(new
                    {
                        artistId = Convert.ToInt32(reader["ArtistID"]),
                        stageName = reader["StageName"]?.ToString() ?? string.Empty,
                        role = reader["Role"]?.ToString() ?? string.Empty
                    });
                }
            }

            // =====================================================
            // RETURN STRUCTURED RESPONSE
            // =====================================================
            return new
            {
                enterprises,
                labels,
                artists
            };
        }

        public async Task<bool> UserHasAccessToEntityAsync(int userId, string entityType, int entityId)
        {
            var normalized = entityType.ToLowerInvariant();
            string query = normalized switch
            {
                "enterprise" => "SELECT COUNT(1) FROM EnterpriseUserRoles WHERE UserID = @UserId AND EnterpriseID = @EntityId",
                "label" => "SELECT COUNT(1) FROM UserLabelRoles WHERE UserID = @UserId AND LabelID = @EntityId",
                "artist" => "SELECT COUNT(1) FROM Artists WHERE ClaimedUserId = @UserId AND ArtistID = @EntityId",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(query))
                return false;

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@EntityId", entityId);

            await conn.OpenAsync();
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task<IEnumerable<ActivityLogDto>> GetActivityLogsAsync(int targetUserId, DateTime? fromUtc, DateTime? toUtc)
        {
            var logs = new List<ActivityLogDto>();

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand(@"
                SELECT TOP 500 AuditId, Action, DetailsJson, IpAddress, CreatedAt
                FROM AuditLogs
                WHERE ((TargetType = 'User' AND TargetId = @TargetId) OR ActorUserId = @TargetUserId)
                  AND (@FromUtc IS NULL OR CreatedAt >= @FromUtc)
                  AND (@ToUtc IS NULL OR CreatedAt <= @ToUtc)
                ORDER BY CreatedAt DESC", conn);

            cmd.Parameters.AddWithValue("@TargetId", targetUserId.ToString());
            cmd.Parameters.AddWithValue("@TargetUserId", targetUserId);
            cmd.Parameters.AddWithValue("@FromUtc", (object?)fromUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ToUtc", (object?)toUtc ?? DBNull.Value);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var detailsJson = reader["DetailsJson"]?.ToString();
                string description = string.Empty;
                
                // Try to parse JSON and extract description, otherwise use raw value
                if (!string.IsNullOrEmpty(detailsJson))
                {
                    try
                    {
                        var json = JsonDocument.Parse(detailsJson);
                        if (json.RootElement.TryGetProperty("description", out var descProp))
                            description = descProp.GetString() ?? detailsJson;
                        else
                            description = detailsJson;
                    }
                    catch
                    {
                        // If not valid JSON, use as-is
                        description = detailsJson;
                    }
                }
                
                logs.Add(new ActivityLogDto
                {
                    AuditId = Convert.ToInt64(reader["AuditId"]),
                    ActionType = reader["Action"]?.ToString() ?? string.Empty,
                    Description = description,
                    IpAddress = reader["IpAddress"]?.ToString(),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }

            return logs;
        }

        public async Task LogAuditAsync(int actorUserId, string actionType, string description, string? targetType = null, string? targetId = null, string? ipAddress = null)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                using var cmd = new SqlCommand(@"
                    INSERT INTO AuditLogs (ActorUserId, Action, DetailsJson, TargetType, TargetId, IpAddress, CreatedAt)
                    VALUES (@ActorUserId, @Action, @DetailsJson, @TargetType, @TargetId, @IpAddress, SYSUTCDATETIME())", conn);

                cmd.Parameters.AddWithValue("@ActorUserId", actorUserId);
                cmd.Parameters.AddWithValue("@Action", actionType);
                // Store description as JSON string for consistency with schema
                cmd.Parameters.AddWithValue("@DetailsJson", $"{{\"description\":\"{description?.Replace("\"", "\\\"") ?? ""}\"}}");
                cmd.Parameters.AddWithValue("@TargetType", (object?)targetType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TargetId", (object?)targetId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IpAddress", (object?)ipAddress ?? DBNull.Value);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Logging failures should not break the main flow.
            }
        }

        /// <summary>
        /// REQUIREMENT 1 IMPLEMENTED - Chesava (Done)
        /// Only Starter (1) and Growth (2) plans are supported
        /// Enterprise plan (3) has been removed
        /// </summary>
        private static string? ResolvePlanType(object value)
        {
            if (value == DBNull.Value)
                return null;

            return Convert.ToInt32(value) switch
            {
                1 => "Starter",      // Auto-generates random space name domain
                2 => "Growth",        // User provides their own domain
                _ => null
            };
        }
    }
}


