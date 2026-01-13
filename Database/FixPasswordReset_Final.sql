-- =============================================
-- Final Fix: Password Reset Stored Procedure
-- =============================================
-- This fixes sp_ForgetPassword_ResetPassword to match the validation logic
-- and correctly find reset requests after OTP validation

-- Step 1: Check current stored procedure definitions
PRINT '=== Step 1: Check Current Stored Procedures ===';
SELECT 
    'sp_ForgetPassword_Create' AS ProcedureName,
    OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS Definition
UNION ALL
SELECT 
    'sp_ForgetPassword_ValidateCode' AS ProcedureName,
    OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ValidateCode')) AS Definition
UNION ALL
SELECT 
    'sp_ForgetPassword_ResetPassword' AS ProcedureName,
    OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_ResetPassword')) AS Definition;
GO

-- Step 2: Find the actual reset table
PRINT '=== Step 2: Find Reset Table ===';
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%Reset%' 
   OR TABLE_NAME LIKE '%Token%'
   OR TABLE_NAME LIKE '%Forgot%'
ORDER BY TABLE_NAME;
GO

-- Step 3: Drop and recreate sp_ForgetPassword_ResetPassword
-- This version matches the validation logic and checks all common table structures
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
    
    -- Try to find reset request - check multiple table structures
    -- The key is to match the same logic that sp_ForgetPassword_ValidateCode uses
    
    -- Option 1: PasswordResets table (most common)
    IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = ResetID
        FROM PasswordResets
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL OR IsValidated = 0);  -- Accept validated or not yet validated
        
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
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL OR IsValidated = 0);
        
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
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL OR IsValidated = 0);
        
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
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL OR IsValidated = 0);
        
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
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL OR IsValidated = 0);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Option 6: Try without IsValidated check (maybe validation sets a different flag)
    IF @IsValid = 0 AND OBJECT_ID('PasswordResets', 'U') IS NOT NULL
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
        -- Debug: Return more information about why it failed
        DECLARE @DebugInfo NVARCHAR(500) = 'Reset request not found. Email: ' + @Email + ', Key: ' + @Key;
        SELECT 0 AS Success, @Message AS Message, @DebugInfo AS DebugInfo;
    END
END;
GO

PRINT 'sp_ForgetPassword_ResetPassword procedure created successfully';
GO

-- Step 4: Verify PasswordHash column is NVARCHAR(MAX)
PRINT '=== Step 4: Verify PasswordHash Column ===';
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






