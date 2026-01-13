-- =============================================
-- Diagnose sp_Auth_Login Issue
-- =============================================

DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

-- Step 1: Check if procedure exists
PRINT '=== Step 1: Check if sp_Auth_Login exists ===';
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
BEGIN
    PRINT 'sp_Auth_Login EXISTS';
    
    -- Get definition
    SELECT OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS ProcedureDefinition;
END
ELSE
BEGIN
    PRINT 'sp_Auth_Login DOES NOT EXIST - Need to create it';
END
GO

-- Step 2: Test what sp_Auth_Login returns
PRINT '=== Step 2: Test sp_Auth_Login Output ===';
DECLARE @TestEmail2 NVARCHAR(255) = 'd@g.com';

IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
BEGIN
    EXEC sp_Auth_Login @Email = @TestEmail2;
END
ELSE
BEGIN
    PRINT 'Cannot test - procedure does not exist';
END
GO

-- Step 3: Compare with direct query
PRINT '=== Step 3: Direct Query (What should be returned) ===';
DECLARE @TestEmail3 NVARCHAR(255) = 'd@g.com';

SELECT 
    UserID,
    FullName,
    Email,
    Role,
    PasswordHash,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,
    LEFT(PasswordHash, 30) AS HashPreview,
    Status,
    IsActive,
    CreatedAt,
    UpdatedAt
FROM Users
WHERE Email = @TestEmail3;
GO







