using System.Text;
using Newtonsoft.Json;

namespace TunewaveAPIDB1.Services
{
    public class BrevoService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public BrevoService(IConfiguration configuration)
        {
            _apiKey = configuration["Brevo:ApiKey"] ?? throw new InvalidOperationException("Brevo:ApiKey is missing");
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public async Task SendEmail(string fromEmail, string fromName, string toEmail, string subject, string htmlContent)
        {
            var payload = new
            {
                sender = new { email = fromEmail, name = fromName },
                to = new[] { new { email = toEmail } },
                subject = subject,
                htmlContent = htmlContent
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.brevo.com/v3/smtp/email",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Brevo API error: {error}");
            }
        }
    }
}



