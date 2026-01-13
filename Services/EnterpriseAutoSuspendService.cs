using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using System.Data;

namespace TunewaveAPIDB1.Services
{
    /// <summary>
    /// Background service that automatically suspends enterprises with unpaid invoices
    /// that are overdue by 105 days or more.
    /// Also stops Zoho recurring invoices before suspending enterprises.
    /// Runs daily and checks the Invoices table directly.
    /// </summary>
    public class EnterpriseAutoSuspendService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<EnterpriseAutoSuspendService> _logger;
        private readonly string _connStr;

        // Number of days after which an enterprise must be suspended (MANDATORY)
        private const int OVERDUE_DAYS_THRESHOLD = 105;

        public EnterpriseAutoSuspendService(
            IServiceProvider serviceProvider,
            IConfiguration config,
            ILogger<EnterpriseAutoSuspendService> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EnterpriseAutoSuspendService started. Will check for overdue invoices daily.");

            // Wait a bit on startup before first check (allows app to fully initialize)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SuspendEnterprisesWithOverdueInvoicesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("EnterpriseAutoSuspendService is stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EnterpriseAutoSuspendService. Will retry in 24 hours.");
                }

                // Run once every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }

            _logger.LogInformation("EnterpriseAutoSuspendService stopped.");
        }

        private async Task SuspendEnterprisesWithOverdueInvoicesAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting daily check for enterprises with overdue invoices (threshold: {Threshold} days)", 
                OVERDUE_DAYS_THRESHOLD);

            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync(cancellationToken);

                // Find enterprises with unpaid invoices overdue by 105+ days
                // Also get ZohoRecurringInvoiceId to stop recurring invoices
                await using var findCmd = new SqlCommand(@"
                    SELECT DISTINCT 
                        e.EnterpriseID,
                        e.EnterpriseName,
                        e.Status,
                        e.OwnerEmail,
                        e.ZohoRecurringInvoiceId,
                        MIN(i.DueDate) AS OldestOverdueDate,
                        MAX(DATEDIFF(DAY, i.DueDate, GETDATE())) AS MaxOverdueDays
                    FROM Enterprises e
                    INNER JOIN Invoices i ON 
                        i.TenantType = 'Enterprise' 
                        AND i.TenantId = e.EnterpriseID
                    WHERE e.Status <> 'Suspended'
                      AND e.Status <> 'Inactive'
                      AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                      AND i.Status <> 'Paid'
                      AND i.DueDate IS NOT NULL
                      AND DATEDIFF(DAY, i.DueDate, GETDATE()) >= @Threshold
                    GROUP BY e.EnterpriseID, e.EnterpriseName, e.Status, e.OwnerEmail, e.ZohoRecurringInvoiceId
                    HAVING MAX(DATEDIFF(DAY, i.DueDate, GETDATE())) >= @Threshold", conn);

                findCmd.Parameters.AddWithValue("@Threshold", OVERDUE_DAYS_THRESHOLD);

                var enterprisesToSuspend = new List<(int EnterpriseId, string EnterpriseName, string? RecurringInvoiceId, int MaxOverdueDays)>();

                await using var reader = await findCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    enterprisesToSuspend.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader["ZohoRecurringInvoiceId"] == DBNull.Value ? null : reader["ZohoRecurringInvoiceId"].ToString(),
                        reader.GetInt32(6)
                    ));
                }

                await reader.CloseAsync();

                _logger.LogInformation("Found {Count} enterprises with invoices overdue by {Threshold}+ days", 
                    enterprisesToSuspend.Count, OVERDUE_DAYS_THRESHOLD);

                // Get ZohoBooksService from DI
                using var scope = _serviceProvider.CreateScope();
                var zohoBooksService = scope.ServiceProvider.GetRequiredService<ZohoBooksService>();

                // Suspend each enterprise and stop recurring invoice
                foreach (var (enterpriseId, enterpriseName, recurringInvoiceId, maxOverdueDays) in enterprisesToSuspend)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await SuspendEnterpriseAndStopRecurringInvoiceAsync(
                            enterpriseId, 
                            enterpriseName, 
                            recurringInvoiceId,
                            maxOverdueDays, 
                            zohoBooksService,
                            conn, 
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error suspending enterprise {EnterpriseId} ({EnterpriseName})", 
                            enterpriseId, enterpriseName);
                    }
                }

                if (enterprisesToSuspend.Count > 0)
                {
                    _logger.LogWarning(
                        "Suspended {Count} enterprise(s) due to invoices overdue by {Threshold}+ days (MANDATORY RULE)",
                        enterprisesToSuspend.Count, OVERDUE_DAYS_THRESHOLD);
                }
                else
                {
                    _logger.LogInformation("No enterprises found requiring suspension. All invoices are within threshold.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enterprise auto-suspend check");
                throw;
            }
        }

        private async Task SuspendEnterpriseAndStopRecurringInvoiceAsync(
            int enterpriseId,
            string enterpriseName,
            string? recurringInvoiceId,
            int overdueDays,
            ZohoBooksService zohoBooksService,
            SqlConnection conn,
            CancellationToken cancellationToken)
        {
            try
            {
                // Step 1: Stop Zoho recurring invoice if it exists
                if (!string.IsNullOrWhiteSpace(recurringInvoiceId))
                {
                    _logger.LogInformation(
                        "Stopping Zoho recurring invoice {RecurringInvoiceId} for enterprise {EnterpriseId} ({EnterpriseName})",
                        recurringInvoiceId, enterpriseId, enterpriseName);

                    var stopSuccess = await zohoBooksService.StopRecurringInvoiceAsync(recurringInvoiceId);
                    if (stopSuccess)
                    {
                        _logger.LogInformation(
                            "Successfully stopped Zoho recurring invoice {RecurringInvoiceId} for enterprise {EnterpriseId}",
                            recurringInvoiceId, enterpriseId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to stop Zoho recurring invoice {RecurringInvoiceId} for enterprise {EnterpriseId}. Continuing with suspension.",
                            recurringInvoiceId, enterpriseId);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Enterprise {EnterpriseId} ({EnterpriseName}) has no Zoho recurring invoice to stop",
                        enterpriseId, enterpriseName);
                }

                // Step 2: Suspend enterprise in database using stored procedure
                await using var suspendCmd = new SqlCommand("sp_UpdateEnterpriseStatus", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                suspendCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                suspendCmd.Parameters.AddWithValue("@Status", "Suspended");
                suspendCmd.Parameters.AddWithValue("@UpdatedBy", 0); // System update

                await using var resultReader = await suspendCmd.ExecuteReaderAsync(cancellationToken);
                if (await resultReader.ReadAsync(cancellationToken))
                {
                    _logger.LogWarning(
                        "Enterprise {EnterpriseId} ({EnterpriseName}) SUSPENDED automatically due to {OverdueDays} days overdue invoice (MANDATORY RULE). Zoho recurring invoice stopped: {RecurringInvoiceId}",
                        enterpriseId, enterpriseName, overdueDays, recurringInvoiceId ?? "N/A");
                }
                else
                {
                    _logger.LogWarning("Enterprise {EnterpriseId} ({EnterpriseName}) was not found or could not be suspended", 
                        enterpriseId, enterpriseName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suspending enterprise {EnterpriseId} ({EnterpriseName}) in database", 
                    enterpriseId, enterpriseName);
                throw;
            }
        }
    }
}

