-- =============================================
-- Find Reset Table and Fix Password Reset
-- =============================================

-- Step 1: Find the actual reset table
PRINT '=== Step 1: Find Reset Table ===';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%Reset%' 
   OR TABLE_NAME LIKE '%Token%'
   OR TABLE_NAME LIKE '%Forgot%'
   OR TABLE_NAME LIKE '%Password%'
ORDER BY TABLE_NAME;

-- Step 2: Check stored procedure definition
PRINT '=== Step 2: Check sp_ForgetPassword_ResetPassword ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ResetPassword')) AS ProcedureDefinition;

-- Step 3: Check sp_ForgetPassword_Create to see which table it uses
PRINT '=== Step 3: Check sp_ForgetPassword_Create ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS ProcedureDefinition;

-- Step 4: Check sp_ForgetPassword_ValidateCode to see which table it uses
PRINT '=== Step 4: Check sp_ForgetPassword_ValidateCode ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ValidateCode')) AS ProcedureDefinition;

-- Step 5: Check for recent reset requests
PRINT '=== Step 5: Check Recent Reset Requests ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';
DECLARE @TestKey NVARCHAR(255) = '0466ad18ac5649a2b88de9fbf4f56cc0';

-- Try common table names
IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
BEGIN
    PRINT 'Checking PasswordResets table...';
    SELECT TOP 5 * FROM PasswordResets 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END

IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT 'Checking ResetTokens table...';
    SELECT TOP 5 * FROM ResetTokens 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END

IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT 'Checking UserResetTokens table...';
    SELECT TOP 5 * FROM UserResetTokens 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END

IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
BEGIN
    PRINT 'Checking PasswordResetTokens table...';
    SELECT TOP 5 * FROM PasswordResetTokens 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END

IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
BEGIN
    PRINT 'Checking ForgotPasswordRequests table...';
    SELECT TOP 5 * FROM ForgotPasswordRequests 
    WHERE Email = @TestEmail 
    ORDER BY CreatedAt DESC;
END







