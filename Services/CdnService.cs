using System.Security.Cryptography;
using System.Text;

namespace TunewaveAPIDB1.Services;

/// <summary>
/// Service for generating CloudFront CDN URLs from S3 keys and creating signed URLs for protected content.
/// </summary>
public class CdnService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CdnService> _logger;

    public CdnService(IConfiguration configuration, ILogger<CdnService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Maps S3 key to CloudFront public URL.
    /// Example: labels/3/releases/1006/tracks/1007/abc123_song.flac
    /// -> https://cdn.tunewave.in/labels/3/releases/1006/tracks/1007/abc123_song.flac
    /// </summary>
    public string GenerateCloudFrontUrl(string s3Key)
    {
        var cdnBaseUrl = _configuration["Cdn:CloudFrontBaseUrl"] 
            ?? "https://cdn.tunewave.in";
        
        // Remove leading slash if present
        var cleanS3Key = s3Key.TrimStart('/');
        
        // Build CloudFront URL
        var cloudfrontUrl = $"{cdnBaseUrl.TrimEnd('/')}/{cleanS3Key}";
        
        _logger.LogInformation("Generated CloudFront URL for S3 key {S3Key}: {CloudFrontUrl}", s3Key, cloudfrontUrl);
        
        return cloudfrontUrl;
    }

    /// <summary>
    /// Generates a signed CloudFront URL for protected content (requires CloudFront key pair).
    /// This is used for private/premium content that requires authentication.
    /// </summary>
    /// <param name="s3Key">The S3 key of the file</param>
    /// <param name="expiresInMinutes">URL expiration time in minutes (default: 60)</param>
    /// <returns>Signed CloudFront URL</returns>
    public string GenerateSignedUrl(string s3Key, int expiresInMinutes = 60)
    {
        var cdnBaseUrl = _configuration["Cdn:CloudFrontBaseUrl"] 
            ?? "https://cdn.tunewave.in";
        
        var keyPairId = _configuration["Cdn:CloudFrontKeyPairId"];
        var privateKeyPath = _configuration["Cdn:CloudFrontPrivateKeyPath"];
        
        // If CloudFront signing is not configured, return unsigned URL
        if (string.IsNullOrWhiteSpace(keyPairId) || string.IsNullOrWhiteSpace(privateKeyPath))
        {
            _logger.LogWarning("CloudFront signing not configured. Returning unsigned URL for {S3Key}", s3Key);
            return GenerateCloudFrontUrl(s3Key);
        }

        try
        {
            var cleanS3Key = s3Key.TrimStart('/');
            var resourceUrl = $"{cdnBaseUrl.TrimEnd('/')}/{cleanS3Key}";
            
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes);
            var epochExpires = expiresAt.ToUnixTimeSeconds();
            
            // Create policy statement
            var policy = $"{{\"Statement\":[{{\"Resource\":\"{resourceUrl}\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{epochExpires}}}}}}}]}}";
            
            // Read private key
            if (!System.IO.File.Exists(privateKeyPath))
            {
                _logger.LogWarning("CloudFront private key file not found at {Path}. Returning unsigned URL.", privateKeyPath);
                return GenerateCloudFrontUrl(s3Key);
            }

            var privateKeyContent = System.IO.File.ReadAllText(privateKeyPath);
            var privateKey = System.Security.Cryptography.RSA.Create();
            privateKey.ImportFromPem(privateKeyContent);
            
            // Sign the policy (CloudFront uses SHA-1 with PKCS1 padding)
            var policyBytes = Encoding.UTF8.GetBytes(policy);
            var signature = privateKey.SignData(policyBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            var signatureBase64 = Convert.ToBase64String(signature)
                .Replace('+', '-')
                .Replace('=', '_')
                .Replace('/', '~');
            
            // Build signed URL
            var signedUrl = $"{resourceUrl}?Expires={epochExpires}&Signature={signatureBase64}&Key-Pair-Id={keyPairId}";
            
            _logger.LogInformation("Generated signed CloudFront URL for {S3Key}, expires in {Minutes} minutes", s3Key, expiresInMinutes);
            
            return signedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for {S3Key}. Returning unsigned URL.", s3Key);
            return GenerateCloudFrontUrl(s3Key);
        }
    }

    /// <summary>
    /// Determines if a file should use signed URLs based on file type or release settings.
    /// </summary>
    public bool RequiresSignedUrl(byte fileTypeId, int? releaseId = null)
    {
        // Audio files (type 1) typically require signed URLs for protected content
        // You can extend this logic based on release settings, subscription tiers, etc.
        return fileTypeId == 1; // Audio files
    }
}

