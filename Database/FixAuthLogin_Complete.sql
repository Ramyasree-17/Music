-- =============================================
-- COMPLETE FIX: sp_Auth_Login PasswordHash Issue
-- =============================================
-- This fixes sp_Auth_Login to return PasswordHash correctly with Unicode preservation

-- Step 1: Drop existing procedure
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
BEGIN
    PRINT 'Dropping existing sp_Auth_Login...';
    DROP PROCEDURE sp_Auth_Login;
END
GO

-- Step 2: Create fixed procedure
CREATE PROCEDURE [dbo].[sp_Auth_Login]
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Return all user fields including PasswordHash as NVARCHAR(MAX)
    -- This is critical to preserve Unicode PBKDF2 hash characters
    SELECT 
        UserID,
        ISNULL(FullName, '') AS FullName,
        Email,
        ISNULL(Role, 'User') AS Role,
        -- CRITICAL: Cast to NVARCHAR(MAX) to preserve Unicode
        -- Without this, Unicode characters get corrupted
        CAST(ISNULL(PasswordHash, '') AS NVARCHAR(MAX)) AS PasswordHash,
        ISNULL(Status, 'Active') AS Status,
        ISNULL(IsActive, 1) AS IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users
    WHERE Email = @Email
      AND (IsActive = 1 OR IsActive IS NULL);
END;
GO

PRINT 'sp_Auth_Login procedure created successfully';
PRINT 'PasswordHash will now be returned as NVARCHAR(MAX) to preserve Unicode';
GO

-- Step 3: Test the procedure
PRINT '=== Testing sp_Auth_Login ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

EXEC sp_Auth_Login @Email = @TestEmail;
GO

-- Step 4: Verify Unicode preservation
PRINT '=== Verifying Unicode Preservation ===';
DECLARE @TestEmail2 NVARCHAR(255) = 'd@g.com';

-- Get hash from stored procedure
DECLARE @HashFromSP NVARCHAR(MAX);
DECLARE @HashFromDirect NVARCHAR(MAX);

-- Create temp table to capture SP output
CREATE TABLE #TempSPOutput (
    UserID INT,
    FullName NVARCHAR(255),
    Email NVARCHAR(255),
    Role NVARCHAR(50),
    PasswordHash NVARCHAR(MAX),
    Status NVARCHAR(50),
    IsActive BIT,
    CreatedAt DATETIME2,
    UpdatedAt DATETIME2
);

INSERT INTO #TempSPOutput
EXEC sp_Auth_Login @Email = @TestEmail2;

SELECT @HashFromSP = PasswordHash FROM #TempSPOutput;

-- Get hash from direct query
SELECT @HashFromDirect = PasswordHash
FROM Users
WHERE Email = @TestEmail2;

-- Compare
SELECT 
    LEN(@HashFromSP) AS SP_HashLength,
    DATALENGTH(@HashFromSP) AS SP_HashBytes,
    LEN(@HashFromDirect) AS Direct_HashLength,
    DATALENGTH(@HashFromDirect) AS Direct_HashBytes,
    CASE 
        WHEN @HashFromSP = @HashFromDirect THEN '✅ MATCH - Unicode preserved!'
        WHEN LEN(@HashFromSP) = LEN(@HashFromDirect) THEN '⚠️ Same length but different - Unicode may be corrupted'
        ELSE '❌ MISMATCH - Lengths differ'
    END AS Comparison,
    LEFT(@HashFromSP, 20) AS SP_HashPreview,
    LEFT(@HashFromDirect, 20) AS Direct_HashPreview;

DROP TABLE #TempSPOutput;
GO







