-- =============================================
-- COMPLETE FIX: sp_Auth_Login PasswordHash Unicode Issue
-- =============================================
-- Issue: sp_Auth_Login is not returning PasswordHash correctly
-- Fix: Ensure PasswordHash is returned as NVARCHAR(MAX) with proper Unicode preservation

-- Step 1: Check current procedure
PRINT '=== Step 1: Current Procedure Definition ===';
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
BEGIN
    SELECT OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS CurrentDefinition;
END
ELSE
BEGIN
    PRINT 'sp_Auth_Login does not exist';
END
GO

-- Step 2: Drop and recreate with proper Unicode handling
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
    DROP PROCEDURE sp_Auth_Login;
GO

CREATE PROCEDURE [dbo].[sp_Auth_Login]
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- CRITICAL FIX: Return PasswordHash as NVARCHAR(MAX) to preserve Unicode
    -- The column might be NVARCHAR, but we need to explicitly cast to ensure
    -- SQL Server doesn't convert it to VARCHAR when returning
    SELECT 
        UserID,
        ISNULL(FullName, '') AS FullName,
        Email,
        ISNULL(Role, 'User') AS Role,
        -- Explicitly cast to NVARCHAR(MAX) to preserve Unicode characters
        -- This is critical for PBKDF2 Unicode hashes
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
GO

-- Step 3: Test the procedure
PRINT '=== Step 3: Testing sp_Auth_Login ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

EXEC sp_Auth_Login @Email = @TestEmail;
GO

-- Step 4: Verify the output preserves Unicode
PRINT '=== Step 4: Verify Unicode Preservation ===';
DECLARE @TestEmail2 NVARCHAR(255) = 'd@g.com';
DECLARE @HashFromSP NVARCHAR(MAX);
DECLARE @HashFromDirect NVARCHAR(MAX);

-- Get hash from stored procedure
SELECT TOP 1 @HashFromSP = PasswordHash
FROM OPENROWSET('SQLNCLI', 'Server=localhost;Trusted_Connection=yes;', 
    'EXEC sp_Auth_Login @Email = ''' + @TestEmail2 + '''');

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
        WHEN @HashFromSP = @HashFromDirect THEN 'MATCH - Unicode preserved!'
        ELSE 'MISMATCH - Unicode corrupted!'
    END AS Comparison;







