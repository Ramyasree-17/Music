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
    [Tags("Branding")]
    public class BrandingController : ControllerBase
    {
        private readonly string _connStr;

        public BrandingController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        // =========================================================
        // 🔓 PUBLIC GET – Branding by Domain (Enterprise / Label)
        // =========================================================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetBrandingByDomain([FromQuery] string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                return BadRequest(new { error = "domainName is required" });

            using var conn = new SqlConnection(_connStr);
            using var cmd = new SqlCommand(@"
                SELECT TOP 1
                    Id AS BrandingId,
                    OwnerType,
                    OwnerId,
                    SiteName,
                    SiteDescription,
                    PrimaryColor,
                    SecondaryColor,
                    HeaderColor,
                    SidebarColor,
                    FooterColor,
                    LogoUrl,
                    FooterText,
                    FooterLinksJson
                FROM Branding
                WHERE DomainName = @DomainName
                  AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@DomainName", domainName);

            conn.Open();
            using var r = cmd.ExecuteReader();

            if (!r.Read())
            {
                r.Close();
                
                // ===============================
                // LABEL FALLBACK (AUTO-CREATE)
                // Check if label exists and create branding
                // ===============================
                using var labelCmd = new SqlCommand(@"
                    SELECT TOP 1
                        LabelID,
                        LabelName,
                        EnterpriseID,
                        OwnerEmail
                    FROM Labels
                    WHERE Domain = @DomainName
                      AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

                labelCmd.Parameters.AddWithValue("@DomainName", domainName);

                using var lr = labelCmd.ExecuteReader();

                if (lr.Read())
                {
                    int labelId = Convert.ToInt32(lr["LabelID"]);
                    int enterpriseId = Convert.ToInt32(lr["EnterpriseID"]);
                    string labelName = lr["LabelName"].ToString()!;
                    string? ownerEmail = lr["OwnerEmail"] as string;

                    lr.Close();

                    // 🔥 Auto-create branding for label (SAFE INSERT - NO DUPLICATES)
                    using var insertCmd = new SqlCommand(@"
                        IF NOT EXISTS (
                            SELECT 1
                            FROM Branding
                            WHERE EnterpriseId = @EnterpriseId
                              AND OwnerType = 'Label'
                              AND OwnerId = @LabelId
                        )
                        BEGIN
                            INSERT INTO Branding (
                                OwnerType,
                                OwnerId,
                                EnterpriseId,
                                DomainName,
                                SiteName,
                                SiteDescription,
                                ContactEmail,
                                PrimaryColor,
                                SecondaryColor,
                                HeaderColor,
                                SidebarColor,
                                FooterColor,
                                LogoUrl,
                                FooterText,
                                FooterLinksJson,
                                IsActive,
                                CreatedAt,
                                UpdatedAt
                            )
                            VALUES (
                                'Label',
                                @LabelId,
                                @EnterpriseId,
                                @DomainName,
                                @SiteName,
                                @SiteDescription,
                                @ContactEmail,
                                @PrimaryColor,
                                @SecondaryColor,
                                @HeaderColor,
                                @SidebarColor,
                                @FooterColor,
                                @LogoUrl,
                                @FooterText,
                                @FooterLinksJson,
                                1,
                                SYSUTCDATETIME(),
                                SYSUTCDATETIME()
                            )
                        END
                    ", conn);

                    insertCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    insertCmd.Parameters.AddWithValue("@LabelId", labelId);
                    insertCmd.Parameters.AddWithValue("@DomainName", domainName);
                    insertCmd.Parameters.AddWithValue("@SiteName", labelName);
                    insertCmd.Parameters.AddWithValue("@SiteDescription", BrandingDefaults.SiteDescription);
                    insertCmd.Parameters.AddWithValue("@ContactEmail",
                        string.IsNullOrWhiteSpace(ownerEmail) ? DBNull.Value : ownerEmail);

                    insertCmd.Parameters.AddWithValue("@PrimaryColor", BrandingDefaults.PrimaryColor);
                    insertCmd.Parameters.AddWithValue("@SecondaryColor", BrandingDefaults.SecondaryColor);
                    insertCmd.Parameters.AddWithValue("@HeaderColor", BrandingDefaults.HeaderColor);
                    insertCmd.Parameters.AddWithValue("@SidebarColor", BrandingDefaults.SidebarColor);
                    insertCmd.Parameters.AddWithValue("@FooterColor", BrandingDefaults.FooterColor);
                    insertCmd.Parameters.AddWithValue("@LogoUrl", BrandingDefaults.DefaultLogo);
                    insertCmd.Parameters.AddWithValue("@FooterText", BrandingDefaults.FooterText);
                    insertCmd.Parameters.AddWithValue("@FooterLinksJson",
                        JsonSerializer.Serialize(BrandingDefaults.FooterLinks));

                    insertCmd.ExecuteNonQuery();

                    // 🔁 FINAL FETCH - Query the branding (newly created or existing)
                    using var finalCmd = new SqlCommand(@"
                        SELECT TOP 1
                            Id AS BrandingId,
                            OwnerType,
                            OwnerId,
                            SiteName,
                            SiteDescription,
                            PrimaryColor,
                            SecondaryColor,
                            HeaderColor,
                            SidebarColor,
                            FooterColor,
                            LogoUrl,
                            FooterText,
                            FooterLinksJson
                        FROM Branding
                        WHERE DomainName = @DomainName
                          AND IsActive = 1", conn);

                    finalCmd.Parameters.AddWithValue("@DomainName", domainName);

                    using var finalR = finalCmd.ExecuteReader();
                    if (finalR.Read())
                    {
                        // 🔐 Set cookie based on owner
                        var finalOwnerType = finalR["OwnerType"]?.ToString();
                        if (finalOwnerType == "Enterprise")
                        {
                            Response.Cookies.Append("enterprise_id", finalR["OwnerId"].ToString()!, new CookieOptions
                            {
                                HttpOnly = true,
                                SameSite = SameSiteMode.Lax,
                                Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                            });
                        }
                        else if (finalOwnerType == "Label")
                        {
                            Response.Cookies.Append("label_id", finalR["OwnerId"].ToString()!, new CookieOptions
                            {
                                HttpOnly = true,
                                SameSite = SameSiteMode.Lax,
                                Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                            });
                        }

                        return Ok(new
                        {
                            brandingId = finalR["BrandingId"],
                            ownerType = finalOwnerType,
                            ownerId = finalR["OwnerId"],

                            site = new
                            {
                                name = finalR["SiteName"]?.ToString() ?? BrandingDefaults.SiteName,
                                description = finalR["SiteDescription"]?.ToString() ?? BrandingDefaults.SiteDescription
                            },

                            colors = new
                            {
                                primary = finalR["PrimaryColor"]?.ToString() ?? BrandingDefaults.PrimaryColor,
                                secondary = finalR["SecondaryColor"]?.ToString() ?? BrandingDefaults.SecondaryColor,
                                header = finalR["HeaderColor"]?.ToString() ?? BrandingDefaults.HeaderColor,
                                sidebar = finalR["SidebarColor"]?.ToString() ?? BrandingDefaults.SidebarColor,
                                footer = finalR["FooterColor"]?.ToString() ?? BrandingDefaults.FooterColor
                            },

                            logoUrl = finalR["LogoUrl"]?.ToString() ?? BrandingDefaults.DefaultLogo,

                            footer = new
                            {
                                text = finalR["FooterText"]?.ToString() ?? BrandingDefaults.FooterText,
                                links = finalR["FooterLinksJson"] != DBNull.Value
                                    ? JsonSerializer.Deserialize<object>(finalR["FooterLinksJson"].ToString()!)
                                    : BrandingDefaults.FooterLinks
                            }
                        });
                    }
                }

                return NotFound(new { error = "Branding not found for this domain" });
            }

            // 🔐 Set cookie based on owner
            var ownerType = r["OwnerType"]?.ToString();
            if (ownerType == "Enterprise")
            {
                Response.Cookies.Append("enterprise_id", r["OwnerId"].ToString()!, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                });
            }
            else if (ownerType == "Label")
            {
                Response.Cookies.Append("label_id", r["OwnerId"].ToString()!, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(30)
                });
            }

            return Ok(new
            {
                brandingId = r["BrandingId"],
                ownerType = ownerType,
                ownerId = r["OwnerId"],

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
                    links = r["FooterLinksJson"] != DBNull.Value
                        ? JsonSerializer.Deserialize<object>(r["FooterLinksJson"].ToString()!)
                        : BrandingDefaults.FooterLinks
                }
            });
        }

        // =========================================================
        // 🔐 PROTECTED PUT – Update Branding (Enterprise / Label)
        // =========================================================
        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateBranding(
            [FromBody] UpdateEnterpriseBrandingDto dto)
        {
            string? ownerType = null;
            int? ownerId = null;

            if (Request.Cookies.TryGetValue("enterprise_id", out var eid))
            {
                ownerType = "Enterprise";
                ownerId = int.Parse(eid);
            }
            else if (Request.Cookies.TryGetValue("label_id", out var lid))
            {
                ownerType = "Label";
                ownerId = int.Parse(lid);
            }
            else
            {
                return Unauthorized(new { error = "Domain context missing" });
            }

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
                WHERE OwnerType = @OwnerType
                  AND OwnerId = @OwnerId
                  AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@OwnerType", ownerType);
            cmd.Parameters.AddWithValue("@OwnerId", ownerId);
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

            return Ok(new
            {
                status = "success",
                message = "Branding updated successfully"
            });
        }
    }
}
