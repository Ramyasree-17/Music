using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/wallet")]
    [Authorize]
    [Tags("Section 12 - Wallet & Ledger")]
    public class WalletController : ControllerBase
    {
        private readonly string _connStr;

        public WalletController(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance([FromQuery] string accountType, [FromQuery] int accountId, [FromQuery] string currency = "USD")
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT Balance, Reserved, UpdatedAt
                    FROM WalletBalances
                    WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn);
                cmd.Parameters.AddWithValue("@EntityType", accountType);
                cmd.Parameters.AddWithValue("@EntityId", accountId);
                cmd.Parameters.AddWithValue("@Currency", currency);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    // Create default wallet
                    await reader.CloseAsync();
                    await using var createCmd = new SqlCommand(@"
                        INSERT INTO WalletBalances (EntityType, EntityId, Currency, Balance, Reserved, UpdatedAt)
                        VALUES (@EntityType, @EntityId, @Currency, 0, 0, SYSUTCDATETIME())", conn);
                    createCmd.Parameters.AddWithValue("@EntityType", accountType);
                    createCmd.Parameters.AddWithValue("@EntityId", accountId);
                    createCmd.Parameters.AddWithValue("@Currency", currency);
                    await createCmd.ExecuteNonQueryAsync();

                    return Ok(new
                    {
                        entityType = accountType,
                        entityId = accountId,
                        currency,
                        balance = 0m,
                        reserved = 0m,
                        available = 0m,
                        lastUpdated = DateTime.UtcNow
                    });
                }

                var balance = Convert.ToDecimal(reader["Balance"]);
                var reserved = Convert.ToDecimal(reader["Reserved"]);
                var updatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"));

                return Ok(new
                {
                    entityType = accountType,
                    entityId = accountId,
                    currency,
                    balance,
                    reserved,
                    available = balance - reserved,
                    lastUpdated = updatedAt
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpGet("ledger")]
        public async Task<IActionResult> GetLedger([FromQuery] string accountType, [FromQuery] int accountId,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                var offset = (page - 1) * pageSize;
                var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
                var toDate = to ?? DateTime.UtcNow;

                await using var cmd = new SqlCommand(@"
                    SELECT LedgerId, CreatedAt, EntryType, Amount, Currency, Reference
                    FROM LedgerEntries
                    WHERE EntityType = @EntityType AND EntityId = @EntityId 
                    AND CreatedAt BETWEEN @From AND @To
                    ORDER BY CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", conn);
                cmd.Parameters.AddWithValue("@EntityType", accountType);
                cmd.Parameters.AddWithValue("@EntityId", accountId);
                cmd.Parameters.AddWithValue("@From", fromDate);
                cmd.Parameters.AddWithValue("@To", toDate);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                await using var reader = await cmd.ExecuteReaderAsync();
                var ledger = new List<object>();
                while (await reader.ReadAsync())
                {
                    ledger.Add(new
                    {
                        ledgerId = reader["LedgerId"],
                        createdAt = reader["CreatedAt"],
                        entryType = reader["EntryType"],
                        amount = reader["Amount"],
                        currency = reader["Currency"],
                        reference = reader["Reference"] == DBNull.Value ? null : reader["Reference"]
                    });
                }

                await using var countCmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM LedgerEntries
                    WHERE EntityType = @EntityType AND EntityId = @EntityId 
                    AND CreatedAt BETWEEN @From AND @To", conn);
                countCmd.Parameters.AddWithValue("@EntityType", accountType);
                countCmd.Parameters.AddWithValue("@EntityId", accountId);
                countCmd.Parameters.AddWithValue("@From", fromDate);
                countCmd.Parameters.AddWithValue("@To", toDate);
                var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync() ?? 0);

                return Ok(new { accountType, accountId, page, pageSize, total, ledger });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("payout/request")]
        public async Task<IActionResult> RequestPayout([FromBody] PayoutRequestDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Check available balance
                    await using var walletCmd = new SqlCommand(@"
                        SELECT Balance, Reserved FROM WalletBalances
                        WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn, tx);
                    walletCmd.Parameters.AddWithValue("@EntityType", dto.AccountType);
                    walletCmd.Parameters.AddWithValue("@EntityId", dto.AccountId);
                    walletCmd.Parameters.AddWithValue("@Currency", dto.Currency);

                    await using var walletReader = await walletCmd.ExecuteReaderAsync();
                    if (!await walletReader.ReadAsync())
                        return BadRequest(new { error = "Wallet not found" });

                    var balance = Convert.ToDecimal(walletReader["Balance"]);
                    var reserved = Convert.ToDecimal(walletReader["Reserved"]);
                    var available = balance - reserved;

                    if (available < dto.Amount)
                        return BadRequest(new { error = "Insufficient funds" });

                    await walletReader.CloseAsync();

                    // Create payout transaction
                    await using var payoutCmd = new SqlCommand(@"
                        INSERT INTO PayoutTransactions (EntityType, EntityId, Currency, Amount, NetAmount, Status, BankDetailsJson, RequestedByUserId)
                        OUTPUT INSERTED.PayoutId
                        VALUES (@EntityType, @EntityId, @Currency, @Amount, @NetAmount, 'PENDING', @BankDetails, @UserId)", conn, tx);
                    payoutCmd.Parameters.AddWithValue("@EntityType", dto.AccountType);
                    payoutCmd.Parameters.AddWithValue("@EntityId", dto.AccountId);
                    payoutCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                    payoutCmd.Parameters.AddWithValue("@Amount", dto.Amount);
                    payoutCmd.Parameters.AddWithValue("@NetAmount", dto.Amount); // TODO: Calculate fee
                    payoutCmd.Parameters.AddWithValue("@BankDetails", System.Text.Json.JsonSerializer.Serialize(dto.BankDetails));
                    payoutCmd.Parameters.AddWithValue("@UserId", userId);

                    var payoutId = await payoutCmd.ExecuteScalarAsync();
                    if (payoutId == null)
                        return BadRequest(new { error = "Failed to create payout" });

                    // Create ledger entry for lock
                    await using var ledgerCmd = new SqlCommand(@"
                        INSERT INTO LedgerEntries (EntityType, EntityId, Amount, Currency, EntryType, Reference)
                        VALUES (@EntityType, @EntityId, @Amount, @Currency, 'PayoutLock', @Reference)", conn, tx);
                    ledgerCmd.Parameters.AddWithValue("@EntityType", dto.AccountType);
                    ledgerCmd.Parameters.AddWithValue("@EntityId", dto.AccountId);
                    ledgerCmd.Parameters.AddWithValue("@Amount", -dto.Amount);
                    ledgerCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                    ledgerCmd.Parameters.AddWithValue("@Reference", $"payout:{payoutId}");
                    await ledgerCmd.ExecuteNonQueryAsync();

                    // Update wallet reserved
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE WalletBalances 
                        SET Reserved = Reserved + @Amount, UpdatedAt = SYSUTCDATETIME()
                        WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn, tx);
                    updateCmd.Parameters.AddWithValue("@EntityType", dto.AccountType);
                    updateCmd.Parameters.AddWithValue("@EntityId", dto.AccountId);
                    updateCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                    updateCmd.Parameters.AddWithValue("@Amount", dto.Amount);
                    await updateCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return StatusCode(201, new { payoutId = Convert.ToInt64(payoutId), status = "PENDING", amount = dto.Amount, currency = dto.Currency });
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("payout/{payoutId:long}/approve")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> ApprovePayout(long payoutId, [FromBody] PayoutActionDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    UPDATE PayoutTransactions 
                    SET Status = 'APPROVED', ProcessedByUserId = @UserId, ProcessedAt = SYSUTCDATETIME(), Notes = @Note
                    WHERE PayoutId = @PayoutId AND Status = 'PENDING'", conn);
                cmd.Parameters.AddWithValue("@PayoutId", payoutId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Note", (object?)dto.Note ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return BadRequest(new { error = "Payout not found or not in PENDING status" });

                // Enqueue payout job
                await using var jobCmd = new SqlCommand(@"
                    INSERT INTO Jobs (JobType, PayloadJson, Status, CreatedAt, UpdatedAt)
                    VALUES ('Payout', @Payload, 'Queued', SYSUTCDATETIME(), SYSUTCDATETIME())", conn);
                jobCmd.Parameters.AddWithValue("@Payload", $"{{\"payoutId\":{payoutId}}}");
                await jobCmd.ExecuteNonQueryAsync();

                return Ok(new { success = true, payoutId });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("payout/{payoutId:long}/reject")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> RejectPayout(long payoutId, [FromBody] PayoutActionDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Get payout details
                    await using var getCmd = new SqlCommand(@"
                        SELECT EntityType, EntityId, Currency, Amount FROM PayoutTransactions
                        WHERE PayoutId = @PayoutId", conn, tx);
                    getCmd.Parameters.AddWithValue("@PayoutId", payoutId);
                    await using var reader = await getCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { error = "Payout not found" });

                    var entityType = reader["EntityType"].ToString()!;
                    var entityId = Convert.ToInt32(reader["EntityId"]);
                    var currency = reader["Currency"].ToString()!;
                    var amount = Convert.ToDecimal(reader["Amount"]);
                    await reader.CloseAsync();

                    // Update payout status
                    await using var updateCmd = new SqlCommand(@"
                        UPDATE PayoutTransactions 
                        SET Status = 'REJECTED', ProcessedByUserId = @UserId, ProcessedAt = SYSUTCDATETIME(), Notes = @Note
                        WHERE PayoutId = @PayoutId", conn, tx);
                    updateCmd.Parameters.AddWithValue("@PayoutId", payoutId);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    updateCmd.Parameters.AddWithValue("@Note", (object?)dto.Note ?? DBNull.Value);
                    await updateCmd.ExecuteNonQueryAsync();

                    // Reverse reserved amount
                    await using var reverseCmd = new SqlCommand(@"
                        UPDATE WalletBalances 
                        SET Reserved = Reserved - @Amount, UpdatedAt = SYSUTCDATETIME()
                        WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn, tx);
                    reverseCmd.Parameters.AddWithValue("@EntityType", entityType);
                    reverseCmd.Parameters.AddWithValue("@EntityId", entityId);
                    reverseCmd.Parameters.AddWithValue("@Currency", currency);
                    reverseCmd.Parameters.AddWithValue("@Amount", amount);
                    await reverseCmd.ExecuteNonQueryAsync();

                    // Create reversal ledger entry
                    await using var ledgerCmd = new SqlCommand(@"
                        INSERT INTO LedgerEntries (EntityType, EntityId, Amount, Currency, EntryType, Reference)
                        VALUES (@EntityType, @EntityId, @Amount, @Currency, 'PayoutLockReversal', @Reference)", conn, tx);
                    ledgerCmd.Parameters.AddWithValue("@EntityType", entityType);
                    ledgerCmd.Parameters.AddWithValue("@EntityId", entityId);
                    ledgerCmd.Parameters.AddWithValue("@Amount", amount);
                    ledgerCmd.Parameters.AddWithValue("@Currency", currency);
                    ledgerCmd.Parameters.AddWithValue("@Reference", $"payout:{payoutId}:rejected");
                    await ledgerCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                return Ok(new { success = true, payoutId });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }

        [HttpPost("ledger/add-adjustment")]
        [Authorize(Policy = "FinanceOrAdmin")]
        public async Task<IActionResult> AddAdjustment([FromBody] AdjustmentRequestDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                await using var tx = conn.BeginTransaction();
                try
                {
                    // Insert ledger entry
                    await using var ledgerCmd = new SqlCommand(@"
                        INSERT INTO LedgerEntries (EntityType, EntityId, Amount, Currency, EntryType, Reference, Notes)
                        OUTPUT INSERTED.LedgerId
                        VALUES (@EntityType, @EntityId, @Amount, @Currency, @EntryType, @Reference, @Description)", conn, tx);
                    ledgerCmd.Parameters.AddWithValue("@EntityType", dto.AccountType);
                    ledgerCmd.Parameters.AddWithValue("@EntityId", dto.AccountId);
                    ledgerCmd.Parameters.AddWithValue("@Amount", dto.Amount);
                    ledgerCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                    ledgerCmd.Parameters.AddWithValue("@EntryType", dto.EntryType);
                    ledgerCmd.Parameters.AddWithValue("@Reference", (object?)dto.Reference ?? DBNull.Value);
                    ledgerCmd.Parameters.AddWithValue("@Description", dto.Description);

                    var ledgerId = await ledgerCmd.ExecuteScalarAsync();

                    // Update wallet balance
                    await using var walletCmd = new SqlCommand(@"
                        UPDATE WalletBalances 
                        SET Balance = Balance + @Amount, UpdatedAt = SYSUTCDATETIME()
                        WHERE EntityType = @EntityType AND EntityId = @EntityId AND Currency = @Currency", conn, tx);
                    walletCmd.Parameters.AddWithValue("@EntityType", dto.AccountType);
                    walletCmd.Parameters.AddWithValue("@EntityId", dto.AccountId);
                    walletCmd.Parameters.AddWithValue("@Currency", dto.Currency);
                    walletCmd.Parameters.AddWithValue("@Amount", dto.Amount);
                    await walletCmd.ExecuteNonQueryAsync();

                    await tx.CommitAsync();

                    return Ok(new { success = true, ledgerId = Convert.ToInt64(ledgerId) });
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
        }
    }
}

