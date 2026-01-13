using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TunewaveAPIDB1.Services
{
    public class PaymentTrackingService
    {
        private readonly string _connStr;
        private readonly ILogger<PaymentTrackingService> _logger;
        private readonly ZohoBooksService _zohoBooksService;

        public PaymentTrackingService(
            IConfiguration configuration,
            ILogger<PaymentTrackingService> logger,
            ZohoBooksService zohoBooksService)
        {
            _connStr = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
            _zohoBooksService = zohoBooksService;
        }

        /// <summary>
        /// Records a payment and updates the next billing date
        /// </summary>
        public async Task<bool> RecordPaymentAsync(int enterpriseId, DateTime paymentDate, decimal amount, string? invoiceId = null)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Get enterprise Zoho details
                await using var getCmd = new SqlCommand(@"
                    SELECT ZohoCustomerId, ZohoRecurringInvoiceId, BillingDayOfMonth, LastPaymentDate, NextBillingDate
                    FROM Enterprises
                    WHERE EnterpriseID = @EnterpriseId", conn);
                getCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);

                await using var reader = await getCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    _logger?.LogWarning("Enterprise {EnterpriseId} not found", enterpriseId);
                    return false;
                }

                var zohoCustomerId = reader["ZohoCustomerId"]?.ToString();
                var zohoRecurringInvoiceId = reader["ZohoRecurringInvoiceId"]?.ToString();
                var billingDayOfMonth = reader["BillingDayOfMonth"] != DBNull.Value ? Convert.ToInt32(reader["BillingDayOfMonth"]) : 0;
                var lastPaymentDate = reader["LastPaymentDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastPaymentDate"]) : (DateTime?)null;

                await reader.CloseAsync();

                // Calculate next billing date: same day next month
                var nextBillingDate = CalculateNextBillingDate(paymentDate, billingDayOfMonth);

                // Update enterprise with payment info
                await using var updateCmd = new SqlCommand(@"
                    UPDATE Enterprises
                    SET LastPaymentDate = @PaymentDate,
                        NextBillingDate = @NextBillingDate,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE EnterpriseID = @EnterpriseId", conn);
                updateCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                updateCmd.Parameters.AddWithValue("@PaymentDate", paymentDate);
                updateCmd.Parameters.AddWithValue("@NextBillingDate", nextBillingDate);

                await updateCmd.ExecuteNonQueryAsync();

                // Update Zoho Books recurring invoice if configured
                if (!string.IsNullOrWhiteSpace(zohoRecurringInvoiceId) && _zohoBooksService != null)
                {
                    var updated = await _zohoBooksService.UpdateNextBillingDateAsync(
                        zohoRecurringInvoiceId, 
                        paymentDate, 
                        billingDayOfMonth > 0 ? billingDayOfMonth : paymentDate.Day);
                    
                    if (updated)
                    {
                        _logger?.LogInformation("Updated Zoho Books recurring invoice {RecurringInvoiceId} for enterprise {EnterpriseId}", 
                            zohoRecurringInvoiceId, enterpriseId);
                    }
                }

                _logger?.LogInformation("Payment recorded for enterprise {EnterpriseId}. Next billing date: {NextBillingDate}", 
                    enterpriseId, nextBillingDate);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error recording payment for enterprise {EnterpriseId}", enterpriseId);
                return false;
            }
        }

        /// <summary>
        /// Calculates next billing date based on payment date and billing day
        /// </summary>
        private DateTime CalculateNextBillingDate(DateTime paymentDate, int billingDayOfMonth)
        {
            var dayOfMonth = paymentDate.Day;
            var targetDay = billingDayOfMonth > 0 ? billingDayOfMonth : dayOfMonth;
            
            var nextMonth = paymentDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
            var actualDay = Math.Min(targetDay, daysInMonth);
            
            return new DateTime(nextMonth.Year, nextMonth.Month, actualDay);
        }
    }
}























