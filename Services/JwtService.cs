using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TunewaveAPIDB1.Models;

public class JwtService
{
    private readonly IConfiguration _cfg;

    public JwtService(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    public string GenerateToken(
        UserRecord user,
        int? enterpriseId = null,
        int? labelId = null,
        string? domain = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("FullName", user.FullName ?? "")
        };

        if (enterpriseId != null)
            claims.Add(new Claim("EnterpriseId", enterpriseId.ToString()!));

        if (labelId != null)
            claims.Add(new Claim("LabelId", labelId.ToString()!));

        if (!string.IsNullOrWhiteSpace(domain))
            claims.Add(new Claim("Domain", domain));

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
