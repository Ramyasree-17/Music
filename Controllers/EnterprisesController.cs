using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using TunewaveAPIDB1.Models;
using TunewaveAPIDB1.Services;
using TunewaveAPIDB1.Common;
using System.Linq;

namespace TunewaveAPIDB1.Controllers
{
    [ApiController]
    [Route("api/enterprises")]
    [Tags("Section 3 - Enterprises")]
    [Authorize]
    public class EnterprisesController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _connStr;
        private readonly IWebHostEnvironment _env;
        private readonly PasswordService _passwordService;
        private readonly ZohoBooksService _zohoBooksService;
        private readonly ILogger<EnterprisesController>? _logger;

        public EnterprisesController(
            IConfiguration cfg,
            IWebHostEnvironment env,
            PasswordService passwordService,
            ZohoBooksService zohoBooksService,
            ILogger<EnterprisesController>? logger = null)
        {
            _cfg = cfg;
            _connStr = cfg.GetConnectionString("DefaultConnection")!;
            _env = env;
            _passwordService = passwordService;
            _zohoBooksService = zohoBooksService;
            _logger = logger;
        }


        [HttpPost]
        [Authorize(Policy = "EnterpriseAdminOrSuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateEnterprise([FromForm] EnterpriseCreateV2 req)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Validate email is required for billing
                if (string.IsNullOrWhiteSpace(req.OwnerEmail))
                {
                    return BadRequest(new { error = "OwnerEmail is required for billing integration" });
                }

                // Handle ISRC certificate file upload
                string? isrcCertificateUrl = null;
                if (req.IsrcCertificateFile != null && req.IsrcCertificateFile.Length > 0)
                {
                    // Validate file type (allow PDF, images, etc.)
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(req.IsrcCertificateFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { error = $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}" });
                    }

                    // Validate file size (max 10MB)
                    const long maxFileSize = 10 * 1024 * 1024; // 10MB
                    if (req.IsrcCertificateFile.Length > maxFileSize)
                    {
                        return BadRequest(new { error = "File size exceeds maximum allowed size of 10MB" });
                    }

                    // Save file to wwwroot/certificates
                    string webRootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string certificatesFolder = Path.Combine(webRootPath, "certificates");

                    if (!Directory.Exists(certificatesFolder))
                        Directory.CreateDirectory(certificatesFolder);

                    var safeFileName = Path.GetFileName(req.IsrcCertificateFile.FileName);
                    string fileName = $"{Guid.NewGuid():N}_{safeFileName}";
                    string filePath = Path.Combine(certificatesFolder, fileName);
                    string relativePath = $"/certificates/{fileName}";

                    // Save file to disk
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await req.IsrcCertificateFile.CopyToAsync(stream);
                    }

                    // Set the URL to the relative path
                    isrcCertificateUrl = relativePath;
                }

                // Validate: If HasIsrcMasterCode is true, ISRC fields must be provided
                if (req.HasIsrcMasterCode)
                {
                    if (string.IsNullOrWhiteSpace(req.AudioMasterCode) &&
                        string.IsNullOrWhiteSpace(req.VideoMasterCode) &&
                        string.IsNullOrWhiteSpace(isrcCertificateUrl))
                    {
                        return BadRequest(new { error = "When HasIsrcMasterCode is true, at least one ISRC field (AudioMasterCode, VideoMasterCode, or IsrcCertificateFile) must be provided" });
                    }
                }

                var createdBy = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_CreateEnterprise_AutoOwner", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@EnterpriseName", req.EnterpriseName);
                cmd.Parameters.AddWithValue("@Domain", string.IsNullOrWhiteSpace(req.Domain) ? DBNull.Value : req.Domain);
                cmd.Parameters.AddWithValue("@RevenueShare", req.RevenueShare);
                cmd.Parameters.AddWithValue("@QCRequired", req.QCRequired);
                cmd.Parameters.AddWithValue("@OwnerEmail", req.OwnerEmail);
                cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(req.Phone) ? DBNull.Value : req.Phone);
                cmd.Parameters.AddWithValue("@AgreementStartDate", req.AgreementStartDate);
                cmd.Parameters.AddWithValue("@AgreementEndDate", req.AgreementEndDate);

                // ISRC / master code fields
                cmd.Parameters.AddWithValue("@HasIsrcMasterCode", req.HasIsrcMasterCode);
                cmd.Parameters.AddWithValue("@AudioMasterCode", string.IsNullOrWhiteSpace(req.AudioMasterCode) ? DBNull.Value : req.AudioMasterCode);
                cmd.Parameters.AddWithValue("@VideoMasterCode", string.IsNullOrWhiteSpace(req.VideoMasterCode) ? DBNull.Value : req.VideoMasterCode);
                cmd.Parameters.AddWithValue("@IsrcCertificateUrl", string.IsNullOrWhiteSpace(isrcCertificateUrl) ? DBNull.Value : isrcCertificateUrl);

                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

                await conn.OpenAsync();

                int enterpriseId;
                int? ownerUserId;
                string? ownerStatus;

                // Read data from stored procedure and close reader before executing second command
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return BadRequest(new { error = "Enterprise creation failed" });

                    enterpriseId = Convert.ToInt32(reader["EnterpriseID"]);
                    ownerUserId = reader["OwnerUserID"] != DBNull.Value ? Convert.ToInt32(reader["OwnerUserID"]) : (int?)null;
                    ownerStatus = reader["OwnerStatus"]?.ToString();
                } // Reader is closed here

                // Ensure creator (SuperAdmin) is also associated with the enterprise
                // This allows SuperAdmin to see the enterprise in their entities list
                if (createdBy != ownerUserId)
                {
                    using var assocCmd = new SqlCommand(@"
                        IF NOT EXISTS (
                            SELECT 1 FROM EnterpriseUserRoles 
                            WHERE EnterpriseID = @EnterpriseId AND UserID = @UserId
                        )
                        BEGIN
                            INSERT INTO EnterpriseUserRoles (EnterpriseID, UserID, Role, CreatedAt)
                            VALUES (@EnterpriseId, @UserId, 'EnterpriseAdmin', SYSUTCDATETIME());
                        END", conn);
                    assocCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    assocCmd.Parameters.AddWithValue("@UserId", createdBy);
                    await assocCmd.ExecuteNonQueryAsync();
                }

                // =====================================================
                // 🔥 AUTO CREATE DEFAULT BRANDING FOR NEW ENTERPRISE
                // =====================================================
                using (var brandingCmd = new SqlCommand(@"
                    INSERT INTO Branding (
                        OwnerType,
                        OwnerId,
                        EnterpriseID,
                        DomainName,
                        SiteName,
                        ContactEmail,
                        PrimaryColor,
                        SecondaryColor,
                        HeaderColor,
                        SidebarColor,
                        FooterColor,
                        LogoUrl,
                        IsActive,
                        CreatedAt
                    )
                    VALUES (
                        'Enterprise',
                        @EnterpriseID,
                        @EnterpriseID,
                        @DomainName,
                        @SiteName,
                        @ContactEmail,
                        @PrimaryColor,
                        @SecondaryColor,
                        @HeaderColor,
                        @SidebarColor,
                        @FooterColor,
                        @LogoUrl,
                        1,
                        SYSUTCDATETIME()
                    )", conn))
                {
                    brandingCmd.Parameters.AddWithValue("@EnterpriseID", enterpriseId);
                    brandingCmd.Parameters.AddWithValue("@DomainName",
                        string.IsNullOrWhiteSpace(req.Domain) ? DBNull.Value : req.Domain);

                    brandingCmd.Parameters.AddWithValue("@SiteName", req.EnterpriseName);
                    brandingCmd.Parameters.AddWithValue("@ContactEmail", req.OwnerEmail);

                    // ✅ DEFAULT TUNEWAVE COLORS
                    brandingCmd.Parameters.AddWithValue("@PrimaryColor", BrandingDefaults.PrimaryColor);
                    brandingCmd.Parameters.AddWithValue("@SecondaryColor", BrandingDefaults.SecondaryColor);
                    brandingCmd.Parameters.AddWithValue("@HeaderColor", BrandingDefaults.HeaderColor);
                    brandingCmd.Parameters.AddWithValue("@SidebarColor", BrandingDefaults.SidebarColor);
                    brandingCmd.Parameters.AddWithValue("@FooterColor", BrandingDefaults.FooterColor);

                    // ✅ DEFAULT LOGO
                    brandingCmd.Parameters.AddWithValue("@LogoUrl", BrandingDefaults.DefaultLogo);

                    await brandingCmd.ExecuteNonQueryAsync();
                }

                // ============================================
                // ZOHO BOOKS INTEGRATION
                // Create Zoho Customer with email for billing
                // Recurring invoice will be created later via separate API
                // ============================================
                string? zohoCustomerId = null;
                string? zohoError = null;

                try
                {
                    // Step 1: Create customer in Zoho Books using OwnerEmail and Phone
                    // Email will be used by Zoho to send recurring invoices automatically
                    // Phone will be used for contact and SMS/WhatsApp (if enabled in Zoho)
                    var zohoCustomer = await _zohoBooksService.CreateCustomerAsync(
                        req.EnterpriseName,
                        req.OwnerEmail,
                        req.Phone
                    );

                    if (zohoCustomer == null || string.IsNullOrWhiteSpace(zohoCustomer.ContactId))
                    {
                        zohoError = "Zoho customer creation failed";
                        _logger?.LogWarning("Zoho customer creation failed for enterprise {EnterpriseId}. Email: {Email}",
                            enterpriseId, req.OwnerEmail);
                    }
                    else
                    {
                        zohoCustomerId = zohoCustomer.ContactId;
                        _logger?.LogInformation("Zoho customer created successfully: CustomerId={CustomerId}, EnterpriseId={EnterpriseId}, Email={Email}",
                            zohoCustomerId, enterpriseId, req.OwnerEmail);

                        // Step 2: Save ZohoCustomerId to Enterprises table
                        using var updateZohoCmd = new SqlCommand(@"
                    UPDATE Enterprises
                    SET ZohoCustomerId = @ZohoCustomerId,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE EnterpriseID = @EnterpriseId", conn);

                        updateZohoCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                        updateZohoCmd.Parameters.AddWithValue("@ZohoCustomerId", zohoCustomerId);

                        await updateZohoCmd.ExecuteNonQueryAsync();

                        _logger?.LogInformation("ZohoCustomerId saved to Enterprise {EnterpriseId}: {ZohoCustomerId}",
                            enterpriseId, zohoCustomerId);

                        // 🔹 CREATE RECURRING INVOICE (AUTOMATIC)
                        try
                        {
                            var defaultItemId = _cfg["ZohoBooks:DefaultItemId"];
                            var defaultAmount = _cfg.GetValue<decimal>("ZohoBooks:DefaultMonthlyAmount", 25000);
                            
                            if (string.IsNullOrWhiteSpace(defaultItemId))
                            {
                                _logger?.LogWarning("ZohoBooks:DefaultItemId not configured. Skipping recurring invoice creation.");
                                zohoError = "Zoho customer created but recurring invoice skipped (DefaultItemId not configured)";
                            }
                            else
                            {
                                var recurringInvoiceResult = await _zohoBooksService.CreateRecurringInvoiceEnhancedAsync(
                                    zohoCustomerId,
                                    $"Monthly Subscription - {req.EnterpriseName}",
                                    "INR",
                                    "months",
                                    1,
                                    DateTime.Today.ToString("yyyy-MM-dd"),
                                    new List<ZohoLineItemRequest>
                                    {
                                        new ZohoLineItemRequest
                                        {
                                            ItemId = defaultItemId,
                                            Rate = defaultAmount,
                                            Quantity = 1
                                        }
                                    },
                                    "Auto subscription",
                                    "Net 15"
                                );

                                if (recurringInvoiceResult.Code == 0 &&
                                    recurringInvoiceResult.RecurringInvoice != null &&
                                    !string.IsNullOrWhiteSpace(recurringInvoiceResult.RecurringInvoice.RecurringInvoiceId))
                                {
                                    var recurringInvoiceId = recurringInvoiceResult.RecurringInvoice.RecurringInvoiceId;

                                    // Save ZohoRecurringInvoiceId to Enterprises table
                                    using var updateRecurringCmd = new SqlCommand(@"
                                        UPDATE Enterprises
                                        SET ZohoRecurringInvoiceId = @ZohoRecurringInvoiceId,
                                            UpdatedAt = SYSUTCDATETIME()
                                        WHERE EnterpriseID = @EnterpriseId", conn);

                                    updateRecurringCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                                    updateRecurringCmd.Parameters.AddWithValue("@ZohoRecurringInvoiceId", recurringInvoiceId);

                                    await updateRecurringCmd.ExecuteNonQueryAsync();

                                    _logger?.LogInformation("Zoho recurring invoice created successfully: RecurringInvoiceId={RecurringInvoiceId}, EnterpriseId={EnterpriseId}",
                                        recurringInvoiceId, enterpriseId);
                                }
                                else
                                {
                                    _logger?.LogWarning("Zoho recurring invoice creation failed: Code={Code}, Message={Message}",
                                        recurringInvoiceResult.Code, recurringInvoiceResult.Message);
                                    zohoError = $"Zoho customer created but recurring invoice failed: {recurringInvoiceResult.Message ?? "Unknown error"}";
                                }
                            }
                        }
                        catch (Exception recurringEx)
                        {
                            _logger?.LogError(recurringEx, "Failed to create recurring invoice for enterprise {EnterpriseId}", enterpriseId);
                            zohoError = $"Zoho customer created but recurring invoice error: {recurringEx.Message}";
                        }
                    }
                }
                catch (Exception zohoEx)
                {
                    // Log error but don't fail enterprise creation if Zoho fails
                    zohoError = $"Zoho customer creation error: {zohoEx.Message}";
                    _logger?.LogError(zohoEx, "Failed to create Zoho customer for enterprise {EnterpriseId}. Enterprise created successfully but Zoho integration failed. Email: {Email}",
                        enterpriseId, req.OwnerEmail);
                }

                // Get recurring invoice ID if it was created
                string? zohoRecurringInvoiceId = null;
                if (!string.IsNullOrWhiteSpace(zohoCustomerId))
                {
                    using var getRecurringCmd = new SqlCommand(@"
                        SELECT ZohoRecurringInvoiceId 
                        FROM Enterprises 
                        WHERE EnterpriseID = @EnterpriseId", conn);
                    getRecurringCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    var result = await getRecurringCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        zohoRecurringInvoiceId = result.ToString();
                }

                // Determine honest response message based on actual outcome
                string finalMessage;
                if (!string.IsNullOrWhiteSpace(zohoCustomerId) && !string.IsNullOrWhiteSpace(zohoRecurringInvoiceId) && string.IsNullOrWhiteSpace(zohoError))
                {
                    finalMessage = "Enterprise created successfully. Zoho customer and recurring invoice created automatically.";
                }
                else if (!string.IsNullOrWhiteSpace(zohoCustomerId))
                {
                    finalMessage = "Enterprise created successfully. Zoho customer created. Recurring invoice pending.";
                }
                else
                {
                    finalMessage = "Enterprise created successfully. Zoho integration failed.";
                }

                return StatusCode(201, new
                {
                    status = "success",
                    message = finalMessage,
                    enterpriseId = enterpriseId,
                    ownerUserId = ownerUserId,
                    ownerStatus = ownerStatus,
                    zohoCustomerId = zohoCustomerId,
                    zohoRecurringInvoiceId = zohoRecurringInvoiceId,
                    zohoError = zohoError
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
        public IActionResult GetEnterprises()
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                SqlCommand cmd;

                if (role == "EnterpriseAdmin")
                {
                    cmd = new SqlCommand("sp_GetEnterpriseByOwner", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("@OwnerId", userId);
                }
                else
                {
                    cmd = new SqlCommand("sp_GetAllEnterprises_Filter", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("@Status", DBNull.Value);
                    cmd.Parameters.AddWithValue("@Search", DBNull.Value);
                }

                conn.Open();
                using var reader = cmd.ExecuteReader();
                var list = new List<object>();

                while (reader.Read())
                {
                    list.Add(new
                    {
                        enterpriseId = reader["EnterpriseID"],
                        enterpriseName = reader["EnterpriseName"],
                        domain = reader["Domain"],
                        revenueShare = reader["RevenueShare"],
                        qcRequired = reader["QCRequired"],
                        status = reader["Status"],
                        hasIsrcMasterCode = HasColumn(reader, "HasIsrcMasterCode") && reader["HasIsrcMasterCode"] != DBNull.Value ? Convert.ToBoolean(reader["HasIsrcMasterCode"]) : false,
                        audioMasterCode = HasColumn(reader, "AudioMasterCode") && reader["AudioMasterCode"] != DBNull.Value ? reader["AudioMasterCode"] : null,
                        videoMasterCode = HasColumn(reader, "VideoMasterCode") && reader["VideoMasterCode"] != DBNull.Value ? reader["VideoMasterCode"] : null,
                        isrcCertificateUrl = HasColumn(reader, "IsrcCertificateUrl") && reader["IsrcCertificateUrl"] != DBNull.Value ? reader["IsrcCertificateUrl"] : null,
                        owner = new
                        {
                            userId = reader["OwnerID"],
                            fullName = reader["OwnerName"],
                            email = reader["OwnerEmail"]
                        },
                        createdBy = reader["CreatedByName"],
                        createdAt = reader["CreatedAt"]
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

        [HttpGet("{enterpriseId}")]
        public async Task<IActionResult> GetEnterprise(int enterpriseId)
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
                    // EnterpriseAdmin can only see enterprises they have access to
                    using var accessCmd = new SqlCommand(@"
                        SELECT 1
                        FROM Enterprises e
                        INNER JOIN EnterpriseUserRoles eur ON eur.EnterpriseID = e.EnterpriseID
                        WHERE e.EnterpriseID = @EnterpriseId 
                          AND eur.UserID = @UserId
                          AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)", conn);
                    accessCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    accessCmd.Parameters.AddWithValue("@UserId", userId);
                    var hasAccess = await accessCmd.ExecuteScalarAsync();
                    if (hasAccess == null)
                        return StatusCode(403, new { error = "You do not have access to this enterprise" });
                }
                else if (role == "LabelAdmin")
                {
                    // LabelAdmin can see enterprises that have labels they manage
                    using var accessCmd = new SqlCommand(@"
                        SELECT 1
                        FROM Enterprises e
                        INNER JOIN Labels l ON l.EnterpriseID = e.EnterpriseID
                        INNER JOIN UserLabelRoles ulr ON ulr.LabelID = l.LabelID
                        WHERE e.EnterpriseID = @EnterpriseId 
                          AND ulr.UserID = @UserId
                          AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)
                          AND (l.IsDeleted = 0 OR l.IsDeleted IS NULL)", conn);
                    accessCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    accessCmd.Parameters.AddWithValue("@UserId", userId);
                    var hasAccess = await accessCmd.ExecuteScalarAsync();
                    if (hasAccess == null)
                        return StatusCode(403, new { error = "You do not have access to this enterprise" });
                }
                else if (role != "SuperAdmin")
                {
                    return StatusCode(403, new { error = "Unauthorized" });
                }

                // Get enterprise details
                // First try to get owner from OwnerUserID, if null, get from EnterpriseUserRoles
                using var cmd = new SqlCommand(@"
                    SELECT 
                        e.EnterpriseID,
                        e.EnterpriseName,
                        e.Domain,
                        e.RevenueSharePercent AS RevenueShare,
                        e.QCRequired,
                        e.Status,
                        e.HasIsrcMasterCode,
                        e.AudioMasterCode,
                        e.VideoMasterCode,
                        e.IsrcCertificateUrl,
                        COALESCE(e.OwnerUserID, 
                            (SELECT TOP 1 eur.UserID 
                             FROM EnterpriseUserRoles eur 
                             WHERE eur.EnterpriseID = e.EnterpriseID 
                               AND eur.Role = 'EnterpriseAdmin' 
                             ORDER BY eur.CreatedAt ASC)) AS OwnerUserID,
                        e.AgreementStartDate,
                        e.AgreementEndDate,
                        e.CreatedAt,
                        u.UserID AS OwnerID,
                        u.FullName AS OwnerName,
                        u.Email AS OwnerEmail,
                        creator.UserID AS CreatedByID,
                        creator.FullName AS CreatedByName
                    FROM Enterprises e
                    LEFT JOIN Users u ON COALESCE(e.OwnerUserID, 
                        (SELECT TOP 1 eur.UserID 
                         FROM EnterpriseUserRoles eur 
                         WHERE eur.EnterpriseID = e.EnterpriseID 
                           AND eur.Role = 'EnterpriseAdmin' 
                         ORDER BY eur.CreatedAt ASC)) = u.UserID
                    LEFT JOIN Users creator ON e.CreatedBy = creator.UserID
                    WHERE e.EnterpriseID = @EnterpriseId
                      AND (e.IsDeleted = 0 OR e.IsDeleted IS NULL)", conn);

                cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        enterpriseId = reader["EnterpriseID"],
                        enterpriseName = reader["EnterpriseName"],
                        domain = reader["Domain"] == DBNull.Value ? null : reader["Domain"],
                        revenueShare = reader["RevenueShare"],
                        qcRequired = reader["QCRequired"],
                        status = reader["Status"],
                        hasIsrcMasterCode = reader["HasIsrcMasterCode"] == DBNull.Value ? false : Convert.ToBoolean(reader["HasIsrcMasterCode"]),
                        audioMasterCode = reader["AudioMasterCode"] == DBNull.Value ? null : reader["AudioMasterCode"],
                        videoMasterCode = reader["VideoMasterCode"] == DBNull.Value ? null : reader["VideoMasterCode"],
                        isrcCertificateUrl = reader["IsrcCertificateUrl"] == DBNull.Value ? null : reader["IsrcCertificateUrl"],
                        ownerId = reader["OwnerUserID"] == DBNull.Value ? null : reader["OwnerUserID"],
                        owner = new
                        {
                            userId = reader["OwnerID"] == DBNull.Value ? null : reader["OwnerID"],
                            fullName = reader["OwnerName"] == DBNull.Value ? null : reader["OwnerName"],
                            email = reader["OwnerEmail"] == DBNull.Value ? null : reader["OwnerEmail"]
                        },
                        agreementStartDate = reader["AgreementStartDate"] == DBNull.Value ? null : reader["AgreementStartDate"],
                        agreementEndDate = reader["AgreementEndDate"] == DBNull.Value ? null : reader["AgreementEndDate"],
                        createdBy = reader["CreatedByName"] == DBNull.Value ? null : reader["CreatedByName"],
                        createdAt = reader["CreatedAt"]
                    });
                }

                return NotFound(new { error = "Enterprise not found" });
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
        [HttpPut("{enterpriseId}")]
        public async Task<IActionResult> UpdateEnterprise(
    int enterpriseId,
    [FromBody] EnterpriseUpdateRequest req)
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                if (role != "SuperAdmin" && role != "EnterpriseAdmin")
                    return StatusCode(403, new { error = "Unauthorized" });

                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // ================================
                // 1️⃣ GET OLD OWNER USER ID
                // ================================
                int? oldOwnerUserId = null;

                using (var getOldOwnerCmd = new SqlCommand(@"
            SELECT OwnerUserID
            FROM Enterprises
            WHERE EnterpriseID = @EnterpriseId", conn))
                {
                    getOldOwnerCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    var result = await getOldOwnerCmd.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                        oldOwnerUserId = Convert.ToInt32(result);
                }

                if (!oldOwnerUserId.HasValue)
                    return BadRequest(new { error = "Old owner not found" });

                // ================================
                // 2️⃣ UPDATE BASIC ENTERPRISE DATA
                // ================================
                using (var cmd = new SqlCommand("sp_UpdateEnterprise", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    cmd.Parameters.AddWithValue("@Domain",
                        string.IsNullOrWhiteSpace(req.Domain) ? DBNull.Value : req.Domain);
                    cmd.Parameters.AddWithValue("@RevenueShare",
                        req.RevenueShare.HasValue ? req.RevenueShare.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@UpdatedBy", userId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return NotFound(new { error = "Enterprise not found" });
                }

                // ================================
                // 3️⃣ OWNER REPLACEMENT (NO NEW USER)
                // ================================
                if (!string.IsNullOrWhiteSpace(req.OwnerEmail))
                {
                    // 🔹 Update OLD USER row with NEW email
                    using (var updateUserCmd = new SqlCommand(@"
                UPDATE Users
                SET Email = @NewEmail,
                    FullName = @NewEmail,
                    Role = 'EnterpriseAdmin',
                    UpdatedAt = SYSUTCDATETIME()
                WHERE UserID = @UserId", conn))
                    {
                        updateUserCmd.Parameters.AddWithValue("@NewEmail", req.OwnerEmail);
                        updateUserCmd.Parameters.AddWithValue("@UserId", oldOwnerUserId.Value);
                        await updateUserCmd.ExecuteNonQueryAsync();
                    }

                    // ================================
                    // 4️⃣ ENSURE ENTERPRISE ROLE EXISTS
                    // ================================
                    using (var ensureRoleCmd = new SqlCommand(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM EnterpriseUserRoles
                    WHERE EnterpriseID = @EnterpriseId
                      AND UserID = @UserId
                      AND Role = 'EnterpriseAdmin'
                )
                BEGIN
                    INSERT INTO EnterpriseUserRoles (
                        EnterpriseID,
                        UserID,
                        Role,
                        CreatedAt
                    )
                    VALUES (
                        @EnterpriseId,
                        @UserId,
                        'EnterpriseAdmin',
                        SYSUTCDATETIME()
                    )
                END", conn))
                    {
                        ensureRoleCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                        ensureRoleCmd.Parameters.AddWithValue("@UserId", oldOwnerUserId.Value);
                        await ensureRoleCmd.ExecuteNonQueryAsync();
                    }

                    // ================================
                    // 5️⃣ UPDATE ENTERPRISE OWNER (SAME USERID)
                    // ================================
                    using (var updateOwnerCmd = new SqlCommand(@"
                UPDATE Enterprises
                SET OwnerUserID = @UserId,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE EnterpriseID = @EnterpriseId", conn))
                    {
                        updateOwnerCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                        updateOwnerCmd.Parameters.AddWithValue("@UserId", oldOwnerUserId.Value);
                        await updateOwnerCmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new
                {
                    status = "success",
                    message = "Enterprise updated. Old owner replaced with new owner successfully."
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

        [HttpPost("{enterpriseId}/status")]
        [Authorize(Policy = "SuperAdmin")]
        public IActionResult UpdateStatus(int enterpriseId, [FromBody] ChangeStatusDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

                using var conn = new SqlConnection(_connStr);
                using var cmd = new SqlCommand("sp_UpdateEnterpriseStatus", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                cmd.Parameters.AddWithValue("@Status", dto.Status);
                cmd.Parameters.AddWithValue("@UpdatedBy", userId);

                conn.Open();
                using var reader = cmd.ExecuteReader();
                if (reader.Read() && reader["EnterpriseID"] != DBNull.Value)
                {
                    return Ok(new
                    {
                        enterpriseId = reader["EnterpriseID"],
                        enterpriseName = reader["EnterpriseName"],
                        status = reader["Status"],
                        message = "Enterprise status updated successfully"
                    });
                }

                return NotFound(new { error = "Enterprise not found" });
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
        /// Retry Zoho recurring invoice creation for an existing enterprise
        /// </summary>
        [HttpPost("{enterpriseId}/retry-recurring-invoice")]
        [Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> RetryRecurringInvoice(int enterpriseId)
        {
            try
            {
                using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();

                // Get enterprise details
                string? zohoCustomerId = null;
                string? existingRecurringInvoiceId = null;
                string? enterpriseName = null;

                using (var getEnterpriseCmd = new SqlCommand(@"
                    SELECT EnterpriseName, ZohoCustomerId, ZohoRecurringInvoiceId
                    FROM Enterprises
                    WHERE EnterpriseID = @EnterpriseId", conn))
                {
                    getEnterpriseCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    using var r = await getEnterpriseCmd.ExecuteReaderAsync();
                    if (!await r.ReadAsync())
                        return NotFound(new { error = "Enterprise not found" });

                    enterpriseName = r["EnterpriseName"]?.ToString();
                    zohoCustomerId = r["ZohoCustomerId"] as string;
                    existingRecurringInvoiceId = r["ZohoRecurringInvoiceId"] as string;
                }

                if (string.IsNullOrEmpty(zohoCustomerId))
                    return BadRequest(new { error = "Zoho customer ID not found. Create customer first." });

                if (!string.IsNullOrEmpty(existingRecurringInvoiceId))
                    return BadRequest(new { error = "Recurring invoice already exists. RecurringInvoiceId: " + existingRecurringInvoiceId });

                // Create recurring invoice with unique name
                var defaultItemId = _cfg["ZohoBooks:DefaultItemId"];
                var defaultAmount = _cfg.GetValue<decimal>("ZohoBooks:DefaultMonthlyAmount", 10000);

                if (string.IsNullOrWhiteSpace(defaultItemId))
                    return BadRequest(new { error = "ZohoBooks:DefaultItemId not configured" });

                var uniqueInvoiceName = $"TuneWave-Ent-{enterpriseId}-{DateTime.UtcNow.Ticks}";

                var rid = await _zohoBooksService.CreateRecurringInvoiceEnhancedAsync(
                    customerId: zohoCustomerId,
                    recurrenceName: uniqueInvoiceName,
                    currencyCode: "INR",
                    recurrenceFrequency: "months",
                    repeatEvery: 1,
                    startDate: DateTime.Today.ToString("yyyy-MM-dd"),
                    lineItems: new List<ZohoLineItemRequest>
                    {
                        new ZohoLineItemRequest
                        {
                            ItemId = defaultItemId,
                            Rate = defaultAmount,
                            Quantity = 1
                        }
                    },
                    notes: "Auto subscription",
                    terms: "Net 15"
                );

                if (rid?.RecurringInvoice?.RecurringInvoiceId != null)
                {
                    var recurringInvoiceId = rid.RecurringInvoice.RecurringInvoiceId;

                    using var saveRecurringCmd = new SqlCommand(@"
                        UPDATE Enterprises
                        SET ZohoRecurringInvoiceId = @Rid,
                            UpdatedAt = SYSUTCDATETIME()
                        WHERE EnterpriseID = @EnterpriseId", conn);

                    saveRecurringCmd.Parameters.AddWithValue("@Rid", recurringInvoiceId);
                    saveRecurringCmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
                    await saveRecurringCmd.ExecuteNonQueryAsync();

                    _logger?.LogInformation("Recurring invoice created via retry API: EnterpriseId={EnterpriseId}, RecurringInvoiceId={RecurringInvoiceId}",
                        enterpriseId, recurringInvoiceId);

                    return Ok(new
                    {
                        status = "success",
                        message = "Recurring invoice created successfully",
                        enterpriseId,
                        zohoRecurringInvoiceId = recurringInvoiceId
                    });
                }
                else
                {
                    var errorMsg = "Unknown error";
                    _logger?.LogWarning("Recurring invoice retry failed: EnterpriseId={EnterpriseId}, Code={Code}, Message={Message}",
                        enterpriseId, rid?.Code ?? -1, errorMsg);
                    return BadRequest(new
                    {
                        error = "Recurring invoice creation failed",
                        code = rid?.Code ?? -1,
                        message = errorMsg
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception in retry recurring invoice for Enterprise {EnterpriseId}", enterpriseId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Calculates next billing date based on start date and billing day of month
        /// </summary>
        private DateTime CalculateNextBillingDate(DateTime startDate, int billingDayOfMonth)
        {
            var dayOfMonth = startDate.Day;
            var targetDay = billingDayOfMonth > 0 ? billingDayOfMonth : dayOfMonth;

            var nextMonth = startDate.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
            var actualDay = Math.Min(targetDay, daysInMonth);

            return new DateTime(nextMonth.Year, nextMonth.Month, actualDay);
        }
    }
}
