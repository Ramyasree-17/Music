-- =============================================
-- Check OTP Table and Validation Logic
-- =============================================

-- Step 1: Find the stored procedure definition
PRINT '=== Checking sp_ForgetPassword_ValidateCode ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ValidateCode')) AS ProcedureDefinition;

-- Step 2: Check sp_ForgetPassword_Create to see which table it uses
PRINT '=== Checking sp_ForgetPassword_Create ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS ProcedureDefinition;

-- Step 3: Find tables with OTP/Reset related columns
PRINT '=== Finding OTP/Reset Tables ===';
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name LIKE '%OTP%' 
   OR c.name LIKE '%Reset%' 
   OR c.name LIKE '%Token%'
   OR c.name LIKE '%Code%'
   OR c.name LIKE '%Key%'
ORDER BY t.name, c.name;

-- Step 4: List all tables that might contain OTP data
PRINT '=== Possible OTP Tables ===';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%Reset%' 
   OR TABLE_NAME LIKE '%OTP%'
   OR TABLE_NAME LIKE '%Token%'
   OR TABLE_NAME LIKE '%Password%'
   OR TABLE_NAME LIKE '%Code%'
ORDER BY TABLE_NAME;

-- Step 5: Check common table names directly
PRINT '=== Checking Common Table Names ===';
IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
BEGIN
    DECLARE @Count1 INT;
    SELECT @Count1 = COUNT(*) FROM PasswordResets;
    SELECT 'PasswordResets table EXISTS' AS Result, @Count1 AS RowCount;
END
ELSE
    SELECT 'PasswordResets table does NOT exist' AS Result;

IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
BEGIN
    DECLARE @Count2 INT;
    SELECT @Count2 = COUNT(*) FROM ResetTokens;
    SELECT 'ResetTokens table EXISTS' AS Result, @Count2 AS RowCount;
END
ELSE
    SELECT 'ResetTokens table does NOT exist' AS Result;

IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
BEGIN
    DECLARE @Count3 INT;
    SELECT @Count3 = COUNT(*) FROM UserResetTokens;
    SELECT 'UserResetTokens table EXISTS' AS Result, @Count3 AS RowCount;
END
ELSE
    SELECT 'UserResetTokens table does NOT exist' AS Result;

IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
BEGIN
    DECLARE @Count4 INT;
    SELECT @Count4 = COUNT(*) FROM PasswordResetTokens;
    SELECT 'PasswordResetTokens table EXISTS' AS Result, @Count4 AS RowCount;
END
ELSE
    SELECT 'PasswordResetTokens table does NOT exist' AS Result;

