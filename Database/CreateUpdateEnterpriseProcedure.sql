-- =============================================
-- Create sp_UpdateEnterprise stored procedure
-- =============================================

IF OBJECT_ID('[dbo].[sp_UpdateEnterprise]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_UpdateEnterprise];
GO

CREATE PROCEDURE [dbo].[sp_UpdateEnterprise]
    @EnterpriseId INT,
    @Domain NVARCHAR(255) = NULL,
    @RevenueShare DECIMAL(5,2) = NULL,
    @QCRequired BIT = NULL,
    @HasIsrcMasterCode BIT = NULL,
    @AudioMasterCode NVARCHAR(50) = NULL,
    @VideoMasterCode NVARCHAR(50) = NULL,
    @IsrcCertificateUrl NVARCHAR(1000) = NULL,
    @UpdatedBy INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if enterprise exists
    IF NOT EXISTS (SELECT 1 FROM Enterprises WHERE EnterpriseID = @EnterpriseId)
    BEGIN
        RAISERROR('Enterprise not found', 16, 1);
        RETURN;
    END
    
    -- Build dynamic UPDATE statement (only update non-NULL parameters)
    DECLARE @Sql NVARCHAR(MAX) = 'UPDATE Enterprises SET UpdatedAt = SYSUTCDATETIME()';
    
    IF @Domain IS NOT NULL
        SET @Sql += ', Domain = @Domain';
    
    IF @RevenueShare IS NOT NULL
        SET @Sql += ', RevenueSharePercent = @RevenueShare';  -- ✅ Correct column name
    
    IF @QCRequired IS NOT NULL
        SET @Sql += ', QCRequired = @QCRequired';
    
    IF @HasIsrcMasterCode IS NOT NULL
        SET @Sql += ', HasIsrcMasterCode = @HasIsrcMasterCode';
    
    IF @AudioMasterCode IS NOT NULL
        SET @Sql += ', AudioMasterCode = @AudioMasterCode';
    
    IF @VideoMasterCode IS NOT NULL
        SET @Sql += ', VideoMasterCode = @VideoMasterCode';
    
    IF @IsrcCertificateUrl IS NOT NULL
        SET @Sql += ', IsrcCertificateUrl = @IsrcCertificateUrl';
    
    SET @Sql += ' WHERE EnterpriseID = @EnterpriseId';
    
    -- Execute dynamic SQL
    EXEC sp_executesql @Sql,
        N'@EnterpriseId INT, @Domain NVARCHAR(255), @RevenueShare DECIMAL(5,2), @QCRequired BIT, 
          @HasIsrcMasterCode BIT, @AudioMasterCode NVARCHAR(50), @VideoMasterCode NVARCHAR(50), 
          @IsrcCertificateUrl NVARCHAR(1000)',
        @EnterpriseId, @Domain, @RevenueShare, @QCRequired, 
        @HasIsrcMasterCode, @AudioMasterCode, @VideoMasterCode, @IsrcCertificateUrl;
    
    -- Return updated enterprise info
    SELECT 
        EnterpriseID,
        EnterpriseName,
        Domain,
        RevenueSharePercent,  -- ✅ Correct column name
        QCRequired,
        Status,
        HasIsrcMasterCode,
        AudioMasterCode,
        VideoMasterCode,
        IsrcCertificateUrl,
        UpdatedAt
    FROM Enterprises
    WHERE EnterpriseID = @EnterpriseId;
END;
GO

