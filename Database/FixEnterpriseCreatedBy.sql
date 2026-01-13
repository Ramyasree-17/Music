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
    PRINT 'Created CreatedBy column in Enterprises table';
END
GO

-- =============================================
-- Step 2: Fix: Update sp_CreateEnterprise_AutoOwner to use @CreatedBy parameter
-- =============================================
-- This script updates the stored procedure to properly set CreatedBy when creating enterprises

IF OBJECT_ID('[dbo].[sp_CreateEnterprise_AutoOwner]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_CreateEnterprise_AutoOwner];
GO

CREATE PROCEDURE [dbo].[sp_CreateEnterprise_AutoOwner]
    @EnterpriseName NVARCHAR(255),
    @Domain NVARCHAR(255) = NULL,
    @RevenueShare DECIMAL(5,2),
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
    
    -- If owner email doesn't exist, use creator as owner
    IF @OwnerUserID IS NULL AND @CreatedBy IS NOT NULL
    BEGIN
        SET @OwnerUserID = @CreatedBy;
        SET @OwnerStatus = 'CREATOR_AS_OWNER';
    END
    ELSE IF @OwnerUserID IS NOT NULL
    BEGIN
        SET @OwnerStatus = 'EXISTING';
    END
    ELSE
    BEGIN
        SET @OwnerStatus = 'NOT_FOUND';
    END
    
    -- Insert Enterprise with CreatedBy (only if column exists)
    -- Check if CreatedBy column exists
    DECLARE @HasCreatedByColumn BIT = 0;
    IF EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID('Enterprises') 
        AND name = 'CreatedBy'
    )
    BEGIN
        SET @HasCreatedByColumn = 1;
    END
    
    IF @HasCreatedByColumn = 1
    BEGIN
        INSERT INTO Enterprises (
            EnterpriseName,
            Domain,
            RevenueShare,
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
            @RevenueShare,
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
    END
    ELSE
    BEGIN
        -- Fallback: Insert without CreatedBy if column doesn't exist
        INSERT INTO Enterprises (
            EnterpriseName,
            Domain,
            RevenueShare,
            QCRequired,
            Status,
            HasIsrcMasterCode,
            AudioMasterCode,
            VideoMasterCode,
            IsrcCertificateUrl,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @EnterpriseName,
            @Domain,
            @RevenueShare,
            @QCRequired,
            'active',
            @HasIsrcMasterCode,
            @AudioMasterCode,
            @VideoMasterCode,
            @IsrcCertificateUrl,
            SYSUTCDATETIME(),
            SYSUTCDATETIME()
        );
    END
    
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

-- =============================================
-- Optional: Update existing NULL CreatedBy values
-- =============================================
-- Uncomment below to update existing enterprises with NULL CreatedBy
-- You may want to set them to a default user or leave as NULL

/*
UPDATE Enterprises 
SET CreatedBy = 1  -- Set to a default SuperAdmin user ID
WHERE CreatedBy IS NULL;
*/

