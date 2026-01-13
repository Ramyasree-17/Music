using System.Text;
using System.Text.Json;

namespace TunewaveAPIDB1.Services;

/// <summary>
/// Service for uploading files to Google Drive.
/// Handles authentication and file uploads to a specific folder.
/// </summary>
public class GoogleDriveService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleDriveService> _logger;
    private readonly HttpClient _httpClient;

    public GoogleDriveService(
        IConfiguration configuration,
        ILogger<GoogleDriveService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Uploads a file to Google Drive in the configured folder.
    /// </summary>
    public async Task<string> UploadFileAsync(string fileName, Stream fileStream, string contentType)
    {
        var accessToken = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Google Drive access token not available. Please configure Google Drive credentials.");
        }

        var folderId = _configuration["GoogleDrive:FolderId"];
        if (string.IsNullOrWhiteSpace(folderId))
        {
            _logger.LogWarning("GoogleDrive:FolderId not configured. Uploading to root folder.");
        }

        try
        {
            // Step 1: Create file metadata
            var metadata = new
            {
                name = fileName,
                parents = string.IsNullOrWhiteSpace(folderId) ? null : new[] { folderId }
            };

            var metadataJson = JsonSerializer.Serialize(metadata);
            var boundary = Guid.NewGuid().ToString();
            var multipartContent = new MultipartFormDataContent(boundary);

            // Add metadata
            multipartContent.Add(new StringContent(metadataJson, Encoding.UTF8, "application/json"), "\"metadata\"");

            // Add file content
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            multipartContent.Add(fileContent, "\"file\"", fileName);

            // Step 2: Upload to Google Drive
            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart")
            {
                Content = multipartContent
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var uploadResult = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var fileId = uploadResult.GetProperty("id").GetString();
            var webViewLink = uploadResult.TryGetProperty("webViewLink", out var webLink) 
                ? webLink.GetString() 
                : $"https://drive.google.com/file/d/{fileId}/view";

            _logger.LogInformation("File uploaded to Google Drive. FileId: {FileId}, Link: {Link}", fileId, webViewLink);

            return webViewLink ?? $"https://drive.google.com/file/d/{fileId}/view";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} to Google Drive", fileName);
            throw;
        }
    }

    /// <summary>
    /// Downloads a file from S3 (or CloudFront) and uploads to Google Drive.
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
            
            // Upload to Google Drive
            return await UploadFileAsync(fileName, fileStream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file from URL {SourceUrl} to Google Drive", sourceUrl);
            throw;
        }
    }

    /// <summary>
    /// Gets Google Drive access token using service account or OAuth.
    /// </summary>
    private async Task<string?> GetAccessTokenAsync()
    {
        // Option 1: Service Account (Recommended for server-to-server)
        var serviceAccountEmail = _configuration["GoogleDrive:ServiceAccountEmail"];
        var serviceAccountKeyPath = _configuration["GoogleDrive:ServiceAccountKeyPath"];

        if (!string.IsNullOrWhiteSpace(serviceAccountEmail) && !string.IsNullOrWhiteSpace(serviceAccountKeyPath))
        {
            return await GetServiceAccountTokenAsync(serviceAccountEmail, serviceAccountKeyPath);
        }

        // Option 2: OAuth Access Token (if provided directly)
        var accessToken = _configuration["GoogleDrive:AccessToken"];
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            return accessToken;
        }

        // Option 3: OAuth Refresh Token
        var refreshToken = _configuration["GoogleDrive:RefreshToken"];
        var clientId = _configuration["GoogleDrive:ClientId"];
        var clientSecret = _configuration["GoogleDrive:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(refreshToken) && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            return await GetOAuthTokenAsync(refreshToken, clientId, clientSecret);
        }

        _logger.LogWarning("Google Drive credentials not configured. Please set up Service Account or OAuth credentials.");
        return null;
    }

    /// <summary>
    /// Gets access token using service account credentials.
    /// </summary>
    private async Task<string?> GetServiceAccountTokenAsync(string serviceAccountEmail, string keyPath)
    {
        // In production, use Google.Apis.Auth library:
        // var credential = GoogleCredential.FromFile(keyPath)
        //     .CreateScoped(new[] { "https://www.googleapis.com/auth/drive.file" });
        // return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

        _logger.LogInformation("Service account authentication not fully implemented. Please use Google.Apis.Auth NuGet package.");
        return null;
    }

    /// <summary>
    /// Gets access token using OAuth refresh token.
    /// </summary>
    private async Task<string?> GetOAuthTokenAsync(string refreshToken, string clientId, string clientSecret)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });
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



