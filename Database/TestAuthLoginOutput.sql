-- =============================================
-- Test sp_Auth_Login Output
-- =============================================
-- This tests what sp_Auth_Login actually returns

DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

-- Step 1: Check what sp_Auth_Login returns
PRINT '=== Step 1: sp_Auth_Login Output ===';
EXEC sp_Auth_Login @Email = @TestEmail;

-- Step 2: Compare with direct query
PRINT '=== Step 2: Direct Query Output ===';
SELECT 
    UserID,
    FullName,
    Email,
    Role,
    PasswordHash,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,
    LEFT(PasswordHash, 20) AS HashPreview
FROM Users
WHERE Email = @TestEmail;

-- Step 3: Check stored procedure definition
PRINT '=== Step 3: sp_Auth_Login Definition ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS ProcedureDefinition;







