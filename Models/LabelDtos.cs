using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace TunewaveAPIDB1.Models
{
    public class CreateLabelDto
    {
        [Required]
        public string LabelName { get; set; } = string.Empty;

        // EnterpriseId is now handled automatically from the logged-in user.
        // We keep the property for internal use but hide it from API/Swagger.
        [JsonIgnore]
        public int? EnterpriseId { get; set; }

        [Required]
        public int PlanTypeId { get; set; }

        [Range(0, 100)]
        public decimal RevenueSharePercent { get; set; } = 80.00m;

        public string? Domain { get; set; }

        [EmailAddress]
        public string? OwnerEmail { get; set; }

        public bool QCRequired { get; set; } = true;

        [Required]
        public DateTime AgreementStartDate { get; set; }

        [Required]
        public DateTime AgreementEndDate { get; set; }

        // ISRC / master code fields
        public bool HasIsrcMasterCode { get; set; }
        public string? AudioMasterCode { get; set; }
        public string? VideoMasterCode { get; set; }

        // ISRC Certificate - file upload only
        public IFormFile? IsrcCertificateFile { get; set; }
    }

    public class UpdateLabelDto
    {
        [Range(0, 100)]
        public decimal? RevenueSharePercent { get; set; }

        [EmailAddress]
        public string? OwnerEmail { get; set; }

        public string? Domain { get; set; }
    }

    public class LabelResponseDto
    {
        public int LabelId { get; set; }
        public string LabelName { get; set; } = string.Empty;
        public int EnterpriseId { get; set; }
        public int PlanTypeId { get; set; }
        public string PlanTypeName { get; set; } = string.Empty;
        public decimal RevenueSharePercent { get; set; }
        public string? Domain { get; set; }
        public string? SubDomain { get; set; }
        public string? OwnerEmail { get; set; }
        public bool QCRequired { get; set; }
        public DateTime? AgreementStartDate { get; set; }
        public DateTime? AgreementEndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public BrandingDto? Branding { get; set; }

        // ISRC / master code fields
        public bool HasIsrcMasterCode { get; set; }
        public string? AudioMasterCode { get; set; }
        public string? VideoMasterCode { get; set; }
        public string? IsrcCertificateUrl { get; set; }
    }

    public class BrandingDto
    {
        public string? LogoUrl { get; set; }
        public string? FaviconUrl { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? FooterText { get; set; }
        public string? EmailTemplateJson { get; set; }
    }

    public class UpdateBrandingDto
    {
        public string? LogoUrl { get; set; }
        public string? FaviconUrl { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? FooterText { get; set; }
        public string? EmailTemplateJson { get; set; }
    }

    public class AssignRoleDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty;
    }

    public class RemoveRoleDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string Role { get; set; } = string.Empty;
    }
}

