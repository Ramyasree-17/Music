using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using TunewaveAPIDB1.Common;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Services;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/labels")]
    [Tags("Section 4 - Labels")]
    [Authorize]
    public class LabelsController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;
        private readonly IWebHostEnvironment _env;

        public LabelsController(IConfiguration cfg, IWebHostEnvironment env)
        {
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
            _env = env;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateLabelDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Handle ISRC certificate file upload
                string? isrcCertificateUrl = null;
                if (dto.IsrcCertificateFile != null && dto.IsrcCertificateFile.Length > 0)
                {
                    // Validate file type (allow PDF, images, etc.)
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(dto.IsrcCertificateFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { error = $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}" });
                    }

                    // Validate file size (max 10MB)
                    const long maxFileSize = 10 * 1024 * 1024; // 10MB
                    if (dto.IsrcCertificateFile.Length > maxFileSize)
                    {
                        return BadRequest(new { error = "File size exceeds maximum allowed size of 10MB" });
                    }

                    // Save file to wwwroot/certificates
                    string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string certificatesFolder = Path.Combine(webRootPath, "certificates");

                    if (!Directory.Exists(certificatesFolder))
                        Directory.CreateDirectory(certificatesFolder);

                    var safeFileName = Path.GetFileName(dto.IsrcCertificateFile.FileName);
                    string fileName = $"{Guid.NewGuid():N}_{safeFileName}";
                    string filePath = Path.Combine(certificatesFolder, fileName);
                    string relativePath = $"/certificates/{fileName}";

                    // Save file to disk
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await dto.IsrcCertificateFile.CopyToAsync(stream);
                    }

                    // Set the URL to the relative path
                    isrcCertificateUrl = relativePath;
                }

                // Validate: If HasIsrcMasterCode is true, ISRC fields must be provided
                if (dto.HasIsrcMasterCode)
                {
                    if (string.IsNullOrWhiteSpace(dto.AudioMasterCode) &&
                        string.IsNullOrWhiteSpace(dto.VideoMasterCode) &&
                        string.IsNullOrWhiteSpace(isrcCertificateUrl))
                    {
                        return BadRequest(new { error = "When HasIsrcMasterCode is true, at least one ISRC field (AudioMasterCode, VideoMasterCode, or IsrcCertificateFile) must be provided" });
                    }
                }

                // ====================================================================
                // AGREEMENT DATE VALIDATION - Chesava (Done)
                // ====================================================================
                // Condition 1: AgreementEndDate must be greater than AgreementStartDate
                // Condition 2: AgreementEndDate must be at least 182 days (6 months) after AgreementStartDate
                // ====================================================================
                if (dto.AgreementEndDate <= dto.AgreementStartDate)
                {
                    return BadRequest(new { error = "AgreementEndDate must be greater than AgreementStartDate. End date cannot be less than or equal to start date." });
                }

                var daysDifference = (dto.AgreementEndDate - dto.AgreementStartDate).TotalDays;
                const int minimumDays = 182; // 6 months minimum

                if (daysDifference < minimumDays)
                {
                    return BadRequest(new { error = $"AgreementEndDate must be at least {minimumDays} days (6 months) after AgreementStartDate. Current difference is {daysDifference:F0} days." });
                }

                // ====================================================================
                // REQUIREMENT 1: Only Starter (1) and Growth (2) plans are allowed
                // Enterprise plan (3) has been removed - Chesava (Done)
                // ====================================================================
                if (dto.PlanTypeId != 1 && dto.PlanTypeId != 2)
                {
                    return BadRequest(new { error = "PlanTypeId must be 1 (Starter) or 2 (Growth). Enterprise plan is no longer available." });
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var role = User.FindFirst(ClaimTypes.Role)?.Value;

                // Only EnterpriseAdmin can create labels (not SuperAdmin directly, not LabelAdmin, not Artist)
                if (role != "EnterpriseAdmin")
                {
                    return StatusCode(403, new { error = "Only EnterpriseAdmin can create labels. You must be associated with an Enterprise." });
                }

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // ====================================================================
                // REQUIREMENT 2 & 3: Domain handling based on plan type - Chesava (Done)
                // ====================================================================
                // REQUIREMENT 2: Starter (PlanTypeId = 1) - Automatically generates random space names
                //              WITHOUT duplicates. Format: {spacename}.twsd.in
                //              Examples: jupiter.twsd.in, mass.twsd.in, mars.twsd.in, venus.twsd.in, etc.
                //              User does NOT provide domain - it's auto-generated with .twsd.in suffix.
                // REQUIREMENT 3: Growth (PlanTypeId = 2) - User puts their OWN domain name (their wish/choice)
                //              User provides their own custom domain name.
                // ====================================================================
                string? finalDomain = dto.Domain;
                
                if (dto.PlanTypeId == 1) // Starter: Automatically generate random space name (NO user input)
                {
                    // Auto-generates unique space name without duplicates (jupiter, mass, mars, etc.)
                    // User's domain input is ignored - system generates automatically
                    finalDomain = await GenerateUniqueSpaceDomainAsync(conn);
                }
                else if (dto.PlanTypeId == 2) // Growth: User provides their own domain name (their wish)
                {
                    if (string.IsNullOrWhiteSpace(dto.Domain))
                    {
                        return BadRequest(new { error = "Domain is required for Growth plan (PlanTypeId = 2). Please provide your own domain name." });
                    }
                    // User's own domain name (their wish/choice)
                    finalDomain = dto.Domain.Trim();
                    
                    // ====================================================================
                    // REQUIREMENT 4: No duplicate domain names - Chesava (Done)
                    // Checks across ALL domains (both Starter AND Growth) to prevent duplicates
                    // ====================================================================
                    if (await DomainExistsAsync(conn, finalDomain))
                    {
                        return BadRequest(new { error = $"Domain '{finalDomain}' already exists. Please choose a different domain name." });
                    }
                }

                // Auto-detect EnterpriseId from logged-in user if not provided
                int enterpriseId = dto.EnterpriseId ?? 0;
                if (enterpriseId == 0)
                {
                    using var entCmd = new SqlCommand(@"
                        SELECT TOP 1 EnterpriseID
                        FROM EnterpriseUserRoles
                        WHERE UserID = @UserId
                        ORDER BY EnterpriseID DESC;", conn);  // pick latest-created enterprise for this user
                    entCmd.Parameters.AddWithValue("@UserId", userId);

                    var entResult = await entCmd.ExecuteScalarAsync();
                    if (entResult != null && entResult != DBNull.Value)
                    {
                        enterpriseId = Convert.ToInt32(entResult);
                    }
                    else
                    {
                        return BadRequest(new { error = "EnterpriseId is required. User must be associated with an Enterprise." });
                    }
                }

                using var cmd = new SqlCommand("sp_CreateLabel", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@LabelName", dto.LabelName);
                cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);

                cmd.Parameters.AddWithValue("@PlanTypeId", dto.PlanTypeId);
                cmd.Parameters.AddWithValue("@RevenueSharePercent", dto.RevenueSharePercent);
                cmd.Parameters.AddWithValue("@Domain", string.IsNullOrWhiteSpace(finalDomain) ? DBNull.Value : finalDomain);
                cmd.Parameters.AddWithValue("@OwnerEmail", string.IsNullOrWhiteSpace(dto.OwnerEmail) ? DBNull.Value : dto.OwnerEmail);
                cmd.Parameters.AddWithValue("@QCRequired", dto.QCRequired);
                cmd.Parameters.AddWithValue("@AgreementStartDate", dto.AgreementStartDate);
                cmd.Parameters.AddWithValue("@AgreementEndDate", dto.AgreementEndDate);

                // ISRC / master code fields
                cmd.Parameters.AddWithValue("@HasIsrcMasterCode", dto.HasIsrcMasterCode);
                cmd.Parameters.AddWithValue("@AudioMasterCode", string.IsNullOrWhiteSpace(dto.AudioMasterCode) ? DBNull.Value : dto.AudioMasterCode);
                cmd.Parameters.AddWithValue("@VideoMasterCode", string.IsNullOrWhiteSpace(dto.VideoMasterCode) ? DBNull.Value : dto.VideoMasterCode);
                cmd.Parameters.AddWithValue("@IsrcCertificateUrl", string.IsNullOrWhiteSpace(isrcCertificateUrl) ? DBNull.Value : isrcCertificateUrl);

                cmd.Parameters.AddWithValue("@CreatedBy", userId);
                var result = await cmd.ExecuteScalarAsync();

                if (result == null || Convert.ToInt32(result) == 0)
                    return BadRequest(new { error = "Label creation failed" });

                var newLabelId = Convert.ToInt32(result);

                // =====================================================
                // 🔥 AUTO CREATE DEFAULT BRANDING FOR NEW LABEL
                // =====================================================
                using (var brandingCmd = new SqlCommand(@"
                    INSERT INTO Branding (
                        OwnerType,
                        OwnerId,
                        EnterpriseId,
                        DomainName,
                        SiteName,
                        SiteDescription,
                        ContactEmail,
                        PrimaryColor,
                        SecondaryColor,
                        HeaderColor,
                        SidebarColor,
                        FooterColor,
                        LogoUrl,
                        FooterText,
                        FooterLinksJson,
                        IsActive,
                        CreatedAt,
                        UpdatedAt
                    )
                    VALUES (
                        'Label',
                        @LabelId,
                        @EnterpriseId,
                        @DomainName,
                        @SiteName,
                        @SiteDescription,
                        @ContactEmail,
                        @PrimaryColor,
                        @SecondaryColor,
                        @HeaderColor,
                        @SidebarColor,
                        @FooterColor,
                        @LogoUrl,
                        @FooterText,
                        @FooterLinksJson,
                        1,
                        SYSUTCDATETIME(),
                        SYSUTCDATETIME()
                    )", conn))
                {
                    brandingCmd.Parameters.AddWithValue("@LabelId", newLabelId);
                    brandingCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    brandingCmd.Parameters.AddWithValue("@DomainName", 
                        string.IsNullOrWhiteSpace(finalDomain) ? DBNull.Value : finalDomain);
                    brandingCmd.Parameters.AddWithValue("@SiteName", dto.LabelName);
                    brandingCmd.Parameters.AddWithValue("@SiteDescription", BrandingDefaults.SiteDescription);
                    brandingCmd.Parameters.AddWithValue("@ContactEmail", 
                        string.IsNullOrWhiteSpace(dto.OwnerEmail) ? DBNull.Value : dto.OwnerEmail);

                    // ✅ DEFAULT TUNEWAVE COLORS
                    brandingCmd.Parameters.AddWithValue("@PrimaryColor", BrandingDefaults.PrimaryColor);
                    brandingCmd.Parameters.AddWithValue("@SecondaryColor", BrandingDefaults.SecondaryColor);
                    brandingCmd.Parameters.AddWithValue("@HeaderColor", BrandingDefaults.HeaderColor);
                    brandingCmd.Parameters.AddWithValue("@SidebarColor", BrandingDefaults.SidebarColor);
                    brandingCmd.Parameters.AddWithValue("@FooterColor", BrandingDefaults.FooterColor);

                    // ✅ DEFAULT LOGO
                    brandingCmd.Parameters.AddWithValue("@LogoUrl", BrandingDefaults.DefaultLogo);
                    brandingCmd.Parameters.AddWithValue("@FooterText", BrandingDefaults.FooterText);
                    brandingCmd.Parameters.AddWithValue("@FooterLinksJson",
                        JsonSerializer.Serialize(BrandingDefaults.FooterLinks));

                    await brandingCmd.ExecuteNonQueryAsync();
                }

                // Ensure label owner email exists as a User and has LabelAdmin role on this label
                if (!string.IsNullOrWhiteSpace(dto.OwnerEmail))
                {
                    int ownerUserId;

                    // 1) Check if user already exists
                    using (var getUserCmd = new SqlCommand(@"
                        SELECT TOP 1 UserID 
                        FROM Users 
                        WHERE Email = @Email;", conn))
                    {
                        getUserCmd.Parameters.AddWithValue("@Email", dto.OwnerEmail);
                        var userResult = await getUserCmd.ExecuteScalarAsync();

                        if (userResult != null && userResult != DBNull.Value)
                        {
                            ownerUserId = Convert.ToInt32(userResult);
                        }
                        else
                        {
                            // 2) Create new user with OwnerEmail (empty password, they'll use forgot password)
                            using var insertUserCmd = new SqlCommand(@"
                                INSERT INTO Users (
                                    FullName,
                                    Email,
                                    PasswordHash,
                                    Role,
                                    Status,
                                    IsActive,
                                    CreatedAt,
                                    UpdatedAt
                                )
                                VALUES (
                                    @FullName,
                                    @Email,
                                    '',              -- empty password, owner will set via forgot-password
                                    'LabelAdmin',
                                    'Active',
                                    1,
                                    SYSUTCDATETIME(),
                                    SYSUTCDATETIME()
                                );
                                SELECT SCOPE_IDENTITY();", conn);

                            insertUserCmd.Parameters.AddWithValue("@FullName", (object?)dto.LabelName ?? dto.OwnerEmail);
                            insertUserCmd.Parameters.AddWithValue("@Email", dto.OwnerEmail);

                            var newUserIdObj = await insertUserCmd.ExecuteScalarAsync();
                            ownerUserId = Convert.ToInt32(newUserIdObj);
                        }
                    }

                    // 3) Ensure owner has LabelAdmin role for this label
                    using (var ownerRoleCmd = new SqlCommand(@"
                        IF NOT EXISTS (
                            SELECT 1 FROM UserLabelRoles WHERE LabelId = @LabelId AND UserId = @UserId
                        )
                        BEGIN
                            INSERT INTO UserLabelRoles (LabelId, UserId, Role, CreatedAt)
                            VALUES (@LabelId, @UserId, @Role, SYSUTCDATETIME());
                        END", conn))
                    {
                        ownerRoleCmd.Parameters.AddWithValue("@LabelId", newLabelId);
                        ownerRoleCmd.Parameters.AddWithValue("@UserId", ownerUserId);
                        ownerRoleCmd.Parameters.AddWithValue("@Role", "LabelAdmin");
                        await ownerRoleCmd.ExecuteNonQueryAsync();
                    }
                }

                // Automatically grant creator a label role so they can see/manage it
                using (var roleCmd = new SqlCommand(@"
                    IF NOT EXISTS (
                        SELECT 1 FROM UserLabelRoles WHERE LabelId = @LabelId AND UserId = @UserId
                    )
                    BEGIN
                        INSERT INTO UserLabelRoles (LabelId, UserId, Role, CreatedAt)
                        VALUES (@LabelId, @UserId, @Role, SYSUTCDATETIME());
                    END", conn))
                {
                    roleCmd.Parameters.AddWithValue("@LabelId", newLabelId);
                    roleCmd.Parameters.AddWithValue("@UserId", userId);
                    roleCmd.Parameters.AddWithValue("@Role", "LabelAdmin");
                    await roleCmd.ExecuteNonQueryAsync();
                }

             

                return StatusCode(201, new
                {
                    message = "Label created successfully",
                    labelId = newLabelId
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetLabels()
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                SqlCommand cmd;

                if (role == "SuperAdmin")
                {
                    cmd = new SqlCommand(@"
                        SELECT 
                            LabelID,
                            LabelName,
                            EnterpriseID,
                            Status,
                            RevenueSharePercent,
                            Domain,
                            QCRequired,
                            HasIsrcMasterCode,
                            AudioMasterCode,
                            VideoMasterCode,
                            IsrcCertificateUrl
                        FROM Labels
                        WHERE IsDeleted = 0 OR IsDeleted IS NULL
                        ORDER BY CreatedAt DESC", conn);
                }
                else if (role == "EnterpriseAdmin")
                {
                    cmd = new SqlCommand(@"
                        SELECT DISTINCT 
                            l.LabelID,
                            l.LabelName,
                            l.EnterpriseID,
                            l.Status,
                            l.RevenueSharePercent,
                            l.Domain,
                            l.QCRequired,
                            l.HasIsrcMasterCode,
                            l.AudioMasterCode,
                            l.VideoMasterCode,
                            l.IsrcCertificateUrl
                        FROM Labels l
                        INNER JOIN EnterpriseUserRoles eur ON eur.EnterpriseID = l.EnterpriseID
                        WHERE eur.UserID = @UserId
                          AND (l.IsDeleted = 0 OR l.IsDeleted IS NULL)", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                }
                else
                {
                    // For regular users, get labels where they have a role
                    cmd = new SqlCommand(@"
                        SELECT DISTINCT 
                            l.LabelID,
                            l.LabelName,
                            l.EnterpriseID,
                            l.Status,
                            l.RevenueSharePercent,
                            l.Domain,
                            l.QCRequired,
                            l.HasIsrcMasterCode,
                            l.AudioMasterCode,
                            l.VideoMasterCode,
                            l.IsrcCertificateUrl
                        FROM Labels l
                        WHERE EXISTS (
                            SELECT 1 FROM UserLabelRoles ulr 
                            WHERE ulr.LabelID = l.LabelID 
                              AND ulr.UserID = @UserId
                        )", conn);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                }

                conn.Open();
                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    list.Add(new
                    {
                        labelId = reader["LabelID"],
                        labelName = reader["LabelName"],
                        enterpriseId = reader["EnterpriseID"],
                        status = reader["Status"],
                        revenueSharePercent = reader["RevenueSharePercent"],
                        domain = reader["Domain"],
                        qcRequired = reader["QCRequired"],
                        hasIsrcMasterCode = HasColumn(reader, "HasIsrcMasterCode") && reader["HasIsrcMasterCode"] != DBNull.Value ? Convert.ToBoolean(reader["HasIsrcMasterCode"]) : false,
                        audioMasterCode = HasColumn(reader, "AudioMasterCode") && reader["AudioMasterCode"] != DBNull.Value ? reader["AudioMasterCode"] : null,
                        videoMasterCode = HasColumn(reader, "VideoMasterCode") && reader["VideoMasterCode"] != DBNull.Value ? reader["VideoMasterCode"] : null,
                        isrcCertificateUrl = HasColumn(reader, "IsrcCertificateUrl") && reader["IsrcCertificateUrl"] != DBNull.Value ? reader["IsrcCertificateUrl"] : null
                    });
                }

                return Ok(list);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET /api/labels/{labelId} - get single label by id
        [HttpGet("{labelId}")]
        public async Task<IActionResult> GetLabel(int labelId)
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Check access permissions
                if (role == "EnterpriseAdmin")
                {
                    // EnterpriseAdmin can only see labels from enterprises they have access to
                    using var accessCmd = new SqlCommand(@"
                        SELECT 1
                        FROM Labels l
                        INNER JOIN EnterpriseUserRoles eur ON eur.EnterpriseID = l.EnterpriseID
                        WHERE l.LabelID = @LabelId 
                          AND eur.UserID = @UserId
                          AND (l.IsDeleted = 0 OR l.IsDeleted IS NULL)", conn);
                    accessCmd.Parameters.AddWithValue("@LabelId", labelId);
                    accessCmd.Parameters.AddWithValue("@UserId", userId);
                    var hasAccess = await accessCmd.ExecuteScalarAsync();
                    if (hasAccess == null)
                        return StatusCode(403, new { error = "You do not have access to this label" });
                }
                else if (role == "LabelAdmin")
                {
                    // LabelAdmin can only see labels they have access to
                    using var accessCmd = new SqlCommand(@"
                        SELECT 1
                        FROM UserLabelRoles
                        WHERE LabelID = @LabelId AND UserID = @UserId", conn);
                    accessCmd.Parameters.AddWithValue("@LabelId", labelId);
                    accessCmd.Parameters.AddWithValue("@UserId", userId);
                    var hasAccess = await accessCmd.ExecuteScalarAsync();
                    if (hasAccess == null)
                        return StatusCode(403, new { error = "You do not have access to this label" });
                }
                else if (role != "SuperAdmin")
                {
                    return StatusCode(403, new { error = "Unauthorized" });
                }

                using var cmd = new SqlCommand(@"
                    SELECT 
                        l.LabelID,
                        l.LabelName,
                        l.EnterpriseID,
                        l.Status,
                        l.RevenueSharePercent,
                        l.Domain,
                        l.OwnerEmail,
                        l.QCRequired,
                        l.AgreementStartDate,
                        l.AgreementEndDate,
                        l.PlanTypeId,
                        l.HasIsrcMasterCode,
                        l.AudioMasterCode,
                        l.VideoMasterCode,
                        l.IsrcCertificateUrl,
                        u.UserID AS OwnerID,
                        u.FullName AS OwnerName,
                        u.Email AS OwnerEmailAddress
                    FROM Labels l
                    LEFT JOIN Users u ON l.OwnerEmail = u.Email
                    WHERE l.LabelID = @LabelId
                      AND (l.IsDeleted = 0 OR l.IsDeleted IS NULL);", conn);

                cmd.Parameters.AddWithValue("@LabelId", labelId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        labelId = reader["LabelID"],
                        labelName = reader["LabelName"],
                        enterpriseId = reader["EnterpriseID"],
                        status = reader["Status"],
                        revenueSharePercent = reader["RevenueSharePercent"],
                        domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"],
                        ownerEmail = reader["OwnerEmail"] == DBNull.Value ? null : reader["OwnerEmail"],
                        ownerId = reader["OwnerID"] == DBNull.Value ? null : reader["OwnerID"],
                        owner = new
                        {
                            userId = reader["OwnerID"] == DBNull.Value ? null : reader["OwnerID"],
                            fullName = reader["OwnerName"] == DBNull.Value ? null : reader["OwnerName"],
                            email = reader["OwnerEmailAddress"] == DBNull.Value ? null : reader["OwnerEmailAddress"]
                        },
                        qcRequired = reader["QCRequired"],
                        agreementStartDate = reader["AgreementStartDate"] == DBNull.Value ? null : reader["AgreementStartDate"],
                        agreementEndDate = reader["AgreementEndDate"] == DBNull.Value ? null : reader["AgreementEndDate"],
                        planTypeId = reader["PlanTypeId"],
                        hasIsrcMasterCode = reader["HasIsrcMasterCode"] == DBNull.Value ? false : Convert.ToBoolean(reader["HasIsrcMasterCode"]),
                        audioMasterCode = reader["AudioMasterCode"] == DBNull.Value ? null : reader["AudioMasterCode"],
                        videoMasterCode = reader["VideoMasterCode"] == DBNull.Value ? null : reader["VideoMasterCode"],
                        isrcCertificateUrl = reader["IsrcCertificateUrl"] == DBNull.Value ? null : reader["IsrcCertificateUrl"]
                    });
                }

                return NotFound(new { error = "Label not found" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("{labelId}")]
        public async Task<IActionResult> UpdateLabel(int labelId, [FromBody] UpdateLabelDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // ================================
                // 1️⃣ GET OLD OWNER EMAIL AND USER ID
                // ================================
                string? oldOwnerEmail = null;
                int? oldOwnerUserId = null;

                using (var getOldOwnerCmd = new SqlCommand(@"
                    SELECT OwnerEmail
                    FROM Labels
                    WHERE LabelID = @LabelId
                      AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn))
                {
                    getOldOwnerCmd.Parameters.AddWithValue("@LabelId", labelId);
                    var result = await getOldOwnerCmd.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        oldOwnerEmail = result.ToString();
                    }
                }

                // If there's an old owner email, find the user
                if (!string.IsNullOrWhiteSpace(oldOwnerEmail))
                {
                    using (var getUserCmd = new SqlCommand(@"
                        SELECT TOP 1 UserID 
                        FROM Users 
                        WHERE Email = @Email;", conn))
                    {
                        getUserCmd.Parameters.AddWithValue("@Email", oldOwnerEmail);
                        var userResult = await getUserCmd.ExecuteScalarAsync();

                        if (userResult != null && userResult != DBNull.Value)
                        {
                            oldOwnerUserId = Convert.ToInt32(userResult);
                        }
                    }
                }

                // ================================
                // 2️⃣ UPDATE BASIC LABEL DATA (RevenueSharePercent, Domain, OwnerEmail)
                // ================================
                using (var cmd = new SqlCommand(@"
                    UPDATE Labels
                    SET RevenueSharePercent = CASE 
                            WHEN @RevenueSharePercent IS NOT NULL THEN @RevenueSharePercent 
                            ELSE RevenueSharePercent 
                        END,
                        Domain = CASE 
                            WHEN @Domain IS NOT NULL THEN @Domain 
                            ELSE Domain 
                        END,
                        OwnerEmail = CASE 
                            WHEN @OwnerEmail IS NOT NULL THEN @OwnerEmail 
                            ELSE OwnerEmail 
                        END,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE LabelID = @LabelId
                      AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn))
                {
                    cmd.Parameters.AddWithValue("@LabelId", labelId);
                    cmd.Parameters.AddWithValue("@RevenueSharePercent", dto.RevenueSharePercent.HasValue ? (object)dto.RevenueSharePercent.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Domain", string.IsNullOrWhiteSpace(dto.Domain) ? DBNull.Value : dto.Domain);
                    cmd.Parameters.AddWithValue("@OwnerEmail", string.IsNullOrWhiteSpace(dto.OwnerEmail) ? DBNull.Value : dto.OwnerEmail);

                    var rows = await cmd.ExecuteNonQueryAsync();
                    if (rows == 0)
                        return NotFound(new { error = "Label not found" });
                }

                // ================================
                // 3️⃣ OWNER EMAIL REPLACEMENT (like Enterprise)
                // ================================
                if (!string.IsNullOrWhiteSpace(dto.OwnerEmail) && oldOwnerUserId.HasValue)
                {
                    // 🔹 Update OLD USER row with NEW email (replace old mail with new mail)
                    using (var updateUserCmd = new SqlCommand(@"
                        UPDATE Users
                        SET Email = @NewEmail,
                            Role = 'LabelAdmin',
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE UserID = @UserId", conn))
                    {
                        updateUserCmd.Parameters.AddWithValue("@NewEmail", dto.OwnerEmail);
                        updateUserCmd.Parameters.AddWithValue("@UserId", oldOwnerUserId.Value);
                        await updateUserCmd.ExecuteNonQueryAsync();
                    }

                    // ================================
                    // 4️⃣ ENSURE LABEL ROLE EXISTS
                    // ================================
                    using (var ensureRoleCmd = new SqlCommand(@"
                        IF NOT EXISTS (
                            SELECT 1
                            FROM UserLabelRoles
                            WHERE LabelID = @LabelId
                              AND UserID = @UserId
                              AND Role = 'LabelAdmin'
                        )
                        BEGIN
                            INSERT INTO UserLabelRoles (
                                LabelID,
                                UserID,
                                Role,
                                CreatedAt
                            )
                            VALUES (
                                @LabelId,
                                @UserId,
                                'LabelAdmin',
                                SYSUTCDATETIME()
                            )
                        END", conn))
                    {
                        ensureRoleCmd.Parameters.AddWithValue("@LabelId", labelId);
                        ensureRoleCmd.Parameters.AddWithValue("@UserId", oldOwnerUserId.Value);
                        await ensureRoleCmd.ExecuteNonQueryAsync();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(dto.OwnerEmail) && !oldOwnerUserId.HasValue)
                {
                    // If there's a new owner email but no old owner, create or find the user
                    int ownerUserId;

                    // Check if user already exists with new email
                    using (var getUserCmd = new SqlCommand(@"
                        SELECT TOP 1 UserID 
                        FROM Users 
                        WHERE Email = @Email;", conn))
                    {
                        getUserCmd.Parameters.AddWithValue("@Email", dto.OwnerEmail);
                        var userResult = await getUserCmd.ExecuteScalarAsync();

                        if (userResult != null && userResult != DBNull.Value)
                        {
                            ownerUserId = Convert.ToInt32(userResult);
                        }
                        else
                        {
                            // Create new user with OwnerEmail
                            using var insertUserCmd = new SqlCommand(@"
                                INSERT INTO Users (
                                    FullName,
                                    Email,
                                    PasswordHash,
                                    Role,
                                    Status,
                                    IsActive,
                                    CreatedAt,
                                    UpdatedAt
                                )
                                VALUES (
                                    @FullName,
                                    @Email,
                                    '',
                                    'LabelAdmin',
                                    'Active',
                                    1,
                                    SYSUTCDATETIME(),
                                    SYSUTCDATETIME()
                                );
                                SELECT SCOPE_IDENTITY();", conn);

                            insertUserCmd.Parameters.AddWithValue("@FullName", dto.OwnerEmail);
                            insertUserCmd.Parameters.AddWithValue("@Email", dto.OwnerEmail);

                            var newUserIdObj = await insertUserCmd.ExecuteScalarAsync();
                            ownerUserId = Convert.ToInt32(newUserIdObj);
                        }
                    }

                    // Ensure owner has LabelAdmin role for this label
                    using (var ownerRoleCmd = new SqlCommand(@"
                        IF NOT EXISTS (
                            SELECT 1 FROM UserLabelRoles WHERE LabelId = @LabelId AND UserId = @UserId
                        )
                        BEGIN
                            INSERT INTO UserLabelRoles (LabelId, UserId, Role, CreatedAt)
                            VALUES (@LabelId, @UserId, @Role, SYSUTCDATETIME());
                        END", conn))
                    {
                        ownerRoleCmd.Parameters.AddWithValue("@LabelId", labelId);
                        ownerRoleCmd.Parameters.AddWithValue("@UserId", ownerUserId);
                        ownerRoleCmd.Parameters.AddWithValue("@Role", "LabelAdmin");
                        await ownerRoleCmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new
                {
                    status = "success",
                    message = "Label updated successfully. Owner email replaced if updated."
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST /api/labels/{labelId}/status - change label status (SuperAdmin)
        [HttpPost("{labelId}/status")]
        public IActionResult UpdateStatus(int labelId, [FromBody] ChangeStatusDto dto)
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                conn.Open();

                if (role == "EnterpriseAdmin")
                {
                    using var accessCmd = new SqlCommand(@"
                        SELECT 1
                        FROM Labels l
                        INNER JOIN EnterpriseUserRoles eur ON eur.EnterpriseID = l.EnterpriseID
                        WHERE l.LabelID = @LabelId AND eur.UserId = @UserId", conn);
                    accessCmd.Parameters.AddWithValue("@LabelId", labelId);
                    accessCmd.Parameters.AddWithValue("@UserId", userId);
                    var hasAccess = accessCmd.ExecuteScalar();
                    if (hasAccess == null)
                        return StatusCode(403, new { error = "You do not have access to this label" });
                }
                else if (role == "LabelAdmin")
                {
                    using var accessCmd = new SqlCommand(@"
                        SELECT 1
                        FROM UserLabelRoles
                        WHERE LabelId = @LabelId AND UserId = @UserId", conn);
                    accessCmd.Parameters.AddWithValue("@LabelId", labelId);
                    accessCmd.Parameters.AddWithValue("@UserId", userId);
                    var hasAccess = accessCmd.ExecuteScalar();
                    if (hasAccess == null)
                        return StatusCode(403, new { error = "You do not have access to this label" });
                }
                else if (role != "SuperAdmin")
                {
                    return StatusCode(403, new { error = "Unauthorized to update label status" });
                }

                using var cmd = new SqlCommand("sp_Labels_ChangeStatus", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@LabelId", labelId);
                cmd.Parameters.AddWithValue("@Status", dto.Status);

                var rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                    return NotFound(new { error = "Label not found" });

                using var getCmd = new SqlCommand("SELECT LabelID, LabelName, Status FROM Labels WHERE LabelID = @LabelId", conn);
                getCmd.Parameters.AddWithValue("@LabelId", labelId);
                using var reader = getCmd.ExecuteReader();

                if (reader.Read())
                {
                    return Ok(new
                    {
                        labelId = reader["LabelID"],
                        labelName = reader["LabelName"],
                        status = reader["Status"],
                        message = "Label status updated successfully"
                    });
                }

                return Ok(new { message = "Label status updated successfully" });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = $"Database error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// REQUIREMENT 2 IMPLEMENTED - Chesava (Done)
        /// Automatically generates a unique random space name domain for Starter plan
        /// WITHOUT duplicates. User does NOT provide domain - system generates automatically.
        /// Format: {spacename}.twsd.in (e.g., jupiter.twsd.in, mass.twsd.in, mars.twsd.in)
        /// 
        /// Space names: jupiter, mass, mars, venus, saturn, neptune, uranus, pluto, earth, moon, 
        /// sun, star, galaxy, nebula, comet, asteroid, orbit, cosmos, stellar, lunar, solar, etc.
        /// 
        /// REQUIREMENT 4 IMPLEMENTED - Chesava (Done)
        /// IMPORTANT: Checks against ALL existing domains (both Starter AND Growth) to prevent duplicates
        /// If space name already exists, tries another random space name automatically.
        /// </summary>
        private async Task<string> GenerateUniqueSpaceDomainAsync(SqlConnection conn)
        {
            var spaceNames = new[]
            {
                "jupiter", "mass", "mars", "venus", "saturn", "neptune", "uranus", "pluto", "earth", "moon",
                "sun", "star", "galaxy", "nebula", "comet", "asteroid", "orbit", "cosmos", "stellar", "lunar",
                "solar", "mercury", "titan", "europa", "io", "phobos", "deimos", "ceres", "pallas", "vesta",
                "apollo", "atlas", "athena", "ares", "hermes", "poseidon", "zeus", "artemis", "diana", "phoebe",
                "rhea", "iapetus", "enceladus", "mimas", "tethys", "dione", "hyperion", "iapetus", "oberon", "titania",
                "umbriel", "ariel", "miranda", "triton", "charon", "nix", "hydra", "kerberos", "styx", "eris",
                "haumea", "makemake", "sedna", "quaoar", "orcus", "ixion", "varuna", "gonggong", "salacia", "varda"
            };

            var random = new Random();
            int maxAttempts = 100; // Prevent infinite loop
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                // Pick a random space name
                var spaceName = spaceNames[random.Next(spaceNames.Length)];
                // Add .twsd.in suffix automatically for Starter plan
                var domainWithSuffix = $"{spaceName}.twsd.in";
                
                // Check if domain already exists (checks ALL domains - Starter AND Growth plans)
                if (!await DomainExistsAsync(conn, domainWithSuffix))
                {
                    return domainWithSuffix;
                }
                
                attempts++;
            }

            // If all space names are taken, fallback to space name with random suffix
            var fallbackName = spaceNames[random.Next(spaceNames.Length)];
            var suffix = random.Next(1000, 9999);
            var fallbackDomain = $"{fallbackName}{suffix}.twsd.in";
            
            // Ensure fallback is also unique
            int fallbackAttempts = 0;
            while (await DomainExistsAsync(conn, fallbackDomain) && fallbackAttempts < 50)
            {
                suffix = random.Next(1000, 9999);
                fallbackDomain = $"{fallbackName}{suffix}.twsd.in";
                fallbackAttempts++;
            }
            
            return fallbackDomain;
        }

        /// <summary>
        /// REQUIREMENT 4 IMPLEMENTED - Chesava (Done)
        /// Checks if a domain name already exists in BOTH Labels and Branding tables
        /// This checks ALL domains regardless of plan type (Starter or Growth) to prevent duplicates
        /// 
        /// Prevents:
        /// - Starter domain duplicating another Starter domain
        /// - Starter domain duplicating a Growth domain
        /// - Growth domain duplicating another Growth domain
        /// - Growth domain duplicating a Starter domain
        /// - Domain duplicating an Enterprise domain
        /// </summary>
        private async Task<bool> DomainExistsAsync(SqlConnection conn, string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            using var cmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM (
                    SELECT Domain FROM Labels WHERE (IsDeleted = 0 OR IsDeleted IS NULL)
                    UNION
                    SELECT DomainName AS Domain FROM Branding WHERE IsActive = 1
                ) d
                WHERE d.Domain = @Domain", conn);
            
            cmd.Parameters.AddWithValue("@Domain", domain);
            var result = await cmd.ExecuteScalarAsync();
            
            return result != null && Convert.ToInt32(result) > 0;
        }
    }
}