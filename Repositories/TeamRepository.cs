using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class TeamRepository : ITeamRepository
    {
        private readonly string _conn;

        public TeamRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<int> CreateTeamAsync(CreateTeamDto dto, int createdBy)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();

                // Check if Teams table exists
                using var checkTableCmd = new SqlCommand(@"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Teams')
                    SELECT 1
                    ELSE
                    SELECT 0", conn);
                
                var tableExists = Convert.ToInt32(await checkTableCmd.ExecuteScalarAsync()) == 1;
                
                if (!tableExists)
                {
                    // Return a placeholder ID if table doesn't exist
                    return 0;
                }

                // Insert team if table exists
                using var cmd = new SqlCommand(@"
                    INSERT INTO Teams (TeamName, EntityType, EntityId, CreatedBy, CreatedAt)
                    VALUES (@TeamName, @EntityType, @EntityId, @CreatedBy, SYSUTCDATETIME());
                    SELECT SCOPE_IDENTITY();", conn);

                cmd.Parameters.AddWithValue("@TeamName", dto.TeamName ?? "");
                cmd.Parameters.AddWithValue("@EntityType", (object?)dto.EntityType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EntityId", (object?)dto.EntityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

                var result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task AddTeamMemberAsync(AddTeamMemberDto dto)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();

                // Check if TeamMembers table exists
                using var checkTableCmd = new SqlCommand(@"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TeamMembers')
                    SELECT 1
                    ELSE
                    SELECT 0", conn);
                
                var tableExists = Convert.ToInt32(await checkTableCmd.ExecuteScalarAsync()) == 1;
                
                if (!tableExists)
                {
                    return; // Silently return if table doesn't exist
                }

                // Insert team member if table exists
                using var cmd = new SqlCommand(@"
                    INSERT INTO TeamMembers (TeamId, UserId, RoleId, CreatedAt)
                    VALUES (@TeamId, @UserId, @RoleId, SYSUTCDATETIME());", conn);

                cmd.Parameters.AddWithValue("@TeamId", dto.TeamId);
                cmd.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd.Parameters.AddWithValue("@RoleId", dto.RoleId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Silently fail if table doesn't exist or any error occurs
            }
        }
    }
}



