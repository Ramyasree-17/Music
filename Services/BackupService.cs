// File: Services/BackupService.cs
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TunewaveAPIDB1.Services
{
    public record BackupJob(int FileId, string S3Key, string FileName, string Provider);

    public interface IBackupService
    {
        Task EnqueueBackupJobAsync(int fileId, string s3Key, string fileName);
        Task<int> BackupAllFilesAsync(CancellationToken cancellationToken = default);
        Task UpdateBackupUrlAsync(int fileId, string backupUrl);
    }

    /// <summary>
    /// Enqueues backup jobs into an in-memory channel. Processing is performed by BackupWorker.
    /// </summary>
    public class BackupService : IBackupService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;
        private readonly string _connStr;
        private readonly Channel<BackupJob> _channel;
        private readonly int _channelCapacity = 10000;

        public BackupService(
            IConfiguration configuration,
            ILogger<BackupService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connStr = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

            // Bounded channel to avoid unbounded memory growth
            _channel = Channel.CreateBounded<BackupJob>(new BoundedChannelOptions(_channelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
        }

        /// <summary>
        /// Enqueue a backup job to the in-process channel. Fast, non-blocking (unless channel full).
        /// </summary>
        public async Task EnqueueBackupJobAsync(int fileId, string s3Key, string fileName)
        {
            var backupEnabled = _configuration.GetValue<bool>("Backup:Enabled", false);
            if (!backupEnabled)
            {
                _logger.LogInformation("Backup disabled. Skipping enqueue for FileId={FileId}", fileId);
                return;
            }

            var provider = _configuration["Backup:Provider"] ?? "GoogleDrive";

            // Safe filename extraction (in case caller provided S3Key incorrectly)
            var safeFileName = string.IsNullOrWhiteSpace(fileName)
                ? Path.GetFileName(s3Key ?? $"file_{fileId}")
                : fileName;

            var parts = safeFileName.Split(new[] { '_' }, 2);
            safeFileName = parts.Length == 2 ? parts[1] : safeFileName;

            var job = new BackupJob(fileId, s3Key ?? string.Empty, safeFileName, provider);

            _logger.LogDebug("Queueing backup job FileId={FileId} Provider={Provider}", fileId, provider);

            // Wait async if channel is full
            await _channel.Writer.WriteAsync(job);
        }

        /// <summary>
        /// Called by the worker to read the next job.
        /// </summary>
        internal ChannelReader<BackupJob> GetReader() => _channel.Reader;

        public void Dispose()
        {
            _channel.Writer.TryComplete();
        }

        /// <summary>
        /// Bulk backup helper: enumerates files that are missing BackupUrl and enqueues them.
        /// Returns number of jobs enqueued (not necessarily processed yet).
        /// </summary>
        public async Task<int> BackupAllFilesAsync(CancellationToken cancellationToken = default)
        {
            var enqueued = 0;

            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync(cancellationToken);

                const string getFilesSql = @"
                    SELECT FileId, S3Key
                    FROM Files
                    WHERE Status = 'AVAILABLE'
                      AND BackupUrl IS NULL
                      AND S3Key IS NOT NULL
                    ORDER BY CreatedAt ASC;";

                await using var cmd = new SqlCommand(getFilesSql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var fileId = reader.GetInt32(0);
                    var s3Key = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var fileName = Path.GetFileName(s3Key);
                    var parts = fileName.Split(new[] { '_' }, 2);
                    fileName = parts.Length == 2 ? parts[1] : fileName;

                    await EnqueueBackupJobAsync(fileId, s3Key, fileName);
                    enqueued++;
                }

                await reader.CloseAsync();
                _logger.LogInformation("Enqueued {Count} backup jobs.", enqueued);
                return enqueued;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueuing bulk backup jobs.");
                throw;
            }
        }

        /// <summary>
        /// Updates BackupUrl in DB after successful upload.
        /// </summary>
        public async Task UpdateBackupUrlAsync(int fileId, string backupUrl)
        {
            if (string.IsNullOrWhiteSpace(backupUrl)) throw new ArgumentNullException(nameof(backupUrl));

            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                const string sql = @"
                    UPDATE Files
                    SET BackupUrl = @BackupUrl
                    WHERE FileId = @FileId;";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@BackupUrl", System.Data.SqlDbType.NVarChar, 2000) { Value = backupUrl });
                cmd.Parameters.Add(new SqlParameter("@FileId", System.Data.SqlDbType.Int) { Value = fileId });

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                {
                    _logger.LogWarning("UpdateBackupUrlAsync affected 0 rows for FileId={FileId}. Possibly file doesn't exist.", fileId);
                }
                else
                {
                    _logger.LogInformation("Updated BackupUrl for FileId={FileId} -> {BackupUrl}", fileId, backupUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update BackupUrl for FileId={FileId}", fileId);
                throw;
            }
        }
    }
}
