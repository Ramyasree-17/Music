using Microsoft.Data.SqlClient;

public class BrandingSmtpService
{
    private readonly IConfiguration _cfg;

    public BrandingSmtpService(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    public async Task<SmtpConfig?> GetByDomain(string domain)
    {
        using var conn = new SqlConnection(
            _cfg.GetConnectionString("DefaultConnection"));

        using var cmd = new SqlCommand(@"
            SELECT TOP 1
                s.SmtpHost,
                s.SmtpPort,
                s.SmtpUsername,
                s.SmtpPassword,
                s.FromEmail,
                s.FromName,
                s.EnableSSL
            FROM BrandingSMTPSettings s
            JOIN Branding b ON s.BrandingId = b.Id
            WHERE b.DomainName = @Domain
              AND s.IsActive = 1
              AND b.IsActive = 1", conn);

        cmd.Parameters.AddWithValue("@Domain", domain);

        await conn.OpenAsync();

        using var r = await cmd.ExecuteReaderAsync();

        if (!r.Read()) return null;

        return new SmtpConfig
        {
            Host = r["SmtpHost"].ToString(),
            Port = (int)r["SmtpPort"],
            Username = r["SmtpUsername"].ToString(),
            Password = r["SmtpPassword"].ToString(),
            FromEmail = r["FromEmail"].ToString(),
            FromName = r["FromName"].ToString(),
            EnableSSL = (bool)r["EnableSSL"]
        };
    }
}
