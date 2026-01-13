using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace TunewaveAPIDB1.Services
{
    /// <summary>
    /// Background service that checks for overdue invoices and logs warnings
    /// at different thresholds (30, 60, 90 days) before the 105-day suspension threshold.
    /// This provides early warning notifications without suspending enterprises.
    /// Runs daily to monitor invoice payment status.
    /// </summary>
    public class OverdueInvoiceCheckerService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<OverdueInvoiceCheckerService> _logger;
        private readonly string _connStr;

        // Warning thresholds (days overdue)
        private const int WARNING_THRESHOLD_30 = 30;
        private const int WARNING_THRESHOLD_60 = 60;
        private const int WARNING_THRESHOLD_90 = 90;

        public OverdueInvoiceCheckerService(
            IConfiguration config,
            ILogger<OverdueInvoiceCheckerService> logger)
        {
            _config = config;
            _logger = logger;
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OverdueInvoiceCheckerService started. Will check for overdue invoices daily.");

            // Wait a bit on startup before first check (allows app to fully initialize)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckOverdueInvoicesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("OverdueInvoiceCheckerService is stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OverdueInvoiceCheckerService. Will retry in 24 hours.");
                }

                // Run once every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }

            _logger.LogInformation("OverdueInvoiceCheckerService stopped.");
        }

        private async Task CheckOverdueInvoicesAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting daily check for overdue invoices");

            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync(cancellationToken);

                // Find enterprises with overdue invoices at different thresholds
                await using var findCmd = new SqlCommand(@"
                    SELECT DISTINCT 
                        e.EnterpriseID,
                        e.EnterpriseName,
                        e.OwnerEmail,
                        e.Status,
                        MIN(i.DueDate) AS OldestOverdueDate,
                        MAX(DATEDIFF(DAY, i.DueDate, GETDATE())) AS MaxOverdueDays,
                        COUNT(DISTINCT i.InvoiceId) AS OverdueInvoiceCount,
                        SUM(i.TotalAmount) AS TotalOverdueAmount
                    FROM Enterprises e
                    INNER JOIN Invoices i ON 
                        i.TenantType = 'Enterprise' 
                        AND i.TenantId = e.EnterpriseID
                    WHERE e.Status = 'Active'
                      AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                      AND i.Status <> 'Paid'
                      AND i.DueDate IS NOT NULL
                      AND DATEDIFF(DAY, i.DueDate, GETDATE()) >= @MinThreshold
                      AND DATEDIFF(DAY, i.DueDate, GETDATE()) < 105
                    GROUP BY e.EnterpriseID, e.EnterpriseName, e.OwnerEmail, e.Status
                    HAVING MAX(DATEDIFF(DAY, i.DueDate, GETDATE())) >= @MinThreshold
                      AND MAX(DATEDIFF(DAY, i.DueDate, GETDATE())) < 105", conn);

                // Check 30-day threshold
                findCmd.Parameters.AddWithValue("@MinThreshold", WARNING_THRESHOLD_30);
                var overdue30Days = await GetOverdueEnterprisesAsync(findCmd, cancellationToken);

                // Check 60-day threshold
                findCmd.Parameters["@MinThreshold"].Value = WARNING_THRESHOLD_60;
                var overdue60Days = await GetOverdueEnterprisesAsync(findCmd, cancellationToken);

                // Check 90-day threshold
                findCmd.Parameters["@MinThreshold"].Value = WARNING_THRESHOLD_90;
                var overdue90Days = await GetOverdueEnterprisesAsync(findCmd, cancellationToken);

                // Log warnings for each threshold
                LogOverdueWarnings(overdue30Days, WARNING_THRESHOLD_30);
                LogOverdueWarnings(overdue60Days, WARNING_THRESHOLD_60);
                LogOverdueWarnings(overdue90Days, WARNING_THRESHOLD_90);

                var totalWarnings = overdue30Days.Count + overdue60Days.Count + overdue90Days.Count;
                if (totalWarnings > 0)
                {
                    _logger.LogWarning(
                        "Found {TotalCount} enterprise(s) with overdue invoices requiring attention (30-104 days overdue)",
                        totalWarnings);
                }
                else
                {
                    _logger.LogInformation("No enterprises found with overdue invoices in warning range (30-104 days).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during overdue invoice check");
                throw;
            }
        }

        private async Task<List<(int EnterpriseId, string EnterpriseName, string? OwnerEmail, int OverdueDays, int InvoiceCount, decimal TotalAmount)>> 
            GetOverdueEnterprisesAsync(SqlCommand cmd, CancellationToken cancellationToken)
        {
            var results = new List<(int, string, string?, int, int, decimal)>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add((
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader["OwnerEmail"] == DBNull.Value ? null : reader["OwnerEmail"].ToString(),
                    reader.GetInt32(5),
                    reader.GetInt32(6),
                    reader.GetDecimal(7)
                ));
            }

            await reader.CloseAsync();
            return results;
        }

        private void LogOverdueWarnings(
            List<(int EnterpriseId, string EnterpriseName, string? OwnerEmail, int OverdueDays, int InvoiceCount, decimal TotalAmount)> enterprises,
            int threshold)
        {
            foreach (var (enterpriseId, enterpriseName, ownerEmail, overdueDays, invoiceCount, totalAmount) in enterprises)
            {
                if (overdueDays >= threshold && overdueDays < threshold + 7) // Log within 7 days of threshold
                {
                    _logger.LogWarning(
                        "⚠️ OVERDUE INVOICE WARNING: Enterprise {EnterpriseId} ({EnterpriseName}) has {InvoiceCount} invoice(s) overdue by {OverdueDays} days. Total overdue amount: {TotalAmount:C}. Owner: {OwnerEmail}",
                        enterpriseId, enterpriseName, invoiceCount, overdueDays, totalAmount, ownerEmail ?? "N/A");
                }
            }
        }
    }
}
