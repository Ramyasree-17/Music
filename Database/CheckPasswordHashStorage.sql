-- =============================================
-- Check Password Hash Storage Issue
-- =============================================
-- This script checks why password reset doesn't work for login

-- Step 1: Check stored procedure definition
PRINT '=== Step 1: Check sp_ForgetPassword_ResetPassword ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ResetPassword')) AS ProcedureDefinition;

-- Step 2: Check Users table PasswordHash column type
PRINT '=== Step 2: Check PasswordHash Column Type ===';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    c.collation_name AS Collation
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Users')
  AND c.name = 'PasswordHash';

-- Step 3: Check the actual password hash for test user
PRINT '=== Step 3: Check User Password Hash After Reset ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

SELECT 
    UserID,
    Email,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,  -- Actual bytes used
    LEFT(PasswordHash, 50) AS HashPreview,
    -- Check if it's Unicode
    CASE 
        WHEN PasswordHash = CAST(PasswordHash AS VARCHAR(MAX)) THEN 'VARCHAR (Unicode lost!)'
        ELSE 'NVARCHAR (Unicode preserved)'
    END AS StorageType,
    -- Check format
    CASE 
        WHEN PasswordHash LIKE '$2a$%' OR PasswordHash LIKE '$2b$%' THEN 'BCrypt'
        WHEN LEN(PasswordHash) = 64 AND PasswordHash LIKE '[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]%' THEN 'SHA256 Hex'
        WHEN PasswordHash LIKE N'%[^ -~]%' THEN 'PBKDF2 Unicode (contains non-ASCII)'
        ELSE 'Unknown Format'
    END AS DetectedFormat,
    UpdatedAt
FROM Users
WHERE Email = @TestEmail;

-- Step 4: Compare with a working user
PRINT '=== Step 4: Compare with Working User ===';
SELECT TOP 3
    UserID,
    Email,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,
    LEFT(PasswordHash, 20) AS HashPreview,
    CASE 
        WHEN PasswordHash LIKE '$2a$%' OR PasswordHash LIKE '$2b$%' THEN 'BCrypt'
        WHEN LEN(PasswordHash) = 64 AND PasswordHash LIKE '[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]%' THEN 'SHA256 Hex'
        WHEN PasswordHash LIKE N'%[^ -~]%' THEN 'PBKDF2 Unicode'
        ELSE 'Unknown'
    END AS Format
FROM Users
WHERE PasswordHash IS NOT NULL
  AND Email != @TestEmail
ORDER BY UpdatedAt DESC;







