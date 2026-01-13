using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Services
{
    public class ZohoBooksService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ZohoBooksService>? _logger;
        private readonly HttpClient _httpClient;

        private string? _cachedAccessToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        public ZohoBooksService(
            IConfiguration config,
            ILogger<ZohoBooksService>? logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        // =====================================================
        // ACCESS TOKEN
        // =====================================================
        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) &&
                DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
            {
                return _cachedAccessToken!;
            }

            var refreshToken = _config["ZohoBooks:RefreshToken"];
            var clientId = _config["ZohoBooks:ClientId"];
            var clientSecret = _config["ZohoBooks:ClientSecret"];
            var staticToken = _config["ZohoBooks:AccessToken"];

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                if (string.IsNullOrWhiteSpace(staticToken))
                    throw new Exception("ZohoBooks access token not configured");

                _cachedAccessToken = staticToken;
                _tokenExpiresAt = DateTime.UtcNow.AddHours(1);
                return staticToken;
            }

            var response = await _httpClient.PostAsync(
                "https://accounts.zoho.in/oauth/v2/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId!,
                    ["client_secret"] = clientSecret!,
                    ["grant_type"] = "refresh_token"
                })
            );

            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Zoho token refresh failed: {json}");

            using var doc = JsonDocument.Parse(json);
            _cachedAccessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            return _cachedAccessToken!;
        }

        // =====================================================
        // REQUEST BUILDER
        // =====================================================
        private async Task<HttpRequestMessage> CreateRequestAsync(
            HttpMethod method,
            string endpoint,
            object? body = null)
        {
            var token = await GetAccessTokenAsync();
            var orgId = _config["ZohoBooks:OrganizationId"];
            var baseUrl = _config["ZohoBooks:ApiBaseUrl"] ?? "https://books.zoho.in/api/v3";

            if (string.IsNullOrWhiteSpace(orgId))
                throw new Exception("ZohoBooks:OrganizationId missing");

            var url = $"{baseUrl}/{endpoint}?organization_id={orgId}";

            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Zoho-oauthtoken", token);

            if (body != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");
            }

            return request;
        }

        // =====================================================
        // CREATE CUSTOMER
        // =====================================================
        public async Task<ZohoCustomerResponse?> CreateCustomerAsync(
            string companyName,
            string email,
            string phone)
        {
            var token = await GetAccessTokenAsync();
            var orgId = _config["ZohoBooks:OrganizationId"];
            var baseUrl = _config["ZohoBooks:ApiBaseUrl"] ?? "https://books.zoho.in/api/v3";
            var url = $"{baseUrl}/contacts?organization_id={orgId}";

            var body = new
            {
                contact_name = companyName,
                company_name = companyName,
                contact_type = "customer",
                contact_persons = new[]
                {
                    new {
                        first_name = companyName,
                        email = email,
                        phone = phone,
                        is_primary_contact = true
                    }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", $"Zoho-oauthtoken {token}");
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("code").GetInt32() != 0)
                return null;

            var contact = doc.RootElement.GetProperty("contact");
            var contactId = contact.GetProperty("contact_id").GetString();
            return new ZohoCustomerResponse
            {
                ContactId = contactId ?? string.Empty,
                ContactName = contact.TryGetProperty("contact_name", out var contactName) ? contactName.GetString() : null,
                Email = email,
                Phone = phone
            };
        }

        // =====================================================
        // CREATE RECURRING INVOICE (Net-15 AUTO SEND)
        // =====================================================
        public async Task<CreateRecurringInvoiceResponseDto> CreateRecurringInvoiceEnhancedAsync(
            string customerId,
            string recurrenceName,
            string currencyCode,
            string recurrenceFrequency,
            int repeatEvery,
            string startDate,
            List<ZohoLineItemRequest> lineItems,
            string? notes,
            string? terms)
        {
            var token = await GetAccessTokenAsync();
            var orgId = _config["ZohoBooks:OrganizationId"];
            var baseUrl = _config["ZohoBooks:ApiBaseUrl"] ?? "https://books.zoho.in/api/v3";
            var url = $"{baseUrl}/recurringinvoices?organization_id={orgId}";

            var body = new
            {
                customer_id = customerId,
                recurrence_name = recurrenceName,
                currency_code = currencyCode,
                recurrence_frequency = recurrenceFrequency,
                repeat_every = repeatEvery,
                start_date = startDate,

                auto_send = true,
                payment_terms = 15,
                payment_terms_label = "Net 15",

                line_items = lineItems.Select(i => new
                {
                    item_id = i.ItemId,
                    rate = i.Rate,
                    quantity = i.Quantity
                }),

                notes,
                terms
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", $"Zoho-oauthtoken {token}");
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(req);
            var responseBody = await res.Content.ReadAsStringAsync();
            
            if (!res.IsSuccessStatusCode)
            {
                return new CreateRecurringInvoiceResponseDto
                {
                    Code = -1,
                    Message = responseBody,
                    RecurringInvoice = null
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var code = doc.RootElement.GetProperty("code").GetInt32();
            
            if (code != 0)
            {
                return new CreateRecurringInvoiceResponseDto
                {
                    Code = code,
                    Message = doc.RootElement.TryGetProperty("message", out var msg) ? (msg.GetString() ?? "Unknown error") : "Unknown error",
                    RecurringInvoice = null
                };
            }

            var recurringInvoice = doc.RootElement.GetProperty("recurring_invoice");
            var recurringInvoiceId = recurringInvoice.GetProperty("recurring_invoice_id").GetString();
            return new CreateRecurringInvoiceResponseDto
            {
                Code = 0,
                Message = "Success",
                RecurringInvoice = new RecurringInvoiceDto
                {
                    RecurringInvoiceId = recurringInvoiceId ?? string.Empty,
                    Status = recurringInvoice.TryGetProperty("status", out var status) ? (status.GetString() ?? string.Empty) : string.Empty,
                    NextInvoiceDate = recurringInvoice.TryGetProperty("next_invoice_date", out var nextDate) ? nextDate.GetString() : null
                }
            };
        }

        // =====================================================
        // STOP RECURRING INVOICE (FIXED)
        // =====================================================
        public async Task<bool> StopRecurringInvoiceAsync(string recurringInvoiceId)
        {
            var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"recurringinvoices/{recurringInvoiceId}/status/stop");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Stop recurring invoice failed: {Json}", json);
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("code").GetInt32() == 0;
        }

        // =====================================================
        // UPDATE NEXT BILLING DATE
        // =====================================================
        public async Task<bool> UpdateNextBillingDateAsync(
            string recurringInvoiceId,
            DateTime paymentDate,
            int billingDayOfMonth)
        {
            var next = paymentDate.AddMonths(1);
            var day = Math.Min(billingDayOfMonth, DateTime.DaysInMonth(next.Year, next.Month));

            var body = new
            {
                start_date = new DateTime(next.Year, next.Month, day).ToString("yyyy-MM-dd")
            };

            var request = await CreateRequestAsync(
                HttpMethod.Put,
                $"recurringinvoices/{recurringInvoiceId}",
                body);

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Update billing date failed: {Json}", json);
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("code").GetInt32() == 0;
        }

        // =====================================================
        // AUTO SUSPEND OVERDUE INVOICES (105 DAYS)
        // =====================================================
        public async Task AutoSuspendOverdueInvoices()
        {
            const int OVERDUE_DAYS_THRESHOLD = 105;
            var connStr = _config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connStr))
            {
                _logger?.LogError("DefaultConnection string not found");
                return;
            }

            _logger?.LogInformation("Starting auto-suspend check for enterprises with invoices overdue by {Threshold}+ days", OVERDUE_DAYS_THRESHOLD);

            try
            {
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Find enterprises with unpaid invoices overdue by 105+ days
                await using var findCmd = new SqlCommand(@"
                    SELECT DISTINCT 
                        e.EnterpriseID,
                        e.EnterpriseName,
                        e.ZohoRecurringInvoiceId,
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
                    GROUP BY e.EnterpriseID, e.EnterpriseName, e.ZohoRecurringInvoiceId
                    HAVING MAX(DATEDIFF(DAY, i.DueDate, GETDATE())) >= @Threshold", conn);

                findCmd.Parameters.AddWithValue("@Threshold", OVERDUE_DAYS_THRESHOLD);

                var enterprisesToSuspend = new List<(int EnterpriseId, string EnterpriseName, string? RecurringInvoiceId, int MaxOverdueDays)>();

                await using var reader = await findCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    enterprisesToSuspend.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader["ZohoRecurringInvoiceId"] == DBNull.Value ? null : reader["ZohoRecurringInvoiceId"].ToString(),
                        reader.GetInt32(3)
                    ));
                }

                await reader.CloseAsync();

                _logger?.LogInformation("Found {Count} enterprises with invoices overdue by {Threshold}+ days", 
                    enterprisesToSuspend.Count, OVERDUE_DAYS_THRESHOLD);

                // Suspend each enterprise and stop recurring invoice
                foreach (var (enterpriseId, enterpriseName, recurringInvoiceId, maxOverdueDays) in enterprisesToSuspend)
                {
                    try
                    {
                        // Stop Zoho recurring invoice if it exists
                        if (!string.IsNullOrWhiteSpace(recurringInvoiceId))
                        {
                            _logger?.LogInformation("Stopping Zoho recurring invoice {RecurringInvoiceId} for enterprise {EnterpriseId}", 
                                recurringInvoiceId, enterpriseId);
                            await StopRecurringInvoiceAsync(recurringInvoiceId);
                        }

                        // Suspend enterprise
                        await using var suspendCmd = new SqlCommand("sp_UpdateEnterpriseStatus", conn)
                        {
                            CommandType = CommandType.StoredProcedure
                        };

                        suspendCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                        suspendCmd.Parameters.AddWithValue("@Status", "Suspended");
                        suspendCmd.Parameters.AddWithValue("@UpdatedBy", 0); // System update

                        await suspendCmd.ExecuteNonQueryAsync();

                        _logger?.LogWarning(
                            "Enterprise {EnterpriseId} ({EnterpriseName}) SUSPENDED automatically due to {OverdueDays} days overdue invoice",
                            enterpriseId, enterpriseName, maxOverdueDays);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error suspending enterprise {EnterpriseId} ({EnterpriseName})", 
                            enterpriseId, enterpriseName);
                    }
                }

                if (enterprisesToSuspend.Count > 0)
                {
                    _logger?.LogWarning("Suspended {Count} enterprise(s) due to invoices overdue by {Threshold}+ days", 
                        enterprisesToSuspend.Count, OVERDUE_DAYS_THRESHOLD);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during auto-suspend check");
                throw;
            }
        }
    }
}
