using System.Threading.Tasks;
using TunewaveAPIDB1.Models;

public interface IAuthRepository
{
    Task<(bool exists, string? displayName, string? email, string? role)> CheckEmailAsync(string email);

    Task<(int code, string message, int? userId, string? fullName,
        string? email, string? role, string? passwordHash)> LoginFullFlowAsync(string email);

    Task<UserRecord?> VerifyUserByEmailAsync(string email);

    Task<UserRecord?> GetUserByEmail(string email);  // ⭐ REQUIRED

    Task<(bool success, string key, int nextResend, string? mockOtp)>
        StartForgetPasswordAsync(string email, string key, string otpHash);

    Task<(bool success, string? message, string? mockOtp)>
        ResendForgetPasswordAsync(string email, string key);

    Task<(bool success, string? message)>
        ValidateForgetPasswordCodeAsync(string email, string key, string code);

    Task<(bool success, string? message)>
        ResetPasswordAsync(string email, string key, string newPasswordHash);
}
