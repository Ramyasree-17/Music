using System.Security.Cryptography;
using System.Text;

namespace TunewaveAPIDB1.Services
{
    public class ResetTokenService
    {
        public string GenerateResetKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        public string GenerateOtp(int length = 6)
        {
            if (length <= 0) length = 6;
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(RandomNumberGenerator.GetInt32(0, 10));
            }
            return sb.ToString();
        }

        public string HashOtp(string otp)
        {
            if (otp == null) otp = string.Empty;
            using var sha = SHA256.Create();
            var b = Encoding.UTF8.GetBytes(otp);
            var h = sha.ComputeHash(b);
            return Convert.ToBase64String(h);
        }

        public bool VerifyOtpHash(string otp, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            var candidate = HashOtp(otp ?? string.Empty);
            try
            {
                var a = Convert.FromBase64String(candidate);
                var b = Convert.FromBase64String(storedHash);
                return CryptographicOperations.FixedTimeEquals(a, b);
            }
            catch
            {
                return false;
            }
        }
    }
}
