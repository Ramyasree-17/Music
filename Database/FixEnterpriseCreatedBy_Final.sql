-- =============================================
-- Step 1: Add CreatedBy column if it doesn't exist
-- =============================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'CreatedBy'
)
BEGIN
    ALTER TABLE Enterprises ADD CreatedBy INT NULL;
    PRINT 'Added CreatedBy column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'CreatedBy column already exists';
END
GO

-- =============================================
-- Step 2: Fix sp_CreateEnterprise_AutoOwner
-- =============================================
-- Uses correct column name: RevenueSharePercent (not RevenueShare)

IF OBJECT_ID('[dbo].[sp_CreateEnterprise_AutoOwner]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_CreateEnterprise_AutoOwner];
GO

CREATE PROCEDURE [dbo].[sp_CreateEnterprise_AutoOwner]
    @EnterpriseName NVARCHAR(255),
    @Domain NVARCHAR(255) = NULL,
    @RevenueShare DECIMAL(5,2),  -- Parameter name stays same, maps to RevenueSharePercent column
    @QCRequired BIT,
    @OwnerEmail NVARCHAR(255),
    @HasIsrcMasterCode BIT = 0,
    @AudioMasterCode NVARCHAR(50) = NULL,
    @VideoMasterCode NVARCHAR(50) = NULL,
    @IsrcCertificateUrl NVARCHAR(1000) = NULL,
    @CreatedBy INT = NULL  -- ✅ Added CreatedBy parameter
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @EnterpriseID INT;
    DECLARE @OwnerUserID INT = NULL;
    DECLARE @OwnerStatus NVARCHAR(50) = NULL;
    
    -- Check if owner email exists
    SELECT @OwnerUserID = UserID 
    FROM Users 
    WHERE Email = @OwnerEmail;
    
    -- If owner email doesn't exist, create a new user
    IF @OwnerUserID IS NULL
    BEGIN
        -- Create new user with owner email
        INSERT INTO Users (
            FullName,
            Email,
            PasswordHash,  -- Empty password - user will need to set it via forgot password
            Role,
            Status,
            IsActive,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @OwnerEmail,  -- Use email as FullName initially
            @OwnerEmail,
            '',  -- Empty password hash - user must set password via forgot password flow
            'EnterpriseAdmin',
            'Active',
            1,
            SYSUTCDATETIME(),
            SYSUTCDATETIME()
        );
        
        SET @OwnerUserID = SCOPE_IDENTITY();
        SET @OwnerStatus = 'CREATED';
    END
    ELSE
    BEGIN
        SET @OwnerStatus = 'EXISTING';
    END
    
    -- Insert Enterprise with CreatedBy
    INSERT INTO Enterprises (
        EnterpriseName,
        Domain,
        RevenueSharePercent,  -- ✅ Correct column name
        QCRequired,
        Status,
        HasIsrcMasterCode,
        AudioMasterCode,
        VideoMasterCode,
        IsrcCertificateUrl,
        CreatedBy,  -- ✅ Use @CreatedBy parameter (creator)
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        @EnterpriseName,
        @Domain,
        @RevenueShare,  -- Parameter value goes to RevenueSharePercent column
        @QCRequired,
        'active',
        @HasIsrcMasterCode,
        @AudioMasterCode,
        @VideoMasterCode,
        @IsrcCertificateUrl,
        @CreatedBy,  -- ✅ Set CreatedBy from parameter
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
    
    SET @EnterpriseID = SCOPE_IDENTITY();
    
    -- Assign owner role (either from email or creator)
    IF @OwnerUserID IS NOT NULL
    BEGIN
        -- Check if user already has enterprise role
        IF NOT EXISTS (SELECT 1 FROM EnterpriseUserRoles WHERE EnterpriseID = @EnterpriseID AND UserID = @OwnerUserID)
        BEGIN
            INSERT INTO EnterpriseUserRoles (EnterpriseID, UserID, Role, CreatedAt)
            VALUES (@EnterpriseID, @OwnerUserID, 'EnterpriseAdmin', SYSUTCDATETIME());
        END
    END
    
    -- Return results
    SELECT 
        @EnterpriseID AS EnterpriseID,
        @OwnerUserID AS OwnerUserID,
        @OwnerStatus AS OwnerStatus;
END;
GO

PRINT 'sp_CreateEnterprise_AutoOwner created successfully';
GO
