public class UserRecord
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;  // ✅ string
    public string? PasswordSalt { get; set; }                  // ✅ string
    public int FailedAttempts { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Status { get; set; }
    public bool IsActive { get; set; }
}
