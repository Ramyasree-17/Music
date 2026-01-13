using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting;

namespace TunewaveAPIDB1.Services;

/// <summary>
/// Background worker that processes backup jobs by copying S3/CloudFront assets
/// to the configured backup provider (Google Drive or OneDrive) and updating
/// the corresponding Files.BackupUrl value.
/// </summary>
public class BackupWorker : BackgroundService
{
    private readonly BackupService _backupService;
    private readonly GoogleDriveService _googleDriveService;
    private readonly OneDriveService _oneDriveService;
    private readonly CdnService _cdnService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupWorker> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public BackupWorker(
        BackupService backupService,
        GoogleDriveService googleDriveService,
        OneDriveService oneDriveService,
        CdnService cdnService,
        IConfiguration configuration,
        ILogger<BackupWorker> logger)
    {
        _backupService = backupService;
        _googleDriveService = googleDriveService;
        _oneDriveService = oneDriveService;
        _cdnService = cdnService;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _backupService.GetReader();

        await foreach (var job in reader.ReadAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process backup job for FileId={FileId}", job.FileId);
            }
        }
    }

    private async Task ProcessJobAsync(BackupJob job, CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("Backup:Enabled", false))
        {
            _logger.LogDebug("Backup disabled while processing job. Skipping FileId={FileId}", job.FileId);
            return;
        }

        if (string.IsNullOrWhiteSpace(job.S3Key))
        {
            _logger.LogWarning("Skipping backup job because S3Key is missing. FileId={FileId}", job.FileId);
            return;
        }

        var contentType = ResolveContentType(job.FileName);
        var sourceUrl = BuildSourceUrl(job.S3Key);
        var provider = (job.Provider ?? "GoogleDrive").Trim();
        var backupUrl = provider.Equals("OneDrive", StringComparison.OrdinalIgnoreCase)
            ? await BackupToOneDriveAsync(job, sourceUrl, contentType, cancellationToken)
            : await BackupToGoogleDriveAsync(job, sourceUrl, contentType, cancellationToken);

        if (string.IsNullOrWhiteSpace(backupUrl))
        {
            _logger.LogWarning("Backup provider returned empty URL for FileId={FileId}", job.FileId);
            return;
        }

        await _backupService.UpdateBackupUrlAsync(job.FileId, backupUrl);
    }

    private async Task<string?> BackupToGoogleDriveAsync(
        BackupJob job,
        string sourceUrl,
        string contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Uploading FileId={FileId} to Google Drive from {SourceUrl}", job.FileId, sourceUrl);
            return await _googleDriveService.UploadFileFromUrlAsync(job.FileName, sourceUrl, contentType);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Google Drive upload failed for FileId={FileId}", job.FileId);
            return null;
        }
    }

    private async Task<string?> BackupToOneDriveAsync(
        BackupJob job,
        string sourceUrl,
        string contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Uploading FileId={FileId} to OneDrive from {SourceUrl}", job.FileId, sourceUrl);
            return await _oneDriveService.UploadFileFromUrlAsync(job.FileName, sourceUrl, contentType);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "OneDrive upload failed for FileId={FileId}", job.FileId);
            return null;
        }
    }

    private string ResolveContentType(string fileName)
    {
        if (_contentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            return contentType;
        }

        return "application/octet-stream";
    }

    private string BuildSourceUrl(string s3KeyOrUrl)
    {
        if (s3KeyOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return s3KeyOrUrl;
        }

        return _cdnService.GenerateCloudFrontUrl(s3KeyOrUrl);
    }
}

