using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TunewaveAPIDB1.Models
{
    public class CreateEnterpriseDto
    {
        [Required]
        public string EnterpriseName { get; set; } = string.Empty;

        public string? Domain { get; set; }

        [Range(0, 100)]
        public decimal RevenueSharePercent { get; set; } = 10.00m;

        public bool QCRequired { get; set; } = true;

        [Required, EmailAddress]
        public string OwnerEmail { get; set; } = string.Empty;

        [Required]
        public DateTime AgreementStartDate { get; set; }

        [Required]
        public DateTime AgreementEndDate { get; set; }

        // ISRC / master code fields
        public bool HasIsrcMasterCode { get; set; }
        public string? AudioMasterCode { get; set; }
        public string? VideoMasterCode { get; set; }
        public string? IsrcCertificateUrl { get; set; }
    }

    public class UpdateEnterpriseDto
    {
        public string? Domain { get; set; }

        [Range(0, 100)]
        public decimal? RevenueSharePercent { get; set; }

        public bool? QCRequired { get; set; }

        // ISRC / master code fields (optional on update)
        public bool? HasIsrcMasterCode { get; set; }
        public string? AudioMasterCode { get; set; }
        public string? VideoMasterCode { get; set; }
        
        // ISRC Certificate - file upload only
        public IFormFile? IsrcCertificateFile { get; set; }
    }

    public class EnterpriseResponseDto
    {
        public int EnterpriseId { get; set; }
        public string EnterpriseName { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public decimal RevenueSharePercent { get; set; }
        public bool QCRequired { get; set; }
        public int? OwnerUserId { get; set; }
        public DateTime? AgreementStartDate { get; set; }
        public DateTime? AgreementEndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // ISRC / master code fields
        public bool HasIsrcMasterCode { get; set; }
        public string? AudioMasterCode { get; set; }
        public string? VideoMasterCode { get; set; }
        public string? IsrcCertificateUrl { get; set; }
    }

    public class ChangeStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    public class TransferLabelDto
    {
        [Required]
        public int LabelId { get; set; }

        [Required]
        public int ToEnterpriseId { get; set; }
    }

    public class EnterpriseLabelDto
    {
        public int LabelId { get; set; }
        public string LabelName { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal RevenueSharePercent { get; set; }
    }

    public class EnterpriseCreateV2
    {
        [Required]
        public string EnterpriseName { get; set; } = string.Empty;

        public string? Domain { get; set; }

        [Range(0, 100)]
        public decimal RevenueShare { get; set; } = 10.00m;

        public bool QCRequired { get; set; } = true;

        [Required, EmailAddress]
        public string OwnerEmail { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Phone number must be in valid format (e.g., +919876543210 or 9876543210)")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public DateTime AgreementStartDate { get; set; }

        [Required]
        public DateTime AgreementEndDate { get; set; }

        // ISRC / master code fields for enterprise-level configuration
        public bool HasIsrcMasterCode { get; set; }
        public string? AudioMasterCode { get; set; }
        public string? VideoMasterCode { get; set; }
        
        // ISRC Certificate - file upload only
        public IFormFile? IsrcCertificateFile { get; set; }
    }

    public class EnterpriseUpdateRequest
    {
        public string? Domain { get; set; }

        [Range(0, 100)]
        public decimal? RevenueShare { get; set; }

        [EmailAddress]
        public string? OwnerEmail { get; set; }
    }

    public class UpdateEnterpriseBrandingDto
    {
        public string? SiteName { get; set; }
        public string? SiteDescription { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? HeaderColor { get; set; }
        public string? SidebarColor { get; set; }
        public string? FooterColor { get; set; }
        public string? LogoUrl { get; set; }
        public string? FooterText { get; set; }
        public string? FooterLinksJson { get; set; }
    }
}


