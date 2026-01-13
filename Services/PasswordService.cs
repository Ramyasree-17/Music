using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TunewaveAPIDB1.Services
{
    public class PasswordService
    {
        // ✅ ALWAYS use BCrypt for NEW passwords
        public string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // ✅ Verify + auto-detect legacy formats
        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            // ✅ BCrypt (preferred)
            if (storedHash.StartsWith("$2"))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }

            // ⚠️ Legacy SHA-256 HEX
            if (storedHash.Length == 64 && Regex.IsMatch(storedHash, @"^[0-9A-Fa-f]{64}$"))
            {
                using var sha = SHA256.Create();
                var computed = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var hex = BitConverter.ToString(computed).Replace("-", "");
                return hex.Equals(storedHash, StringComparison.OrdinalIgnoreCase);
            }

            // ⚠️ Legacy PBKDF2 Unicode
            return VerifyLegacyPbkdf2(password, storedHash);
        }

        // ---------------- LEGACY SUPPORT ONLY ---------------- 
        private bool VerifyLegacyPbkdf2(string password, string storedHash)
        {
            // Try multiple parameter combinations for legacy hashes
            var iterationsToTry = new[] { 100_000, 10_000, 50_000, 1_000, 5_000, 20_000 };
            var hashAlgorithms = new[] { HashAlgorithmName.SHA256, HashAlgorithmName.SHA1, HashAlgorithmName.SHA512 };

            foreach (var iterations in iterationsToTry)
            {
                foreach (var hashAlg in hashAlgorithms)
                {
                    // Try UTF-8 password encoding
                    if (TryVerifyPbkdf2(password, storedHash, iterations, hashAlg, Encoding.UTF8))
                        return true;
                    
                    // Try UTF-16 password encoding
                    if (TryVerifyPbkdf2(password, storedHash, iterations, hashAlg, Encoding.Unicode))
                        return true;
                }
            }

            return false;
        }

        private bool TryVerifyPbkdf2(string password, string storedHash, int iterations, HashAlgorithmName hashAlgorithm, Encoding passwordEncoding)
        {
            try
            {
                // Unicode chars → bytes (UTF-16 encoding - big-endian)
                byte[] bytes = new byte[storedHash.Length * 2];
                for (int i = 0; i < storedHash.Length; i++)
                {
                    ushort v = storedHash[i];
                    bytes[i * 2] = (byte)(v >> 8);      // High byte
                    bytes[i * 2 + 1] = (byte)(v & 0xFF); // Low byte
                }

                // Validate we have enough bytes (16 salt + 32 hash = 48 bytes = 24 Unicode chars)
                if (bytes.Length < 48)
                    return false;

                // Extract salt (first 16 bytes)
                byte[] salt = new byte[16];
                Array.Copy(bytes, 0, salt, 0, 16);

                // Convert password to bytes using specified encoding
                byte[] passwordBytes = passwordEncoding.GetBytes(password);

                // Use Rfc2898DeriveBytes with password bytes directly
                using var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, iterations, hashAlgorithm);
                byte[] hash = pbkdf2.GetBytes(32);

                // Compare hash (bytes 16-47)
                for (int i = 0; i < 32; i++)
                {
                    if (bytes[i + 16] != hash[i])
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // 🔥 Used to detect if migration is needed
        public bool IsLegacyHash(string hash)
        {
            return !hash.StartsWith("$2");
        }
    }
}
