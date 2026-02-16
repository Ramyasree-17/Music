using System.Data;
using Microsoft.Data.SqlClient;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class AddressRepository : IAddressRepository
    {
        private readonly string _conn;

        public AddressRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<bool> ExistsAsync(int ownerId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_UserAddresses_Exists", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@OwnerId", ownerId);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<AddressResponseDto?> GetByOwnerAsync(int ownerId)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                
                // Check if stored procedure exists, if not return null
                await conn.OpenAsync();
                using var checkCmd = new SqlCommand(@"
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_UserAddresses_GetByOwner]') AND type in (N'P', N'PC'))
                    SELECT 1 ELSE SELECT 0", conn);
                var procExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) == 1;
                
                if (!procExists)
                    return null;

                using var cmd = new SqlCommand("sp_UserAddresses_GetByOwner", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@OwnerId", ownerId);

                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

                if (!await reader.ReadAsync())
                    return null;

                // Helper to safely get column value
                string? SafeGetString(SqlDataReader r, string column)
                {
                    try
                    {
                        int idx = r.GetOrdinal(column);
                        if (r.IsDBNull(idx)) return null;
                        return r.GetString(idx);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        return null;
                    }
                }

                DateTime? SafeGetDateTime(SqlDataReader r, string column)
                {
                    try
                    {
                        int idx = r.GetOrdinal(column);
                        if (r.IsDBNull(idx)) return null;
                        return r.GetDateTime(idx);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        return null;
                    }
                }

                return new AddressResponseDto
                {
                    AddressId = Convert.ToInt32(reader["AddressId"]),
                    Type = "User",
                    OwnerId = Convert.ToInt32(reader["OwnerId"]),
                    AddressLine1 = SafeGetString(reader, "AddressLine1"),
                    AddressLine2 = SafeGetString(reader, "AddressLine2"),
                    City = SafeGetString(reader, "City"),
                    State = SafeGetString(reader, "State"),
                    Country = SafeGetString(reader, "Country"),
                    Pincode = SafeGetString(reader, "Pincode"),
                    CreatedAt = SafeGetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                    UpdatedAt = SafeGetDateTime(reader, "UpdatedAt")
                };
            }
            catch
            {
                // If any error occurs (stored procedure doesn't exist, table doesn't exist, etc.), return null
                return null;
            }
        }

        public async Task<int> UpsertAsync(int ownerId, AddressUpsertRequestDto dto)
        {
            try
            {
                using var conn = new SqlConnection(_conn);
                await conn.OpenAsync();
                
                // Check if stored procedure exists
                using var checkCmd = new SqlCommand(@"
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_UserAddresses_Upsert]') AND type in (N'P', N'PC'))
                    SELECT 1 ELSE SELECT 0", conn);
                var procExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) == 1;
                
                if (!procExists)
                    return 0; // Return 0 if procedure doesn't exist

                using var cmd = new SqlCommand("sp_UserAddresses_Upsert", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@OwnerId", ownerId);
                cmd.Parameters.AddWithValue("@AddressLine1", (object?)dto.AddressLine1 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Country", (object?)dto.Country ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);

                return await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // If any error occurs, return 0
                return 0;
            }
        }
    }
}


