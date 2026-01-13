-- =============================================
-- Check sp_Auth_Login Stored Procedure
-- =============================================
-- This checks how sp_Auth_Login returns PasswordHash

-- Step 1: Get stored procedure definition
PRINT '=== Step 1: Check sp_Auth_Login Definition ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS ProcedureDefinition;

-- Step 2: Test what sp_Auth_Login returns for d@g.com
PRINT '=== Step 2: Test sp_Auth_Login Output ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

EXEC sp_Auth_Login @Email = @TestEmail;

-- Step 3: Compare direct query vs stored procedure
PRINT '=== Step 3: Compare Direct Query vs Stored Procedure ===';
-- Direct query
SELECT 
    'Direct Query' AS Source,
    UserID,
    Email,
    PasswordHash,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,
    LEFT(PasswordHash, 20) AS HashPreview
FROM Users
WHERE Email = @TestEmail;

-- Step 4: Check if stored procedure converts to VARCHAR
PRINT '=== Step 4: Check for VARCHAR Conversion ===';
-- Look for CAST or CONVERT in the procedure
SELECT 
    OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS ProcedureDefinition
WHERE OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) LIKE '%CAST%PasswordHash%'
   OR OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) LIKE '%CONVERT%PasswordHash%'
   OR OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) LIKE '%VARCHAR%PasswordHash%';







