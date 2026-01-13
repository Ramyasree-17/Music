using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/billing")]
    [Authorize]
    [Tags("Section 13 - Billing & Subscriptions")]
    public class BillingController : ControllerBase
    {
        private readonly string _connStr;

        public BillingController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpPost("subscription/create")]
        [Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionDto dto)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    INSERT INTO Subscriptions (TenantType, TenantId, PlanTypeId, BillingModel, BillingValue, StartDate, Status, AutoPayEnabled, Currency)
                    OUTPUT INSERTED.SubscriptionId
                    VALUES (@TenantType, @TenantId, @PlanTypeId, @BillingModel, @BillingValue, @StartDate, 'Active', @AutoPay, @Currency)", conn);
                cmd.Parameters.AddWithValue("@TenantType", dto.TenantType);
                cmd.Parameters.AddWithValue("@TenantId", dto.TenantId);
                cmd.Parameters.AddWithValue("@PlanTypeId", dto.PlanTypeId);
                cmd.Parameters.AddWithValue("@BillingModel", dto.BillingModel);
                cmd.Parameters.AddWithValue("@BillingValue", dto.BillingValue);
                cmd.Parameters.AddWithValue("@StartDate", dto.StartDate);
                cmd.Parameters.AddWithValue("@AutoPay", dto.AutoPayEnabled);
                cmd.Parameters.AddWithValue("@Currency", dto.Currency);

                var subscriptionId = await cmd.ExecuteScalarAsync();
                return StatusCode(201, new { subscriptionId = Convert.ToInt64(subscriptionId) });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("subscription/{tenantId:int}")]
        public async Task<IActionResult> GetSubscription(int tenantId, [FromQuery] string tenantType)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT SubscriptionId, TenantType, TenantId, PlanTypeId, BillingModel, BillingValue, StartDate, Status, AutoPayEnabled
                    FROM Subscriptions
                    WHERE TenantType = @TenantType AND TenantId = @TenantId", conn);
                cmd.Parameters.AddWithValue("@TenantType", tenantType);
                cmd.Parameters.AddWithValue("@TenantId", tenantId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound();

                return Ok(new
                {
                    subscriptionId = reader["SubscriptionId"],
                    tenantType = reader["TenantType"],
                    tenantId = reader["TenantId"],
                    planTypeId = reader["PlanTypeId"],
                    billingModel = reader["BillingModel"],
                    billingValue = reader["BillingValue"],
                    startDate = reader["StartDate"],
                    status = reader["Status"],
                    autoPayEnabled = reader["AutoPayEnabled"]
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("subscription/change-plan")]
        public async Task<IActionResult> ChangePlan([FromBody] ChangePlanDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Get current plan
                await using var getCmd = new SqlCommand(@"
                    SELECT PlanTypeId FROM Subscriptions
                    WHERE TenantType = @TenantType AND TenantId = @TenantId", conn);
                getCmd.Parameters.AddWithValue("@TenantType", dto.TenantType);
                getCmd.Parameters.AddWithValue("@TenantId", dto.TenantId);
                var currentPlan = await getCmd.ExecuteScalarAsync();
                if (currentPlan == null)
                    return NotFound(new { error = "Subscription not found" });

                var effectiveOn = DateTime.UtcNow.AddDays(dto.EffectiveAfterDays);

                await using var cmd = new SqlCommand(@"
                    INSERT INTO PlanChangeRequests (TenantType, TenantId, CurrentPlanTypeId, NewPlanTypeId, RequestedByUserId, EffectiveAfterDays, EffectiveOn, Status, Notes)
                    OUTPUT INSERTED.RequestId
                    VALUES (@TenantType, @TenantId, @CurrentPlan, @NewPlan, @UserId, @Days, @EffectiveOn, 'Pending', @Notes)", conn);
                cmd.Parameters.AddWithValue("@TenantType", dto.TenantType);
                cmd.Parameters.AddWithValue("@TenantId", dto.TenantId);
                cmd.Parameters.AddWithValue("@CurrentPlan", currentPlan);
                cmd.Parameters.AddWithValue("@NewPlan", dto.NewPlanTypeId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Days", dto.EffectiveAfterDays);
                cmd.Parameters.AddWithValue("@EffectiveOn", effectiveOn);
                cmd.Parameters.AddWithValue("@Notes", (object?)dto.Notes ?? DBNull.Value);

                var requestId = await cmd.ExecuteScalarAsync();
                return Accepted(new { requestId = Convert.ToInt64(requestId), effectiveOn });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("invoices/{tenantId:int}")]
        public async Task<IActionResult> GetInvoices(int tenantId, [FromQuery] string tenantType)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT InvoiceId, PeriodStart, PeriodEnd, SubTotal, TaxAmount, TotalAmount, Status, DueDate, PaidAt
                    FROM Invoices
                    WHERE TenantType = @TenantType AND TenantId = @TenantId
                    ORDER BY CreatedAt DESC", conn);
                cmd.Parameters.AddWithValue("@TenantType", tenantType);
                cmd.Parameters.AddWithValue("@TenantId", tenantId);

                await using var reader = await cmd.ExecuteReaderAsync();
                var invoices = new List<object>();
                while (await reader.ReadAsync())
                {
                    invoices.Add(new
                    {
                        invoiceId = reader["InvoiceId"],
                        periodStart = reader["PeriodStart"],
                        periodEnd = reader["PeriodEnd"],
                        subtotal = reader["SubTotal"],
                        tax = reader["TaxAmount"],
                        total = reader["TotalAmount"],
                        status = reader["Status"],
                        dueDate = reader["DueDate"] == DBNull.Value ? null : reader["DueDate"],
                        paidAt = reader["PaidAt"] == DBNull.Value ? null : reader["PaidAt"]
                    });
                }

                return Ok(invoices);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("invoice/{invoiceId:long}")]
        public async Task<IActionResult> GetInvoice(long invoiceId)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT InvoiceId, TenantType, TenantId, PeriodStart, PeriodEnd, SubTotal, TaxAmount, TotalAmount, Status, DueDate, PaidAt
                    FROM Invoices
                    WHERE InvoiceId = @InvoiceId", conn);
                cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound();

                var invoiceIdVal = reader.GetInt64(0);
                var tenantType = reader["TenantType"].ToString()!;
                var tenantId = Convert.ToInt32(reader["TenantId"]);
                var periodStart = reader["PeriodStart"];
                var periodEnd = reader["PeriodEnd"];
                var subTotal = reader["SubTotal"];
                var taxAmount = reader["TaxAmount"];
                var totalAmount = reader["TotalAmount"];
                var status = reader["Status"];
                var dueDate = reader["DueDate"] == DBNull.Value ? null : reader["DueDate"];

                await reader.CloseAsync();

                // Get lines
                await using var linesCmd = new SqlCommand(@"
                    SELECT Description, Amount, Taxable FROM InvoiceLines WHERE InvoiceId = @InvoiceId", conn);
                linesCmd.Parameters.AddWithValue("@InvoiceId", invoiceIdVal);
                await using var linesReader = await linesCmd.ExecuteReaderAsync();
                var lines = new List<object>();
                while (await linesReader.ReadAsync())
                {
                    lines.Add(new
                    {
                        description = linesReader["Description"],
                        amount = linesReader["Amount"],
                        taxable = linesReader["Taxable"]
                    });
                }

                return Ok(new
                {
                    invoiceId = invoiceIdVal,
                    tenantType,
                    tenantId,
                    periodStart,
                    periodEnd,
                    lines,
                    subTotal,
                    taxAmount,
                    totalAmount,
                    status,
                    dueDate
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("invoice/{invoiceId:long}/pay")]
        public async Task<IActionResult> PayInvoice(long invoiceId, [FromBody] PayInvoiceDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Get invoice
                    await using var getCmd = new SqlCommand(@"
                        SELECT TenantType, TenantId, TotalAmount, Status FROM Invoices
                        WHERE InvoiceId = @InvoiceId", conn, tx);
                    getCmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
                    await using var reader = await getCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { error = "Invoice not found" });

                    if (reader["Status"].ToString() != "Unpaid")
                        return BadRequest(new { error = "Invoice already paid" });

                    var tenantType = reader["TenantType"].ToString()!;
                    var tenantId = Convert.ToInt32(reader["TenantId"]);
                    var totalAmount = Convert.ToDecimal(reader["TotalAmount"]);
                    await reader.CloseAsync();

                    if (dto.Method == "WALLET")
                    {
                        // Check wallet balance
                        await using var walletCmd = new SqlCommand(@"
                            SELECT Balance, Reserved FROM WalletBalances
                            WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn, tx);
                        walletCmd.Parameters.AddWithValue("@EntityType", tenantType);
                        walletCmd.Parameters.AddWithValue("@EntityId", tenantId);
                        walletCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                        await using var walletReader = await walletCmd.ExecuteReaderAsync();
                        if (!await walletReader.ReadAsync())
                            return BadRequest(new { error = "Wallet not found" });

                        var balance = Convert.ToDecimal(walletReader["Balance"]);
                        var reserved = Convert.ToDecimal(walletReader["Reserved"]);
                        var available = balance - reserved;

                        if (available < totalAmount)
                            return BadRequest(new { error = "Insufficient funds" });

                        await walletReader.CloseAsync();

                        // Create ledger entries
                        await using var ledgerCmd = new SqlCommand(@"
                            INSERT INTO LedgerEntries (EntityType, EntityId, Amount, Currency, EntryType, Reference)
                            VALUES (@EntityType, @EntityId, @Amount, @Currency, 'SaaSFeeDebit', @Reference)", conn, tx);
                        ledgerCmd.Parameters.AddWithValue("@EntityType", tenantType);
                        ledgerCmd.Parameters.AddWithValue("@EntityId", tenantId);
                        ledgerCmd.Parameters.AddWithValue("@Amount", -totalAmount);
                        ledgerCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                        ledgerCmd.Parameters.AddWithValue("@Reference", $"invoice:{invoiceId}");
                        await ledgerCmd.ExecuteNonQueryAsync();

                        // Update wallet
                        await using var updateCmd = new SqlCommand(@"
                            UPDATE WalletBalances 
                            SET Balance = Balance - @Amount, UpdatedAt = SYSUTCDATETIME()
                            WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn, tx);
                        updateCmd.Parameters.AddWithValue("@EntityType", tenantType);
                        updateCmd.Parameters.AddWithValue("@EntityId", tenantId);
                        updateCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                        updateCmd.Parameters.AddWithValue("@Amount", totalAmount);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    // Update invoice
                    await using var updateInvoiceCmd = new SqlCommand(@"
                        UPDATE Invoices 
                        SET Status = 'Paid', PaidAt = SYSUTCDATETIME()
                        WHERE InvoiceId = @InvoiceId", conn, tx);
                    updateInvoiceCmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
                    await updateInvoiceCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                return Ok(new { invoiceId, status = "Paid", paidAt = DateTime.UtcNow });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

