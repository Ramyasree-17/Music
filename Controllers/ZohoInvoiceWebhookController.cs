using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    /// <summary>
    /// Controller for Zoho invoice webhooks
    /// This endpoint receives invoice events from Zoho Books
    /// </summary>
    [ApiController]
    [Route("api/zoho/webhook")]
    [AllowAnonymous]
    [Tags("Section 13 - Billing & Subscriptions - Webhooks")]
    public class ZohoInvoiceWebhookController : ControllerBase
    {
        private readonly string _connStr;
        private readonly ILogger<ZohoInvoiceWebhookController>? _logger;

        public ZohoInvoiceWebhookController(
            IConfiguration config,
            ILogger<ZohoInvoiceWebhookController>? logger = null)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        /// <summary>
        /// Webhook endpoint for Zoho invoice events (created, updated, paid)
        /// This keeps the Invoices table synchronized with Zoho Books
        /// </summary>
        /// <remarks>
        /// Zoho will call this webhook when:
        /// - Invoice is created
        /// - Invoice is updated
        /// - Invoice is paid
        /// 
        /// Expected payload from Zoho:
        /// {
        ///   "invoice": {
        ///     "invoice_id": "123456789",
        ///     "customer_id": "3341366000000045021",
        ///     "date": "2024-01-01",
        ///     "due_date": "2024-01-31",
        ///     "status": "sent",
        ///     "total": 25000.00
        ///   }
        /// }
        /// </remarks>
        [HttpPost("invoice")]
        [Consumes("application/json")]
        public async Task<IActionResult> OnInvoiceCreated([FromBody] ZohoInvoiceWebhookDto payload)
        {
            try
            {
                if (payload?.Invoice == null)
                {
                    _logger?.LogWarning("Zoho invoice webhook received with null payload");
                    return Ok(new { message = "Invalid payload, ignoring" });
                }

                var invoice = payload.Invoice;
                _logger?.LogInformation("Zoho invoice webhook received: InvoiceId={InvoiceId}, Status={Status}, CustomerId={CustomerId}", 
                    invoice.Invoice_Id, invoice.Status, invoice.Customer_Id);

                // 1️⃣ Find Enterprise using Zoho Customer ID
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var findCmd = new SqlCommand(@"
                    SELECT EnterpriseID 
                    FROM Enterprises 
                    WHERE ZohoCustomerId = @CustomerId
                      AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

                findCmd.Parameters.AddWithValue("@CustomerId", invoice.Customer_Id ?? "");

                var enterpriseIdObj = await findCmd.ExecuteScalarAsync();
                if (enterpriseIdObj == null || enterpriseIdObj == DBNull.Value)
                {
                    _logger?.LogWarning("Enterprise not found for Zoho customer ID: {CustomerId}, ignoring webhook", invoice.Customer_Id);
                    return Ok(new { message = "Enterprise not found for this customer, skipping" });
                }

                var enterpriseId = Convert.ToInt32(enterpriseIdObj);

                // Parse dates
                if (!DateTime.TryParse(invoice.Due_Date ?? "", out var dueDate))
                {
                    _logger?.LogWarning("Invalid due_date in invoice webhook: {DueDate}", invoice.Due_Date);
                    return BadRequest(new { error = "Invalid due_date format" });
                }

                DateTime invoiceDate = dueDate;
                if (!string.IsNullOrWhiteSpace(invoice.Date) && DateTime.TryParse(invoice.Date, out var parsedDate))
                {
                    invoiceDate = parsedDate;
                }

                // 2️⃣ Insert invoice if not exists, update if exists
                await using var upsertCmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM Invoices WHERE InvoiceId = @InvoiceId)
                    BEGIN
                        INSERT INTO Invoices (
                            InvoiceId,
                            TenantType,
                            TenantId,
                            Status,
                            CreatedAt,
                            DueDate,
                            TotalAmount,
                            PeriodStart,
                            PeriodEnd
                        )
                        VALUES (
                            @InvoiceId,
                            'Enterprise',
                            @TenantId,
                            @Status,
                            @CreatedAt,
                            @DueDate,
                            @TotalAmount,
                            @CreatedAt,
                            @DueDate
                        )
                    END
                    ELSE
                    BEGIN
                        UPDATE Invoices
                        SET Status = @Status,
                            DueDate = @DueDate,
                            TotalAmount = @TotalAmount,
                            PaidAt = CASE WHEN @Status = 'Paid' THEN SYSUTCDATETIME() ELSE PaidAt END
                        WHERE InvoiceId = @InvoiceId
                    END", conn);

                upsertCmd.Parameters.AddWithValue("@InvoiceId", invoice.Invoice_Id ?? "");
                upsertCmd.Parameters.AddWithValue("@TenantId", enterpriseId);
                upsertCmd.Parameters.AddWithValue("@Status", MapZohoStatusToDbStatus(invoice.Status ?? ""));
                upsertCmd.Parameters.AddWithValue("@CreatedAt", invoiceDate);
                upsertCmd.Parameters.AddWithValue("@DueDate", dueDate);
                upsertCmd.Parameters.AddWithValue("@TotalAmount", invoice.Total ?? 0);

                await upsertCmd.ExecuteNonQueryAsync();

                _logger?.LogInformation("Invoice {InvoiceId} synced successfully for enterprise {EnterpriseId}", 
                    invoice.Invoice_Id, enterpriseId);

                return Ok(new { message = "Invoice synced successfully" });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing Zoho invoice webhook");
                return StatusCode(500, new { error = $"Error processing webhook: {ex.Message}" });
            }
        }

        private string MapZohoStatusToDbStatus(string zohoStatus)
        {
            return zohoStatus.ToLowerInvariant() switch
            {
                "paid" => "Paid",
                "sent" => "Unpaid",
                "draft" => "Unpaid",
                "overdue" => "Unpaid",
                _ => "Unpaid"
            };
        }
    }
}












