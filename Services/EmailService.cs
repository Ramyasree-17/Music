using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TunewaveAPIDB1.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService>? _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService>? logger = null)
        {
            _configuration = configuration;
            _logger = logger;
        }


        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp, string fullName = "")
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var smtpUsername = _configuration["Email:SmtpUsername"];
                var smtpPassword = _configuration["Email:SmtpPassword"];
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@tunewave.in";
                var fromName = _configuration["Email:FromName"] ?? "Tunewave";

                if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger?.LogWarning("SMTP not configured. Email settings missing. OTP: {Otp} for {Email}", otp, toEmail);
                    return false;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "Password Reset OTP - Tunewave",
                    Body = GetOtpEmailBody(otp, fullName),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);

                _logger?.LogInformation("OTP email sent successfully to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
                return false;
            }
        }

        private string GetOtpEmailBody(string otp, string fullName)
        {
            var name = string.IsNullOrWhiteSpace(fullName) ? "User" : fullName;
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .otp-box {{ background-color: #fff; border: 2px solid #4CAF50; padding: 20px; text-align: center; margin: 20px 0; }}
        .otp-code {{ font-size: 32px; font-weight: bold; color: #4CAF50; letter-spacing: 5px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Tunewave</h1>
        </div>
        <div class=""content"">
            <h2>Password Reset OTP</h2>
            <p>Hello {name},</p>
            <p>You have requested to reset your password. Use the OTP code below to proceed:</p>
            <div class=""otp-box"">
                <div class=""otp-code"">{otp}</div>
            </div>
            <p>This OTP is valid for <strong>10 minutes</strong>.</p>
            <p>If you didn't request this, please ignore this email.</p>
        </div>
        <div class=""footer"">
            <p>&copy; 2025 Tunewave. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}

