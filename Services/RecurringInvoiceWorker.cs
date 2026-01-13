using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using System.Data;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Services
{
    /// <summary>
    /// Background service that automatically creates recurring invoices for enterprises
    /// that have Zoho customers but no recurring invoice configured.
    /// Runs periodically to ensure all active enterprises have recurring invoices set up.
    /// </summary>
    public class RecurringInvoiceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<RecurringInvoiceWorker> _logger;
        private readonly string _connStr;

        // Run every 6 hours to check for enterprises needing recurring invoices
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

        public RecurringInvoiceWorker(
            IServiceProvider serviceProvider,
            IConfiguration config,
            ILogger<RecurringInvoiceWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RecurringInvoiceWorker started. Will check for enterprises needing recurring invoices every {Interval} hours.", 
                CheckInterval.TotalHours);

            // Wait a bit on startup before first check (allows app to fully initialize)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRecurringInvoicesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("RecurringInvoiceWorker is stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in RecurringInvoiceWorker. Will retry in {Interval} hours.", 
                        CheckInterval.TotalHours);
                }

                // Wait before next check
                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.LogInformation("RecurringInvoiceWorker stopped.");
        }

        private async Task ProcessRecurringInvoicesAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting check for enterprises needing recurring invoices");

            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync(cancellationToken);

                // Find enterprises with Zoho customers but no recurring invoice
                await using var findCmd = new SqlCommand(@"
                    SELECT 
                        EnterpriseID,
                        EnterpriseName,
                        ZohoCustomerId,
                        Domain
                    FROM Enterprises
                    WHERE ZohoCustomerId IS NOT NULL
                      AND ZohoCustomerId <> ''
                      AND (ZohoRecurringInvoiceId IS NULL OR ZohoRecurringInvoiceId = '')
                      AND Status = 'Active'
                      AND (IsDeleted = 0 OR IsDeleted IS NULL)
                    ORDER BY EnterpriseID", conn);

                var enterprisesNeedingInvoice = new List<(int EnterpriseId, string EnterpriseName, string ZohoCustomerId, string? Domain)>();

                await using var reader = await findCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    enterprisesNeedingInvoice.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader["Domain"] == DBNull.Value ? null : reader["Domain"].ToString()
                    ));
                }

                await reader.CloseAsync();

                _logger.LogInformation("Found {Count} enterprises needing recurring invoices", 
                    enterprisesNeedingInvoice.Count);

                if (enterprisesNeedingInvoice.Count == 0)
                {
                    _logger.LogInformation("All enterprises have recurring invoices configured");
                    return;
                }

                // Get ZohoBooksService from DI
                using var scope = _serviceProvider.CreateScope();
                var zohoBooksService = scope.ServiceProvider.GetRequiredService<ZohoBooksService>();

                var defaultItemId = _config["ZohoBooks:DefaultItemId"];
                var defaultAmount = _config.GetValue<decimal>("ZohoBooks:DefaultMonthlyAmount", 25000);

                if (string.IsNullOrWhiteSpace(defaultItemId))
                {
                    _logger.LogWarning("ZohoBooks:DefaultItemId not configured. Skipping recurring invoice creation.");
                    return;
                }

                // Create recurring invoices for each enterprise
                foreach (var (enterpriseId, enterpriseName, zohoCustomerId, domain) in enterprisesNeedingInvoice)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await CreateRecurringInvoiceForEnterpriseAsync(
                            enterpriseId,
                            enterpriseName,
                            zohoCustomerId,
                            domain,
                            defaultItemId,
                            defaultAmount,
                            zohoBooksService,
                            conn,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating recurring invoice for enterprise {EnterpriseId} ({EnterpriseName})", 
                            enterpriseId, enterpriseName);
                    }
                }

                if (enterprisesNeedingInvoice.Count > 0)
                {
                    _logger.LogInformation(
                        "Processed {Count} enterprise(s) for recurring invoice creation",
                        enterprisesNeedingInvoice.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recurring invoice check");
                throw;
            }
        }

        private async Task CreateRecurringInvoiceForEnterpriseAsync(
            int enterpriseId,
            string enterpriseName,
            string zohoCustomerId,
            string? domain,
            string defaultItemId,
            decimal defaultAmount,
            ZohoBooksService zohoBooksService,
            SqlConnection conn,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Creating recurring invoice for enterprise {EnterpriseId} ({EnterpriseName}), CustomerId={CustomerId}",
                    enterpriseId, enterpriseName, zohoCustomerId);

                var uniqueInvoiceName = $"Monthly Subscription - {enterpriseName} (ID: {enterpriseId})";

                var recurringInvoiceResult = await zohoBooksService.CreateRecurringInvoiceEnhancedAsync(
                    customerId: zohoCustomerId,
                    recurrenceName: uniqueInvoiceName,
                    currencyCode: "INR",
                    recurrenceFrequency: "months",
                    repeatEvery: 1,
                    startDate: DateTime.Today.ToString("yyyy-MM-dd"),
                    lineItems: new List<ZohoLineItemRequest>
                    {
                        new ZohoLineItemRequest
                        {
                            ItemId = defaultItemId,
                            Rate = defaultAmount,
                            Quantity = 1
                        }
                    },
                    notes: "Auto subscription",
                    terms: "Net 15"
                );

                if (recurringInvoiceResult != null && 
                    recurringInvoiceResult.Code == 0 &&
                    recurringInvoiceResult.RecurringInvoice != null &&
                    !string.IsNullOrWhiteSpace(recurringInvoiceResult.RecurringInvoice.RecurringInvoiceId))
                {
                    var recurringInvoiceId = recurringInvoiceResult.RecurringInvoice.RecurringInvoiceId;

                    // Save ZohoRecurringInvoiceId to Enterprises table
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE Enterprises
                        SET ZohoRecurringInvoiceId = @ZohoRecurringInvoiceId,
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE EnterpriseID = @EnterpriseId", conn);

                    updateCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    updateCmd.Parameters.AddWithValue("@ZohoRecurringInvoiceId", recurringInvoiceId);

                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                    _logger.LogInformation(
                        "Successfully created recurring invoice for enterprise {EnterpriseId} ({EnterpriseName}). RecurringInvoiceId={RecurringInvoiceId}",
                        enterpriseId, enterpriseName, recurringInvoiceId);
                }
                else
                {
                    var errorMsg = "Unknown error";
                    _logger.LogWarning(
                        "Failed to create recurring invoice for enterprise {EnterpriseId} ({EnterpriseName}): Code={Code}, Message={Message}",
                        enterpriseId, enterpriseName, recurringInvoiceResult?.Code ?? -1, errorMsg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recurring invoice for enterprise {EnterpriseId} ({EnterpriseName})", 
                    enterpriseId, enterpriseName);
                throw;
            }
        }
    }
}
