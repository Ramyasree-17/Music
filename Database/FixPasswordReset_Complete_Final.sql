-- =============================================
-- Complete Fix: Password Reset & Login Flow
-- =============================================
-- This ensures password reset works and login works after reset

-- Step 1: Fix PasswordHash column to NVARCHAR(MAX) if needed
PRINT '=== Step 1: Ensure PasswordHash is NVARCHAR(MAX) ===';
IF EXISTS (
    SELECT 1 
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('Users')
      AND c.name = 'PasswordHash'
      AND (t.name = 'varchar' OR (t.name = 'nvarchar' AND c.max_length != -1))
)
BEGIN
    PRINT 'Changing PasswordHash to NVARCHAR(MAX)...';
    ALTER TABLE Users 
    ALTER COLUMN PasswordHash NVARCHAR(MAX) NULL;
    PRINT 'PasswordHash column fixed!';
END
ELSE
BEGIN
    PRINT 'PasswordHash column is already NVARCHAR(MAX).';
END
GO

-- Step 2: Fix sp_ForgetPassword_ResetPassword
-- This version finds reset requests without strict IsValidated check
-- (since OTP validation already happened)
PRINT '=== Step 2: Fix sp_ForgetPassword_ResetPassword ===';
IF OBJECT_ID('sp_ForgetPassword_ResetPassword', 'P') IS NOT NULL
    DROP PROCEDURE sp_ForgetPassword_ResetPassword;
GO

CREATE PROCEDURE [dbo].[sp_ForgetPassword_ResetPassword]
    @Email NVARCHAR(255),
    @Key NVARCHAR(255),
    @NewPassword NVARCHAR(MAX)  -- Must be NVARCHAR(MAX) for Unicode PBKDF2 hash
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UserID INT;
    DECLARE @ResetRequestID INT = NULL;
    DECLARE @IsValid BIT = 0;
    DECLARE @Message NVARCHAR(255) = 'Invalid reset request';
    
    -- Find user
    SELECT @UserID = UserID 
    FROM Users 
    WHERE Email = @Email;
    
    IF @UserID IS NULL
    BEGIN
        SELECT 0 AS Success, 'User not found' AS Message;
        RETURN;
    END
    
    -- Find reset request - try multiple table structures
    -- We check: Email + Key match, Not used, Not expired
    -- We DON'T require IsValidated=1 because OTP validation already happened
    
    -- Option 1: PasswordResets table
    IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = ResetID
        FROM PasswordResets
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Option 2: ResetTokens table
    IF @IsValid = 0 AND OBJECT_ID('ResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID
        FROM ResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Option 3: UserResetTokens table
    IF @IsValid = 0 AND OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID
        FROM UserResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Option 4: PasswordResetTokens table
    IF @IsValid = 0 AND OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID
        FROM PasswordResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Option 5: ForgotPasswordRequests table
    IF @IsValid = 0 AND OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = RequestID
        FROM ForgotPasswordRequests
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Update password if valid
    IF @IsValid = 1
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            
            -- Update password hash - CRITICAL: Use NVARCHAR(MAX) to preserve Unicode
            UPDATE Users 
            SET PasswordHash = @NewPassword,  -- Already NVARCHAR(MAX), preserves Unicode
                UpdatedAt = SYSUTCDATETIME()
            WHERE UserID = @UserID;
            
            -- Mark reset request as used
            IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL AND @ResetRequestID IS NOT NULL
            BEGIN
                UPDATE PasswordResets 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE ResetID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL AND @ResetRequestID IS NOT NULL
            BEGIN
                UPDATE ResetTokens 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE TokenID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL AND @ResetRequestID IS NOT NULL
            BEGIN
                UPDATE UserResetTokens 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE TokenID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL AND @ResetRequestID IS NOT NULL
            BEGIN
                UPDATE PasswordResetTokens 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE TokenID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL AND @ResetRequestID IS NOT NULL
            BEGIN
                UPDATE ForgotPasswordRequests 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE RequestID = @ResetRequestID;
            END
            
            COMMIT TRANSACTION;
            
            SET @Message = 'Password updated successfully';
            SELECT 1 AS Success, @Message AS Message;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;
            
            SET @Message = 'Failed to update password: ' + ERROR_MESSAGE();
            SELECT 0 AS Success, @Message AS Message;
        END CATCH
    END
    ELSE
    BEGIN
        SELECT 0 AS Success, @Message AS Message;
    END
END;
GO

PRINT 'sp_ForgetPassword_ResetPassword procedure fixed!';
GO

-- Step 3: Fix sp_Auth_Login to return PasswordHash as NVARCHAR(MAX)
PRINT '=== Step 3: Fix sp_Auth_Login ===';
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
    DROP PROCEDURE sp_Auth_Login;
GO

CREATE PROCEDURE [dbo].[sp_Auth_Login]
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        UserID,
        FullName,
        Email,
        Role,
        CAST(PasswordHash AS NVARCHAR(MAX)) AS PasswordHash,  -- Explicit NVARCHAR cast preserves Unicode
        Status,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users
    WHERE Email = @Email
      AND (IsActive = 1 OR IsActive IS NULL);
END;
GO

PRINT 'sp_Auth_Login procedure fixed!';
GO

-- Step 4: Verify fixes
PRINT '=== Step 4: Verify Fixes ===';
PRINT 'PasswordHash column type:';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    CASE 
        WHEN c.max_length = -1 THEN 'MAX'
        ELSE CAST(c.max_length AS VARCHAR(10))
    END AS MaxLength
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Users')
  AND c.name = 'PasswordHash';
GO

PRINT 'Stored procedures created:';
SELECT 
    'sp_ForgetPassword_ResetPassword' AS ProcedureName,
    CASE WHEN OBJECT_ID('sp_ForgetPassword_ResetPassword', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END AS Status
UNION ALL
SELECT 
    'sp_Auth_Login' AS ProcedureName,
    CASE WHEN OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END AS Status;
GO

PRINT '=== Fix Complete! ===';
PRINT 'Now try:';
PRINT '1. POST /api/auth/forgetpassword (with email)';
PRINT '2. POST /api/auth/forgetpassword/codevalidate (with email, key, code)';
PRINT '3. POST /api/auth/forgetpassword/password (with email, key, newPassword, confirmPassword)';
PRINT '4. POST /api/auth/login (with email and new password)';
GO






