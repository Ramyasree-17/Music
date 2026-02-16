using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.Data;
using System.Security.Claims;
using System.Text;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/mail")]
    [ApiExplorerSettings(GroupName = "Mail")]
    [Authorize]
    public class MailController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly string _connStr;

        public MailController(IConfiguration config)
        {
            _config = config;
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        // ✅ FINAL SendMail METHOD (WORKING)
        [HttpPost("send")]
        public async Task<IActionResult> SendMail([FromBody] SendMailRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // =====================================================
                // STEP 1: DEFAULT VALUES
                // =====================================================

                string senderName = "tunewave";          // Default fallback
                string senderEmail = "gopi@twsd.me";     // Verified Brevo sender

                // =====================================================
                // STEP 2: GET BrandingId FROM JWT
                // =====================================================

                var brandingIdClaim = User.FindFirst("BrandingId")?.Value;

                if (!string.IsNullOrEmpty(brandingIdClaim) &&
                    int.TryParse(brandingIdClaim, out int brandingId))
                {
                    // =====================================================
                    // STEP 3: FETCH DOMAIN NAME FROM BRANDING TABLE
                    // =====================================================

                    using var conn = new SqlConnection(_connStr);
                    await conn.OpenAsync();

                    var cmd = new SqlCommand(@"
                SELECT DomainName
                FROM Branding
                WHERE Id = @Id AND IsActive = 1", conn);

                    cmd.Parameters.AddWithValue("@Id", brandingId);

                    var result = await cmd.ExecuteScalarAsync();

                    if (result != null)
                    {
                        var domainName = result.ToString()?.Trim();

                        // Safety check
                        if (!string.IsNullOrEmpty(domainName) &&
                            domainName.ToLower() != "string")
                        {
                            senderName = domainName; // ✅ DOMAIN USED
                        }
                    }
                }

                // =====================================================
                // STEP 4: LOAD BREVO API KEY
                // =====================================================

                var apiKey = _config["Brevo:ApiKey"];

                if (string.IsNullOrEmpty(apiKey))
                    return StatusCode(500, new { error = "Brevo API Key missing" });

                // =====================================================
                // STEP 5: CREATE BREVO PAYLOAD
                // =====================================================

                var brevoBody = new
                {
                    sender = new
                    {
                        email = senderEmail,
                        name = senderName
                    },
                    to = new[]
                    {
                new { email = request.ToEmail }
            },
                    subject = request.Subject,
                    htmlContent = request.HtmlContent
                };

                var json = JsonConvert.SerializeObject(brevoBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("api-key", apiKey);

                var response = await client.PostAsync(
                    "https://api.brevo.com/v3/smtp/email",
                    content
                );

                var brevoResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        error = "Brevo mail send failed",
                        brevoResponse
                    });
                }

                // =====================================================
                // FINAL RESPONSE
                // =====================================================

                return Ok(new
                {
                    message = "Mail sent successfully",
                    senderNameUsed = senderName,
                    senderEmailUsed = senderEmail
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ✅ OLD: Keep for backward compatibility (if needed)
        [HttpPost("send-legacy")]
        [Obsolete("Use /send endpoint instead")]
        public async Task<IActionResult> SendMailLegacy([FromBody] BrevoSendMailRequest request)
        {
            var apiKey = _config["Brevo:ApiKey"];

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var brevoBody = new
            {
                sender = new
                {
                    name = request.FromName,
                    email = request.FromEmail
                },
                to = new[]
                {
                    new
                    {
                        email = request.ToEmail,
                        name = request.ToName
                    }
                },
                subject = request.Subject,
                htmlContent = request.HtmlContent
            };

            var json = JsonConvert.SerializeObject(brevoBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                "https://api.brevo.com/v3/smtp/email",
                content
            );

            var result = await response.Content.ReadAsStringAsync();

            return Ok(result);
        }
    }
}