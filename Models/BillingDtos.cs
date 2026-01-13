using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TunewaveAPIDB1.Models
{
    public class CreateSubscriptionDto
    {
        [Required]
        public string TenantType { get; set; } = string.Empty;
        [Required]
        public int TenantId { get; set; }
        [Required]
        public int PlanTypeId { get; set; }
        [Required]
        public string BillingModel { get; set; } = string.Empty;
        [Required]
        public decimal BillingValue { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        public bool AutoPayEnabled { get; set; } = false;
        public string Currency { get; set; } = "INR";
    }

    public class ChangePlanDto
    {
        [Required]
        public string TenantType { get; set; } = string.Empty;
        [Required]
        public int TenantId { get; set; }
        [Required]
        public int NewPlanTypeId { get; set; }
        public int EffectiveAfterDays { get; set; } = 60;
        public string? Notes { get; set; }
    }

    public class SubscriptionResponse
    {
        public long SubscriptionId { get; set; }
        public string TenantType { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public int PlanTypeId { get; set; }
        public string BillingModel { get; set; } = string.Empty;
        public decimal BillingValue { get; set; }
        public DateTime StartDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool AutoPayEnabled { get; set; }
    }

    public class InvoiceDto
    {
        public long InvoiceId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class InvoiceDetailDto : InvoiceDto
    {
        public List<InvoiceLineDto> Lines { get; set; } = new();
    }

    public class InvoiceLineDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool Taxable { get; set; }
    }

    public class PayInvoiceDto
    {
        [Required]
        public string Method { get; set; } = string.Empty;
        public string Currency { get; set; } = "INR";
        public string? Gateway { get; set; }
        public string? PaymentToken { get; set; }
    }

    public class RecordEnterprisePaymentDto
    {
        [Required]
        public DateTime PaymentDate { get; set; }
        
        [Required]
        public decimal Amount { get; set; }
        
        public string? InvoiceId { get; set; }
    }

    /// <summary>
    /// Request DTO for creating a Zoho Books recurring invoice
    /// </summary>
    public class CreateRecurringInvoiceRequestDto
    {
        [Required]
        public int EnterpriseId { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        public string RecurrenceName { get; set; } = string.Empty;

        public string CurrencyCode { get; set; } = "INR";

        public string RecurrenceFrequency { get; set; } = "months";

        public int RepeatEvery { get; set; } = 1;

        [Required]
        public string StartDate { get; set; } = string.Empty; // Format: "yyyy-MM-dd"

        [Required]
        public List<RecurringInvoiceLineItemDto> LineItems { get; set; } = new();

        public string? Notes { get; set; }

        public string? Terms { get; set; }
    }

    /// <summary>
    /// Line item for recurring invoice
    /// </summary>
    public class RecurringInvoiceLineItemDto
    {
        [Required]
        public string ItemId { get; set; } = string.Empty;

        [Required]
        public decimal Rate { get; set; }

        public int Quantity { get; set; } = 1;
    }

    /// <summary>
    /// Response DTO for recurring invoice creation
    /// </summary>
    public class CreateRecurringInvoiceResponseDto
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public RecurringInvoiceDto? RecurringInvoice { get; set; }
    }

    /// <summary>
    /// Recurring invoice details
    /// </summary>
    public class RecurringInvoiceDto
    {
        public string RecurringInvoiceId { get; set; } = string.Empty;
        public string RecurrenceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string RecurrenceFrequency { get; set; } = string.Empty;
        public int RepeatEvery { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string? NextInvoiceDate { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string? RecurringInvoicePreference { get; set; }  // e.g., "create_charge_and_send", "create_and_send", "create_as_draft"
    }

    /// <summary>
    /// DTO for Zoho webhook auto-suspend payload
    /// </summary>
    public class ZohoAutoSuspendDto
    {
        public string Source { get; set; } = string.Empty;          // "zoho"
        public string Event { get; set; } = string.Empty;           // "AUTO_SUSPEND_105_DAYS"
        
        [JsonPropertyName("customer_id")]
        public string? Customer_Id { get; set; }
        
        [JsonPropertyName("customer_name")]
        public string? Customer_Name { get; set; }
        
        [JsonPropertyName("customer_email")]
        public string? Customer_Email { get; set; }
        
        [JsonPropertyName("invoice_id")]
        public string? Invoice_Id { get; set; }
        
        [JsonPropertyName("invoice_number")]
        public string? Invoice_Number { get; set; }
        
        [JsonPropertyName("invoice_date")]
        public string? Invoice_Date { get; set; }
        
        [JsonPropertyName("invoice_status")]
        public string? Invoice_Status { get; set; }
        
        [JsonPropertyName("days_overdue")]
        public int Days_Overdue { get; set; }
    }

    /// <summary>
    /// DTO for stopping a Zoho recurring invoice
    /// </summary>
    public class StopRecurringInvoiceDto
    {
        [Required]
        public string RecurringInvoiceId { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for Zoho invoice webhook payload
    /// </summary>
    public class ZohoInvoiceWebhookDto
    {
        [JsonPropertyName("invoice")]
        public ZohoInvoiceDto? Invoice { get; set; }
    }

    /// <summary>
    /// DTO for Zoho invoice data in webhook
    /// </summary>
    public class ZohoInvoiceDto
    {
        [JsonPropertyName("invoice_id")]
        public string? Invoice_Id { get; set; }

        [JsonPropertyName("customer_id")]
        public string? Customer_Id { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("due_date")]
        public string? Due_Date { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("total")]
        public decimal? Total { get; set; }
    }

    /// <summary>
    /// Line item request for Zoho Books API
    /// </summary>
    public class ZohoLineItemRequest
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public int Quantity { get; set; } = 1;
    }

    /// <summary>
    /// Zoho customer model
    /// </summary>
    public class ZohoCustomer
    {
        public string? ContactId { get; set; }
    }

    /// <summary>
    /// Response from Zoho Books customer creation
    /// </summary>
    public class ZohoCustomerResponse
    {
        public string? ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    /// <summary>
    /// DTO for Zoho recurring invoice response
    /// </summary>
    public class ZohoRecurringInvoiceDto
    {
        public string? RecurringInvoiceId { get; set; }
    }

    /// <summary>
    /// Response from Zoho Books recurring invoice creation
    /// </summary>
    public class ZohoRecurringInvoiceResponse
    {
        public int Code { get; set; }
        public ZohoRecurringInvoiceDto? RecurringInvoice { get; set; }
    }
}
























