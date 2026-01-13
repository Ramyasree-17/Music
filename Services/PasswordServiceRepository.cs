using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TunewaveAPIDB1.Services
{
    public interface IPasswordServiceRepository
    {
        Task<(bool success, string message)> ResetPasswordAsync(
            string email,
            string passwordHash);
    }

    public class PasswordServiceRepository : IPasswordServiceRepository
    {
        private readonly IConfiguration _configuration;

        public PasswordServiceRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ✅ SINGLE, CORRECT METHOD
        public async Task<(bool success, string message)> ResetPasswordAsync(
            string email,
            string passwordHash)
        {
            using var conn = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using var cmd = new SqlCommand("sp_ResetPassword", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 255)
                          .Value = email;

            cmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, -1)
                          .Value = passwordHash;

            await conn.OpenAsync();
            int rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
                return (false, "Email not found");

            return (true, "Password updated successfully");
        }
    }
}
