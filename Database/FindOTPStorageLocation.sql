-- =============================================
-- Find Where OTPs are Stored in Database
-- =============================================
-- This script finds the exact table and columns where OTPs are saved

-- Method 1: Check stored procedure sp_ForgetPassword_Create to see which table it inserts into
PRINT '=== Step 1: Check sp_ForgetPassword_Create Procedure ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS ProcedureDefinition;

-- Method 2: Find all tables with OTP/Reset/Token/Code/Key columns
PRINT '=== Step 2: Find Tables with OTP Related Columns ===';
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
   OR c.name LIKE '%Hash%'
ORDER BY t.name, c.name;

-- Method 3: List all possible table names
PRINT '=== Step 3: List All Possible OTP Tables ===';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%Reset%' 
   OR TABLE_NAME LIKE '%OTP%'
   OR TABLE_NAME LIKE '%Token%'
   OR TABLE_NAME LIKE '%Password%'
   OR TABLE_NAME LIKE '%Code%'
   OR TABLE_NAME LIKE '%Forgot%'
ORDER BY TABLE_NAME;

-- Method 4: Check specific common table names
PRINT '=== Step 4: Check Specific Tables ===';

-- Check PasswordResets
IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
BEGIN
    PRINT 'PasswordResets table EXISTS';
    SELECT TOP 5 * FROM PasswordResets ORDER BY CreatedAt DESC;
    SELECT 'Table: PasswordResets' AS TableName, COUNT(*) AS TotalRows FROM PasswordResets;
END
ELSE
    PRINT 'PasswordResets table does NOT exist';

-- Check ResetTokens
IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT 'ResetTokens table EXISTS';
    SELECT TOP 5 * FROM ResetTokens ORDER BY CreatedAt DESC;
    SELECT 'Table: ResetTokens' AS TableName, COUNT(*) AS TotalRows FROM ResetTokens;
END
ELSE
    PRINT 'ResetTokens table does NOT exist';

-- Check UserResetTokens
IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT 'UserResetTokens table EXISTS';
    SELECT TOP 5 * FROM UserResetTokens ORDER BY CreatedAt DESC;
    SELECT 'Table: UserResetTokens' AS TableName, COUNT(*) AS TotalRows FROM UserResetTokens;
END
ELSE
    PRINT 'UserResetTokens table does NOT exist';

-- Check PasswordResetTokens
IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT 'PasswordResetTokens table EXISTS';
    SELECT TOP 5 * FROM PasswordResetTokens ORDER BY CreatedAt DESC;
    SELECT 'Table: PasswordResetTokens' AS TableName, COUNT(*) AS TotalRows FROM PasswordResetTokens;
END
ELSE
    PRINT 'PasswordResetTokens table does NOT exist';

-- Check ForgotPasswordRequests
IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
BEGIN
    PRINT 'ForgotPasswordRequests table EXISTS';
    SELECT TOP 5 * FROM ForgotPasswordRequests ORDER BY CreatedAt DESC;
    SELECT 'Table: ForgotPasswordRequests' AS TableName, COUNT(*) AS TotalRows FROM ForgotPasswordRequests;
END
ELSE
    PRINT 'ForgotPasswordRequests table does NOT exist';

-- Method 5: Search in all tables for columns that might store OTP hash
PRINT '=== Step 5: Find All Tables with Email and Hash/Code Columns ===';
SELECT 
    t.name AS TableName,
    STRING_AGG(c.name, ', ') AS Columns
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
WHERE c.name IN ('Email', 'OTPHash', 'CodeHash', 'ResetKey', 'Key', 'Token', 'OTP', 'Code')
GROUP BY t.name
HAVING COUNT(*) >= 2; -- Tables that have at least 2 of these columns







