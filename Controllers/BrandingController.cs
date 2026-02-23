using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using TunewaveAPIDB1.Common;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/branding")]
    public class BrandingController : ControllerBase
    {
        private readonly string _connStr;

        public BrandingController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        // =========================================================
        // 🔓 1️⃣ GET – Branding by Domain (PUBLIC)
        // =========================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetByDomain([FromQuery] string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                return BadRequest(new { error = "domainName is required" });

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT TOP 1 *
                FROM Branding
                WHERE DomainName = @DomainName
                  AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@DomainName", domainName);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new { error = "Branding not found" });

            return Ok(BuildBrandingResponse(reader));
        }

        // =========================================================
        // 🔓 2️⃣ GET – Branding by ID (PUBLIC)
        // =========================================================
        [HttpGet("{brandingId}")]
        public async Task<IActionResult> GetById(int brandingId)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(@"
        SELECT DomainName
        FROM Branding
        WHERE Id = @BrandingId
          AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@BrandingId", brandingId);

            var result = await cmd.ExecuteScalarAsync();

            if (result == null)
                return NotFound(new { error = "Branding not found" });

            return Ok(new
            {
                domainName = result.ToString()
            });
        }

        // =========================================================
        // 🔐 3️⃣ PUT – Update Branding (AUTHORIZED)
        // =========================================================
        [HttpPut("{brandingId}")]
        [Authorize]
        public async Task<IActionResult> UpdateBranding(
            int brandingId,
            [FromBody] UpdateEnterpriseBrandingDto dto)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(@"
                UPDATE Branding
                SET
                    SiteName = COALESCE(@SiteName, SiteName),
                    SiteDescription = COALESCE(@SiteDescription, SiteDescription),
                    PrimaryColor = COALESCE(@PrimaryColor, PrimaryColor),
                    SecondaryColor = COALESCE(@SecondaryColor, SecondaryColor),
                    HeaderColor = COALESCE(@HeaderColor, HeaderColor),
                    SidebarColor = COALESCE(@SidebarColor, SidebarColor),
                    FooterColor = COALESCE(@FooterColor, FooterColor),
                    LogoUrl = COALESCE(@LogoUrl, LogoUrl),
                    FooterText = COALESCE(@FooterText, FooterText),
                    FooterLinksJson = COALESCE(@FooterLinksJson, FooterLinksJson),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @BrandingId
                  AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@BrandingId", brandingId);
            cmd.Parameters.AddWithValue("@SiteName", (object?)dto.SiteName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SiteDescription", (object?)dto.SiteDescription ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrimaryColor", (object?)dto.PrimaryColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SecondaryColor", (object?)dto.SecondaryColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@HeaderColor", (object?)dto.HeaderColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SidebarColor", (object?)dto.SidebarColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FooterColor", (object?)dto.FooterColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LogoUrl", (object?)dto.LogoUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FooterText", (object?)dto.FooterText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FooterLinksJson", (object?)dto.FooterLinksJson ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
                return NotFound(new { error = "Branding not found" });

            return Ok(new { message = "Branding updated successfully" });
        }

        // =========================================================
        // 🔹 Helper Method
        // =========================================================
        private object BuildBrandingResponse(SqlDataReader r)
        {
            return new
            {
                brandingId = r["Id"],
                ownerType = r["OwnerType"]?.ToString(),
                ownerId = r["OwnerId"],
                domainName = r["DomainName"]?.ToString(),

                site = new
                {
                    name = r["SiteName"]?.ToString() ?? BrandingDefaults.SiteName,
                    description = r["SiteDescription"]?.ToString() ?? BrandingDefaults.SiteDescription
                },

                colors = new
                {
                    primary = r["PrimaryColor"]?.ToString() ?? BrandingDefaults.PrimaryColor,
                    secondary = r["SecondaryColor"]?.ToString() ?? BrandingDefaults.SecondaryColor,
                    header = r["HeaderColor"]?.ToString() ?? BrandingDefaults.HeaderColor,
                    sidebar = r["SidebarColor"]?.ToString() ?? BrandingDefaults.SidebarColor,
                    footer = r["FooterColor"]?.ToString() ?? BrandingDefaults.FooterColor
                },

                logoUrl = r["LogoUrl"]?.ToString() ?? BrandingDefaults.DefaultLogo,

                footer = new
                {
                    text = r["FooterText"]?.ToString() ?? BrandingDefaults.FooterText,
                    links = GetSafeFooterLinks(r["FooterLinksJson"]?.ToString())
                }
            };
        }

        // =========================================================
        // Helper Method: Safe Footer Links JSON Parsing
        // =========================================================
        private object GetSafeFooterLinks(string? footerJson)
        {
            // Default to BrandingDefaults if null or empty
            if (string.IsNullOrWhiteSpace(footerJson))
                return BrandingDefaults.FooterLinks;

            // Check if it's the bad value "string"
            if (footerJson.Trim().Equals("string", StringComparison.OrdinalIgnoreCase))
                return BrandingDefaults.FooterLinks;

            // Try to parse JSON
            try
            {
                // Check if it starts with '[' (array) or '{' (object)
                string trimmed = footerJson.Trim();
                if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
                {
                    return JsonSerializer.Deserialize<object>(trimmed) ?? BrandingDefaults.FooterLinks;
                }
            }
            catch (JsonException)
            {
                // Invalid JSON - return defaults
                return BrandingDefaults.FooterLinks;
            }
            catch (Exception)
            {
                // Any other error - return defaults
                return BrandingDefaults.FooterLinks;
            }

            // If we get here, it's not valid JSON format
            return BrandingDefaults.FooterLinks;
        }
    }
}
