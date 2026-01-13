-- =============================================
-- Diagnose Password Reset Login Issue
-- =============================================
-- This script helps identify why login fails after password reset

-- Step 1: Check stored procedure definition
PRINT '=== Step 1: Check sp_ForgetPassword_ResetPassword Procedure ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ResetPassword')) AS ProcedureDefinition;

-- Step 2: Check Users table structure for PasswordHash column
PRINT '=== Step 2: Check Users Table PasswordHash Column ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users' 
  AND COLUMN_NAME = 'PasswordHash';

-- Step 3: Check a user's password hash after reset (replace email with test email)
PRINT '=== Step 3: Check User Password Hash Format ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

SELECT 
    UserID,
    Email,
    FullName,
    LEN(PasswordHash) AS HashLength,
    -- Check first few characters to see format
    LEFT(PasswordHash, 20) AS HashPreview,
    -- Check if it contains Unicode characters
    CASE 
        WHEN PasswordHash LIKE N'%[^ -~]%' THEN 'Contains Unicode'
        ELSE 'ASCII Only'
    END AS HashType,
    -- Check format type
    CASE 
        WHEN PasswordHash LIKE '$2a$%' OR PasswordHash LIKE '$2b$%' THEN 'BCrypt'
        WHEN LEN(PasswordHash) = 64 AND PasswordHash LIKE '[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]%' THEN 'SHA256 Hex'
        ELSE 'PBKDF2 Unicode'
    END AS DetectedFormat,
    CreatedAt,
    UpdatedAt
FROM Users
WHERE Email = @TestEmail;

-- Step 4: Check if there are any recent password resets
PRINT '=== Step 4: Check Recent Password Reset Activity ===';
-- This will help identify if the reset actually happened
-- (You may need to check the actual reset table if it exists)

-- Step 5: Compare with a working user's password hash
PRINT '=== Step 5: Compare with Working User Hash Format ===';
SELECT TOP 5
    UserID,
    Email,
    LEN(PasswordHash) AS HashLength,
    LEFT(PasswordHash, 20) AS HashPreview,
    CASE 
        WHEN PasswordHash LIKE N'%[^ -~]%' THEN 'Contains Unicode'
        ELSE 'ASCII Only'
    END AS HashType,
    CASE 
        WHEN PasswordHash LIKE '$2a$%' OR PasswordHash LIKE '$2b$%' THEN 'BCrypt'
        WHEN LEN(PasswordHash) = 64 AND PasswordHash LIKE '[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]%' THEN 'SHA256 Hex'
        ELSE 'PBKDF2 Unicode'
    END AS DetectedFormat
FROM Users
WHERE PasswordHash IS NOT NULL
ORDER BY UpdatedAt DESC;

-- Step 6: Check if PasswordHash column can store Unicode properly
PRINT '=== Step 6: Check Column Collation ===';
SELECT 
    c.name AS ColumnName,
    c.collation_name AS Collation,
    t.name AS DataType,
    c.max_length AS MaxLength
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Users')
  AND c.name = 'PasswordHash';







