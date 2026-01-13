using System.ComponentModel.DataAnnotations;

namespace TunewaveAPIDB1.Models
{
    public class WalletBalanceResponse
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal Reserved { get; set; }
        public decimal Available { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class LedgerEntryDto
    {
        public long LedgerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string EntryType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? Reference { get; set; }
    }

    public class LedgerResponse
    {
        public string AccountType { get; set; } = string.Empty;
        public int AccountId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<LedgerEntryDto> Ledger { get; set; } = new();
    }

    public class PayoutRequestDto
    {
        [Required]
        public string AccountType { get; set; } = string.Empty;
        [Required]
        public int AccountId { get; set; }
        [Required]
        public string Currency { get; set; } = string.Empty;
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }
        [Required]
        public BankDetailsDto BankDetails { get; set; } = new();
        public string? Note { get; set; }
    }

    public class BankDetailsDto
    {
        [Required]
        public string BankName { get; set; } = string.Empty;
        [Required]
        public string AccountNumber { get; set; } = string.Empty;
        [Required]
        public string Ifsc { get; set; } = string.Empty;
        [Required]
        public string BeneficiaryName { get; set; } = string.Empty;
    }

    public class PayoutActionDto
    {
        public int? ApprovedBy { get; set; }
        public int? RejectedBy { get; set; }
        public string? Note { get; set; }
    }

    public class AdjustmentRequestDto
    {
        [Required]
        public string AccountType { get; set; } = string.Empty;
        [Required]
        public int AccountId { get; set; }
        [Required]
        public string Currency { get; set; } = string.Empty;
        [Required]
        public decimal Amount { get; set; }
        [Required]
        public string EntryType { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
    }
}
























