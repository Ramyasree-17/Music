-- =============================================
-- Find OTP/Password Reset Table
-- =============================================
-- This script helps identify which table stores OTP codes

-- Method 1: Check stored procedure definition (sp_ForgetPassword_Create)
-- This will show which table is used to store OTP
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS ProcedureDefinition;

-- Method 2: Check stored procedure definitions for table references
SELECT 
    OBJECT_NAME(object_id) AS ProcedureName,
    OBJECT_DEFINITION(object_id) AS ProcedureDefinition
FROM sys.procedures
WHERE name LIKE '%ForgetPassword%' 
   OR name LIKE '%Reset%' 
   OR name LIKE '%OTP%'
ORDER BY name;

-- Method 2: Find tables with OTP/Reset related columns
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name LIKE '%OTP%' 
   OR c.name LIKE '%Reset%' 
   OR c.name LIKE '%Token%'
   OR c.name LIKE '%Code%'
   OR c.name LIKE '%Key%'
ORDER BY t.name, c.name;

-- Method 3: List all tables that might contain OTP data
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%Reset%' 
   OR TABLE_NAME LIKE '%OTP%'
   OR TABLE_NAME LIKE '%Token%'
   OR TABLE_NAME LIKE '%Password%'
   OR TABLE_NAME LIKE '%Code%'
ORDER BY TABLE_NAME;

-- Method 4: Check for common table names directly
IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
    SELECT 'PasswordResets table exists' AS Result;
ELSE
    SELECT 'PasswordResets table does NOT exist' AS Result;

IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
    SELECT 'ResetTokens table exists' AS Result;
ELSE
    SELECT 'ResetTokens table does NOT exist' AS Result;

IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
    SELECT 'UserResetTokens table exists' AS Result;
ELSE
    SELECT 'UserResetTokens table does NOT exist' AS Result;

IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
    SELECT 'PasswordResetTokens table exists' AS Result;
ELSE
    SELECT 'PasswordResetTokens table does NOT exist' AS Result;
