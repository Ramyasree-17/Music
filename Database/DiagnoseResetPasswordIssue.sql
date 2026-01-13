-- =============================================
-- Diagnose Password Reset Issue
-- =============================================
-- Run this to see why password reset is failing

DECLARE @TestEmail NVARCHAR(255) = 'admin@tunewav.in';
DECLARE @TestKey NVARCHAR(255) = '122bad0f7dd29cea017842a266ab8e4d';

PRINT '=== Step 1: Check Stored Procedure Definitions ===';
SELECT 
    'sp_ForgetPassword_Create' AS ProcedureName,
    OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS Definition;
GO

SELECT 
    'sp_ForgetPassword_ValidateCode' AS ProcedureName,
    OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ValidateCode')) AS Definition;
GO

SELECT 
    'sp_ForgetPassword_ResetPassword' AS ProcedureName,
    OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ResetPassword')) AS Definition;
GO

PRINT '=== Step 2: Find Reset Tables ===';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%Reset%' 
   OR TABLE_NAME LIKE '%Token%'
   OR TABLE_NAME LIKE '%Forgot%'
ORDER BY TABLE_NAME;
GO

PRINT '=== Step 3: Check Recent Reset Request ===';
DECLARE @TestEmail NVARCHAR(255) = 'admin@tunewav.in';
DECLARE @TestKey NVARCHAR(255) = '122bad0f7dd29cea017842a266ab8e4d';

-- Check PasswordResets table
IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
BEGIN
    PRINT '=== PasswordResets Table ===';
    SELECT TOP 5 
        *,
        CASE 
            WHEN IsUsed = 1 THEN 'USED'
            WHEN IsUsed = 0 OR IsUsed IS NULL THEN 'NOT USED'
            ELSE 'UNKNOWN'
        END AS UsageStatus,
        CASE 
            WHEN ExpiresAt IS NULL THEN 'NO EXPIRY'
            WHEN ExpiresAt > SYSUTCDATETIME() THEN 'VALID'
            ELSE 'EXPIRED'
        END AS ExpiryStatus,
        CASE 
            WHEN IsValidated = 1 THEN 'VALIDATED'
            WHEN IsValidated = 0 OR IsValidated IS NULL THEN 'NOT VALIDATED'
            ELSE 'UNKNOWN'
        END AS ValidationStatus
    FROM PasswordResets 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
    
    -- Check if the specific key exists
    SELECT 
        'Key Check' AS CheckType,
        COUNT(*) AS FoundCount,
        MAX(CASE WHEN ResetKey = @TestKey THEN 1 ELSE 0 END) AS KeyMatches,
        MAX(CASE WHEN IsUsed = 0 OR IsUsed IS NULL THEN 1 ELSE 0 END) AS NotUsed,
        MAX(CASE WHEN ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL THEN 1 ELSE 0 END) AS NotExpired,
        MAX(CASE WHEN IsValidated = 1 OR IsValidated IS NULL THEN 1 ELSE 0 END) AS ValidatedOrNull
    FROM PasswordResets
    WHERE Email = @TestEmail AND ResetKey = @TestKey;
END
ELSE
    PRINT 'PasswordResets table does NOT exist';

-- Check ResetTokens table
IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT '=== ResetTokens Table ===';
    SELECT TOP 5 
        *,
        CASE 
            WHEN IsUsed = 1 THEN 'USED'
            WHEN IsUsed = 0 OR IsUsed IS NULL THEN 'NOT USED'
            ELSE 'UNKNOWN'
        END AS UsageStatus,
        CASE 
            WHEN ExpiresAt IS NULL THEN 'NO EXPIRY'
            WHEN ExpiresAt > SYSUTCDATETIME() THEN 'VALID'
            ELSE 'EXPIRED'
        END AS ExpiryStatus,
        CASE 
            WHEN IsValidated = 1 THEN 'VALIDATED'
            WHEN IsValidated = 0 OR IsValidated IS NULL THEN 'NOT VALIDATED'
            ELSE 'UNKNOWN'
        END AS ValidationStatus
    FROM ResetTokens 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END
ELSE
    PRINT 'ResetTokens table does NOT exist';

-- Check UserResetTokens table
IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT '=== UserResetTokens Table ===';
    SELECT TOP 5 * FROM UserResetTokens 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END
ELSE
    PRINT 'UserResetTokens table does NOT exist';

-- Check PasswordResetTokens table
IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT '=== PasswordResetTokens Table ===';
    SELECT TOP 5 * FROM PasswordResetTokens 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END
ELSE
    PRINT 'PasswordResetTokens table does NOT exist';

-- Check ForgotPasswordRequests table
IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
BEGIN
    PRINT '=== ForgotPasswordRequests Table ===';
    SELECT TOP 5 * FROM ForgotPasswordRequests 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END
ELSE
    PRINT 'ForgotPasswordRequests table does NOT exist';

PRINT '=== Step 4: Check Table Column Names ===';
-- Find all columns in reset-related tables
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE (t.name LIKE '%Reset%' OR t.name LIKE '%Token%' OR t.name LIKE '%Forgot%')
  AND (c.name LIKE '%Valid%' OR c.name LIKE '%Use%' OR c.name LIKE '%Expire%' OR c.name LIKE '%Key%')
ORDER BY t.name, c.name;
GO






