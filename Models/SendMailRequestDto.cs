namespace TunewaveAPIDB1.Models
{
    public class SendMailRequestDto
    {
        public string ToEmail { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string HtmlContent { get; set; } = string.Empty;
    }
}
