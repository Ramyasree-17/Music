using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Repositories;
using TunewaveAPIDB1.Services;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Tags("Section 1 - Authentication & Session")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _repo;
        private readonly JwtService _jwt;
        private readonly PasswordService _passwordService;
        private readonly ResetTokenService _reset;
        private readonly IConfiguration _cfg;
        private readonly EmailService _emailService;

        public AuthController(
            IConfiguration cfg,
            IAuthRepository repo,
            JwtService jwt,
            PasswordService passwordService,
            ResetTokenService reset,
            EmailService emailService)
        {
            _cfg = cfg;
            _repo = repo;
            _jwt = jwt;
            _passwordService = passwordService;
            _reset = reset;
            _emailService = emailService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
                return BadRequest(new { error = "Passwords do not match" });

            using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
            using var cmd = new SqlCommand("sp_RegisterUser", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@FullName", dto.FullName);
            cmd.Parameters.AddWithValue("@Email", dto.Email.Trim());
            cmd.Parameters.AddWithValue("@PasswordHash", _passwordService.Hash(dto.Password));
            cmd.Parameters.AddWithValue("@Role", string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role);
            cmd.Parameters.AddWithValue("@Mobile", string.IsNullOrWhiteSpace(dto.Mobile) ? DBNull.Value : dto.Mobile);
            cmd.Parameters.AddWithValue("@CountryCode", string.IsNullOrWhiteSpace(dto.CountryCode) ? DBNull.Value : dto.CountryCode);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            if (result == null)
                return StatusCode(500, new { error = "Failed to create user" });

            var newUserId = Convert.ToInt32(result);
            if (newUserId == -1)
                return Conflict(new { error = "Email already registered" });

            return StatusCode(201, new RegisterResponseDto
            {
                Message = "User registered successfully",
                UserId = newUserId,
                Email = dto.Email,
                FullName = dto.FullName,
                Role = string.IsNullOrWhiteSpace(dto.Role) ? "User" : dto.Role!
            });
        }

        // ----------------------------- CHECK EMAIL -----------------------------
        [HttpGet("check-email")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "Email is required" });

            var (exists, name, em, role) = await _repo.CheckEmailAsync(email);

            if (!exists)
                return Ok(new { exists = false });

            return Ok(new { exists = true, displayName = name, email = em, role });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequestDto req)
        {
            req.Email = req.Email?.Trim().ToLower();
            req.Password = req.Password?.Trim();

            // 1️⃣ BASIC LOGIN (ALWAYS)
            var user = await _repo.GetUserByEmail(req.Email);
            if (user == null)
                return Unauthorized(new { message = "Invalid email or password" });

            bool valid = _passwordService.Verify(req.Password, user.PasswordHash);
            if (!valid)
                return Unauthorized(new { message = "Invalid email or password" });

            // 🔑 READ DOMAIN CONTEXT (Enterprise or Label)
            Request.Cookies.TryGetValue("enterprise_id", out var enterpriseIdStr);
            Request.Cookies.TryGetValue("label_id", out var labelIdStr);

            int? enterpriseId = null;
            int? labelId = null;
            string? domain = null;

            // 🔒 DOMAIN RESTRICTION - Enterprise
            if (!string.IsNullOrEmpty(enterpriseIdStr))
            {
                enterpriseId = int.Parse(enterpriseIdStr);

                using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT 1 FROM EnterpriseUserRoles
                    WHERE EnterpriseID = @EnterpriseId AND UserID = @UserId", conn);

                cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                cmd.Parameters.AddWithValue("@UserId", user.UserID);

                var hasAccess = (await cmd.ExecuteScalarAsync()) != null;

                if (!hasAccess)
                {
                    return Unauthorized(new
                    {
                        error = "You do not belong to this enterprise domain"
                    });
                }
            }
            // 🔒 DOMAIN RESTRICTION - Label
            else if (!string.IsNullOrEmpty(labelIdStr))
            {
                labelId = int.Parse(labelIdStr);

                using var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT 1 FROM UserLabelRoles
                    WHERE LabelID = @LabelId AND UserID = @UserId", conn);

                cmd.Parameters.AddWithValue("@LabelId", labelId);
                cmd.Parameters.AddWithValue("@UserId", user.UserID);

                var hasAccess = (await cmd.ExecuteScalarAsync()) != null;

                if (!hasAccess)
                {
                    return Unauthorized(new
                    {
                        error = "You do not belong to this label domain"
                    });
                }
            }

            // ==================================================
            // ✅ LOGIN SUCCESS
            // ==================================================
            var token = _jwt.GenerateToken(
                user,
                enterpriseId: enterpriseId,
                labelId: labelId,
                domain: domain
            );

            return Ok(new
            {
                message = "Login successful",
                token
            });
        }



        [HttpGet("verify")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> Verify()
        {
            // Extract email
            var email = User.FindFirst(ClaimTypes.Email)?.Value
                        ?? User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                        ?? User.FindFirst("email")?.Value
                        ?? User.FindFirst(ClaimTypes.Name)?.Value
                        ?? User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { error = "Invalid or expired token" });

            email = email.Trim().ToLowerInvariant();

            UserRecord user = null;

            using (var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("sp_Auth_Verify", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Email", email);

                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        if (!await reader.ReadAsync())
                            return NotFound(new { error = "User not found" });

                        // SAFE helper to convert any column to string
                        string SafeGetString(SqlDataReader r, string column)
                        {
                            int idx = r.GetOrdinal(column);
                            if (r.IsDBNull(idx)) return null;

                            object v = r.GetValue(idx);

                            return v switch
                            {
                                string s => s,
                                bool b => b ? "1" : "0",  // convert BIT to string
                                int i => i.ToString(),
                                long l => l.ToString(),
                                decimal d => d.ToString(),
                                DateTime dt => dt.ToString("o"),
                                _ => v.ToString()
                            };
                        }

                        // Build user
                        user = new UserRecord
                        {
                            UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                            FullName = SafeGetString(reader, "FullName"),
                            Email = SafeGetString(reader, "Email"),
                            Role = SafeGetString(reader, "Role"),
                            Status = SafeGetString(reader, "Status"),

                            // VERY IMPORTANT:
                            // PasswordHash is stored as UNICODE STRING
                            // DO NOT base64, DO NOT binary convert
                            PasswordHash = SafeGetString(reader, "PasswordHash")
                        };
                    }
                }
            }

            // Do NOT return PasswordHash in response

            return Ok(new
            {
                message = "Token verified successfully",
                user = new
                {
                    userID = user.UserID,
                    fullName = user.FullName,
                    email = user.Email,
                    role = user.Role,
                    status = user.Status
                }
            });
        }







        // ----------------------------- FORGOT PASSWORD -----------------------------
        [HttpPost("forgetpassword")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgetPassword([FromBody] ForgetPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var key = _reset.GenerateResetKey();
            var otp = _reset.GenerateOtp(6);
            var otpHash = _reset.HashOtp(otp);

            var (success, returnedKey, next, mockOtp) =
                await _repo.StartForgetPasswordAsync(dto.Email, key.ToString(), otpHash);

            if (!success)
                return NotFound(new { message = "Email not found" });

            await _emailService.SendOtpEmailAsync(dto.Email, otp);

            return Ok(new
            {
                key = returnedKey,
                success = true,
                message = "OTP sent successfully",
                next_resend_wait_seconds = next,
                mock_otp = mockOtp
            });
        }


        [HttpPost("forgetpassword/resendcode")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendCode([FromBody] ResendOtpDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, message, mockOtp) = await _repo.ResendForgetPasswordAsync(dto.Email, dto.Key);
            if (!success) return BadRequest(new { error = message });

            return Ok(new { key = dto.Key, message, success = true, mock_otp = mockOtp });
        }

        [HttpPost("forgetpassword/codevalidate")]
        [AllowAnonymous]
        public async Task<IActionResult> CodeValidate([FromBody] ValidateOtpDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Hash the OTP using UTF-8 (matching what was stored in DB)
            var codeHash = _reset.HashOtp(dto.Code);
            var (success, message) = await _repo.ValidateForgetPasswordCodeAsync(dto.Email, dto.Key, codeHash);
            if (!success) return BadRequest(new { error = message });

            return Ok(new { key = dto.Key, success = true, message = "OTP validated successfully" });
        }

        [HttpPost("forgetpassword/password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { error = "Passwords do not match" });

            // ✅ BCrypt hash
            string newPasswordHash = _passwordService.Hash(dto.NewPassword);

            var (success, message) =
                await _repo.ResetPasswordAsync(dto.Email, dto.Key, newPasswordHash);

            if (!success)
                return BadRequest(new { error = message });

            return Ok(new
            {
                success = true,
                message = "Password updated successfully. Please login with new password."
            });
        }


        // ----------------------------- DEBUG: TEST PASSWORD VERIFICATION -----------------------------
        [HttpPost("debug-test-password")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugTestPassword([FromBody] LoginRequestDto req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            UserRecord? user = null;

            // Get user from database
            using (var conn = new SqlConnection(_cfg.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("sp_Auth_Login", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Email", req.Email);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new UserRecord
                            {
                                UserID = reader.GetInt32(reader.GetOrdinal("UserID")),
                                FullName = reader.GetString(reader.GetOrdinal("FullName")),
                                Email = reader.GetString(reader.GetOrdinal("Email")),
                                Role = reader.GetString(reader.GetOrdinal("Role")),
                                PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash"))
                            };
                        }
                    }
                }
            }

            if (user == null)
                return NotFound(new { error = "User not found" });

            // Test verification with detailed debugging
            bool verifyResult = false;
            string errorMessage = "";
            string debugInfo = "";

            try
            {
                // Generate a new hash with the same password to compare
                var testHash = _passwordService.Hash(req.Password);

                verifyResult = _passwordService.Verify(req.Password, user.PasswordHash);

                // Additional debug info
                debugInfo = $"Test hash length: {testHash.Length}, Stored hash length: {user.PasswordHash?.Length ?? 0}";

                // Check if hashes match character by character (first 5 chars for preview)
                if (user.PasswordHash != null && testHash.Length == user.PasswordHash.Length)
                {
                    int matchingChars = 0;
                    for (int i = 0; i < Math.Min(5, user.PasswordHash.Length); i++)
                    {
                        if (testHash[i] == user.PasswordHash[i])
                            matchingChars++;
                    }
                    debugInfo += $", First 5 chars match: {matchingChars}/5";
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                debugInfo = $"Exception: {ex.GetType().Name}";
            }

            // Debug info
            return Ok(new
            {
                email = user.Email,
                passwordProvided = req.Password,
                storedHashLength = user.PasswordHash?.Length ?? 0,
                storedHashIsNull = user.PasswordHash == null,
                storedHashIsEmpty = string.IsNullOrEmpty(user.PasswordHash),
                verificationResult = verifyResult,
                errorMessage = errorMessage,
                debugInfo = debugInfo,
                hashFormat = user.PasswordHash != null
                    ? (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$")
                        ? "BCrypt"
                        : user.PasswordHash.Length == 64 && Regex.IsMatch(user.PasswordHash, @"^[0-9A-Fa-f]{64}$")
                            ? "SHA-256 Hex"
                            : "Unicode PBKDF2")
                    : "NULL"
            });
        }
    }
}