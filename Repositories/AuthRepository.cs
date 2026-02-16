using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class AuthRepository : IAuthRepository
    {
        private readonly string _conn;

        public AuthRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        // ---------------------------------------------------------
        // CHECK EMAIL
        // ---------------------------------------------------------
        public async Task<(bool exists, string? displayName, string? email, string? role, int? brandingId)> CheckEmailAsync(string email)
        {
            using var conn = new SqlConnection(_conn);
            await conn.OpenAsync();

            // First, check if user exists
            using var cmd = new SqlCommand("sp_Auth_CheckEmail", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Email", email);

            string? displayName = null;
            string? userEmail = null;
            string? role = null;

            using (var rdr = await cmd.ExecuteReaderAsync())
            {
                if (await rdr.ReadAsync())
                {
                    int existsFlag = Convert.ToInt32(rdr["ExistsFlag"]);
                    if (existsFlag == 0) return (false, null, null, null, null);

                    displayName = rdr["DisplayName"]?.ToString();
                    userEmail = rdr["Email"]?.ToString();
                    role = rdr["Role"]?.ToString();
                }
                else
                {
                    return (false, null, null, null, null);
                }
            }

            // Now get brandingId from Branding table based on ContactEmail
            int? brandingId = null;
            using var brandingCmd = new SqlCommand(@"
                SELECT TOP 1 Id 
                FROM Branding 
                WHERE ContactEmail = @Email 
                  AND IsActive = 1
                ORDER BY Id DESC", conn);
            brandingCmd.Parameters.AddWithValue("@Email", email);
            
            using var brandingRdr = await brandingCmd.ExecuteReaderAsync();
            if (await brandingRdr.ReadAsync())
            {
                brandingId = brandingRdr["Id"] != DBNull.Value ? Convert.ToInt32(brandingRdr["Id"]) : null;
            }

            return (
                true,
                displayName,
                userEmail,
                role,
                brandingId
            );
        }

        // ---------------------------------------------------------
        // LOGIN FULL FLOW
        // ---------------------------------------------------------
        public async Task<(int code, string message, int? userId, string? fullName,
            string? email, string? role, string? passwordHash)> LoginFullFlowAsync(string email)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_LoginUser_FullFlow", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Email", email);

            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync())
                return (1, "Invalid credentials", null, null, null, null, null);

            return (
                rdr["Code"] != DBNull.Value ? Convert.ToInt32(rdr["Code"]) : 1,
                rdr["Message"]?.ToString() ?? "",
                rdr["UserID"] != DBNull.Value ? Convert.ToInt32(rdr["UserID"]) : null,
                rdr["FullName"]?.ToString(),
                rdr["Email"]?.ToString(),
                rdr["Role"]?.ToString(),
                rdr["PasswordHash"] != DBNull.Value ? Convert.ToBase64String((byte[])rdr["PasswordHash"]) : null
            );
        }

        // ---------------------------------------------------------
        // GET USER BY EMAIL
        // ---------------------------------------------------------
        public async Task<UserRecord?> GetUserByEmail(string email)
        {
            using var conn = new SqlConnection(_conn);
            var query = "SELECT TOP 1 * FROM Users WHERE Email = @Email";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Email", email);

            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;

            // Helper to safely get string value
            string SafeGetString(SqlDataReader r, string column)
            {
                try
                {
                    int idx = r.GetOrdinal(column);
                    if (r.IsDBNull(idx)) return null;
                    return r.GetString(idx);
                }
                catch (IndexOutOfRangeException)
                {
                    // Column not found, return null
                    return null;
                }
            }

            return new UserRecord
            {
                UserID = Convert.ToInt32(rdr["UserID"]),
                FullName = SafeGetString(rdr, "FullName"),
                Email = SafeGetString(rdr, "Email"),
                Role = SafeGetString(rdr, "Role"),
                // PasswordHash is stored as NVARCHAR(MAX) - read as string to preserve Unicode
                PasswordHash = SafeGetString(rdr, "PasswordHash"),
                PasswordSalt = SafeGetString(rdr, "PasswordSalt"),
                Status = SafeGetString(rdr, "Status"),
                IsActive = rdr["IsActive"] != DBNull.Value && Convert.ToBoolean(rdr["IsActive"])
            };
        }

        // ---------------------------------------------------------
        // VERIFY USER BY EMAIL
        // ---------------------------------------------------------
        public async Task<UserRecord?> VerifyUserByEmailAsync(string email)
        {
            return await GetUserByEmail(email); // Reuse existing method
        }

        // ---------------------------------------------------------
        // FORGET PASSWORD: START
        // ---------------------------------------------------------
        public async Task<(bool success, string key, int nextResend, string? mockOtp)>
            StartForgetPasswordAsync(string email, string key, string otpHash)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ForgetPassword_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@OtpHash", otpHash);

            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync())
                return (false, "", 0, null);

            return (
                Convert.ToBoolean(rdr["success"]),
                rdr["key"]?.ToString() ?? key,
                Convert.ToInt32(rdr["next_resend_wait_seconds"]),
                rdr["mock_otp"]?.ToString()
            );
        }

        // ---------------------------------------------------------
        // FORGET PASSWORD: RESEND OTP
        // ---------------------------------------------------------
        public async Task<(bool success, string? message, string? mockOtp)>
            ResendForgetPasswordAsync(string email, string key)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ForgetPassword_Resend", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Key", key);

            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync()) return (false, "Invalid key", null);

            return (
                Convert.ToBoolean(rdr["success"]),
                rdr["message"]?.ToString(),
                rdr["mock_otp"]?.ToString()
            );
        }

        // ---------------------------------------------------------
        // FORGET PASSWORD: VALIDATE OTP
        // ---------------------------------------------------------
        public async Task<(bool success, string? message)>
            ValidateForgetPasswordCodeAsync(string email, string key, string code)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ForgetPassword_ValidateCode", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@CodeHash", code);  // Changed: Now expects hash, not plain code

            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync()) return (false, "Invalid key/code");

            return (Convert.ToBoolean(rdr["success"]), rdr["message"]?.ToString());
        }

        // ---------------------------------------------------------
        // FORGET PASSWORD: RESET PASSWORD
        // ---------------------------------------------------------
        public async Task<(bool success, string? message)>
            ResetPasswordAsync(string email, string key, string newPasswordHash)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ForgetPassword_ResetPassword", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@NewPassword", newPasswordHash);

            await conn.OpenAsync();
            using var rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync()) return (false, "Reset failed");

            return (Convert.ToBoolean(rdr["Success"]), rdr["Message"]?.ToString());
        }
    }
}
