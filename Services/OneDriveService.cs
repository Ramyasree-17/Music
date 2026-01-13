using System.Text;
using System.Text.Json;

namespace TunewaveAPIDB1.Services;

/// <summary>
/// Service for uploading files to Microsoft OneDrive/SharePoint.
/// Handles authentication and file uploads to a specific folder.
/// </summary>
public class OneDriveService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OneDriveService> _logger;
    private readonly HttpClient _httpClient;

    public OneDriveService(
        IConfiguration configuration,
        ILogger<OneDriveService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Uploads a file to OneDrive in the configured folder.
    /// </summary>
    public async Task<string> UploadFileAsync(string fileName, Stream fileStream, string contentType)
    {
        var accessToken = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("OneDrive access token not available. Please configure OneDrive credentials.");
        }

        var folderPath = _configuration["OneDrive:FolderPath"] ?? "/Releases";
        var siteId = _configuration["OneDrive:SiteId"];
        var driveId = _configuration["OneDrive:DriveId"];

        try
        {
            // Construct Microsoft Graph API URL
            string uploadUrl;
            
            if (!string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(driveId))
            {
                // SharePoint/OneDrive for Business
                uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:{folderPath}/{fileName}:/content";
            }
            else
            {
                // Personal OneDrive
                uploadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:{folderPath}/{fileName}:/content";
            }

            // Upload file
            var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = new StreamContent(fileStream)
            };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var uploadResult = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var webUrl = uploadResult.TryGetProperty("webUrl", out var webLink) 
                ? webLink.GetString() 
                : uploadResult.TryGetProperty("@microsoft.graph.downloadUrl", out var downloadLink)
                    ? downloadLink.GetString()
                    : uploadUrl;

            _logger.LogInformation("File uploaded to OneDrive. Link: {Link}", webUrl);

            return webUrl ?? uploadUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} to OneDrive", fileName);
            throw;
        }
    }

    /// <summary>
    /// Downloads a file from source URL and uploads to OneDrive.
    /// </summary>
    public async Task<string> UploadFileFromUrlAsync(string fileName, string sourceUrl, string contentType)
    {
        try
        {
            // Download file from source URL
            _logger.LogInformation("Downloading file from {SourceUrl}", sourceUrl);
            var response = await _httpClient.GetAsync(sourceUrl);
            response.EnsureSuccessStatusCode();

            using var fileStream = await response.Content.ReadAsStreamAsync();
            
            // Upload to OneDrive
            return await UploadFileAsync(fileName, fileStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file from URL {SourceUrl} to OneDrive", sourceUrl);
            throw;
        }
    }

    /// <summary>
    /// Gets OneDrive access token using OAuth.
    /// </summary>
    private async Task<string?> GetAccessTokenAsync()
    {
        // Option 1: Access Token (if provided directly)
        var accessToken = _configuration["OneDrive:AccessToken"];
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        // Option 2: Refresh Token (recommended)
        var refreshToken = _configuration["OneDrive:RefreshToken"];
        var clientId = _configuration["OneDrive:ClientId"];
        var clientSecret = _configuration["OneDrive:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(refreshToken) && !string.IsNullOrWhiteSpace(clientId))
        {
            return await GetOAuthTokenAsync(refreshToken, clientId, clientSecret);
        }

        _logger.LogWarning("OneDrive credentials not configured. Please set up OAuth credentials.");
        return null;
    }

    /// <summary>
    /// Gets access token using OAuth refresh token.
    /// </summary>
    private async Task<string?> GetOAuthTokenAsync(string refreshToken, string clientId, string? clientSecret = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://login.microsoftonline.com/common/oauth2/v2.0/token");
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("scope", "Files.ReadWrite offline_access")
            });

            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("scope", "Files.ReadWrite offline_access")
                });
            }

            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            return tokenResponse.GetProperty("access_token").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OAuth access token");
            return null;
        }
    }
}



























