using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using TunewaveAPIDB1.Data;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Services;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/mail")]
    [Authorize]
    public class MailController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly BrevoService _brevoService;
        private readonly IConfiguration _configuration;

        public MailController(
            ApplicationDbContext context,
            BrevoService brevoService,
            IConfiguration configuration)
        {
            _context = context;
            _brevoService = brevoService;
            _configuration = configuration;
        }

        // ✅ MAIN SEND METHOD
        [HttpPost("send")]
        public async Task<IActionResult> SendMail([FromBody] SendMailRequestDto request)
        {
            var brandingClaim = User.FindFirst("BrandingId");

            Branding? branding = null;

            if (brandingClaim != null)
            {
                int brandingId = int.Parse(brandingClaim.Value);

                branding = await _context.Brandings
                    .FirstOrDefaultAsync(x => x.Id == brandingId && x.IsActive);
            }

            string fromEmail = "gopi@twsd.me";   // Fixed sender email
            string fromName;

            // ✅ If branding exists and has domain
            if (branding != null && !string.IsNullOrWhiteSpace(branding.DomainName))
            {
                fromName = branding.DomainName;
            }
            else
            {
                // ✅ Default TuneWave
                fromName = "TuneWave Music";
            }

            try
            {
                await _brevoService.SendEmail(
                    fromEmail,
                    fromName,
                    request.ToEmail,
                    request.Subject,
                    request.HtmlContent
                );

                return Ok($"Mail sent from {fromName} <{fromEmail}>");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        // ✅ Legacy method
        [HttpPost("send-legacy")]
        [Obsolete("Use /send endpoint instead")]
        public async Task<IActionResult> SendMailLegacy([FromBody] BrevoSendMailRequest request)
        {
            var apiKey = _configuration["Brevo:ApiKey"];

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
