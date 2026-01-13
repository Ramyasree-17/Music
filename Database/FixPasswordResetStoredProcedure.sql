-- =============================================
-- Fix Password Reset Stored Procedure
-- =============================================
-- This script fixes the sp_ForgetPassword_ResetPassword to ensure
-- password hash is stored correctly in Unicode format

-- Step 1: Check current procedure
IF OBJECT_ID('sp_ForgetPassword_ResetPassword', 'P') IS NOT NULL
BEGIN
    PRINT 'Current procedure exists. Dropping...';
    DROP PROCEDURE sp_ForgetPassword_ResetPassword;
END
GO

-- Step 2: Create fixed procedure
-- This ensures the password hash is stored as NVARCHAR to support Unicode characters
CREATE PROCEDURE [dbo].[sp_ForgetPassword_ResetPassword]
    @Email NVARCHAR(255),
    @Key NVARCHAR(255),
    @NewPassword NVARCHAR(MAX)  -- Changed to NVARCHAR(MAX) to support Unicode PBKDF2 hash
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UserID INT = NULL;
    DECLARE @ResetRequestID INT = NULL;
    DECLARE @IsValid BIT = 0;
    DECLARE @Message NVARCHAR(255) = 'Invalid reset request';
    
    -- Find user by email first
    SELECT @UserID = UserID 
    FROM Users 
    WHERE Email = @Email;
    
    IF @UserID IS NULL
    BEGIN
        SELECT 
            0 AS Success,
            'User not found' AS Message;
        RETURN;
    END
    
    -- Try to find reset request in common table names
    -- Check PasswordResets table
    IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = ResetID
        FROM PasswordResets
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL);
        
        IF @ResetRequestID IS NOT NULL
        BEGIN
            SET @IsValid = 1;
        END
    END
    
    -- If not found, check other common table names
    IF @IsValid = 0 AND OBJECT_ID('ResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID
        FROM ResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Step 3: Update password if valid
    IF @IsValid = 1 AND @UserID IS NOT NULL
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            
            -- Update password hash - CRITICAL: Use NVARCHAR to preserve Unicode characters
            UPDATE Users 
            SET PasswordHash = CAST(@NewPassword AS NVARCHAR(MAX)),  -- Explicit cast to ensure Unicode
                UpdatedAt = SYSUTCDATETIME()
            WHERE UserID = @UserID;
            
            -- Mark reset request as used
            IF @ResetRequestID IS NOT NULL
            BEGIN
                IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
                BEGIN
                    UPDATE PasswordResets 
                    SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                    WHERE ResetID = @ResetRequestID;
                END
                ELSE IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
                BEGIN
                    UPDATE ResetTokens 
                    SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                    WHERE TokenID = @ResetRequestID;
                END
            END
            
            COMMIT TRANSACTION;
            
            SET @Message = 'Password updated successfully';
            
            SELECT 
                1 AS Success,
                @Message AS Message;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;
            
            SET @Message = 'Failed to update password: ' + ERROR_MESSAGE();
            
            SELECT 
                0 AS Success,
                @Message AS Message;
        END CATCH
    END
    ELSE
    BEGIN
        SELECT 
            0 AS Success,
            @Message AS Message;
    END
END;
GO
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UserID INT;
    DECLARE @ResetRequestID INT;
    DECLARE @IsValid BIT = 0;
    DECLARE @Message NVARCHAR(255) = 'Invalid reset request';
    
    -- Step 1: Find the reset request
    -- Check common table names for password reset
    IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = ResetID,
            @UserID = UserID
        FROM PasswordResets
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND IsUsed = 0
          AND ExpiresAt > SYSUTCDATETIME()
          AND IsValidated = 1; -- OTP must be validated first
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    ELSE IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID,
            @UserID = UserID
        FROM ResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND IsUsed = 0
          AND ExpiresAt > SYSUTCDATETIME()
          AND IsValidated = 1;
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    ELSE IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID,
            @UserID = UserID
        FROM UserResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND IsUsed = 0
          AND ExpiresAt > SYSUTCDATETIME()
          AND IsValidated = 1;
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    ELSE IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID,
            @UserID = UserID
        FROM PasswordResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND IsUsed = 0
          AND ExpiresAt > SYSUTCDATETIME()
          AND IsValidated = 1;
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    ELSE IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = RequestID,
            @UserID = UserID
        FROM ForgotPasswordRequests
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND IsUsed = 0
          AND ExpiresAt > SYSUTCDATETIME()
          AND IsValidated = 1;
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- If no reset request found, try to find user by email
    IF @IsValid = 0 AND @UserID IS NULL
    BEGIN
        SELECT @UserID = UserID 
        FROM Users 
        WHERE Email = @Email;
        
        IF @UserID IS NOT NULL
        BEGIN
            -- If user exists but no valid reset request, return error
            SET @Message = 'Invalid or expired reset request';
        END
        ELSE
        BEGIN
            SET @Message = 'User not found';
        END
    END
    
    -- Step 2: Update password if valid
    IF @IsValid = 1 AND @UserID IS NOT NULL
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            
            -- Update password hash - ensure it's stored as NVARCHAR
            UPDATE Users 
            SET PasswordHash = @NewPassword,  -- Store as NVARCHAR to preserve Unicode
                UpdatedAt = SYSUTCDATETIME()
            WHERE UserID = @UserID;
            
            -- Mark reset request as used
            IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
            BEGIN
                UPDATE PasswordResets 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE ResetID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('ResetTokens', 'U') IS NOT NULL
            BEGIN
                UPDATE ResetTokens 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE TokenID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
            BEGIN
                UPDATE UserResetTokens 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE TokenID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
            BEGIN
                UPDATE PasswordResetTokens 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE TokenID = @ResetRequestID;
            END
            ELSE IF OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
            BEGIN
                UPDATE ForgotPasswordRequests 
                SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
                WHERE RequestID = @ResetRequestID;
            END
            
            COMMIT TRANSACTION;
            
            SET @Message = 'Password updated successfully';
            
            SELECT 
                1 AS Success,
                @Message AS Message;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;
            
            SET @Message = 'Failed to update password: ' + ERROR_MESSAGE();
            
            SELECT 
                0 AS Success,
                @Message AS Message;
        END CATCH
    END
    ELSE
    BEGIN
        SELECT 
            0 AS Success,
            @Message AS Message;
    END
END;
GO

PRINT 'sp_ForgetPassword_ResetPassword procedure created successfully';
GO

-- Step 3: Verify Users table PasswordHash column supports Unicode
PRINT '=== Checking Users.PasswordHash Column ===';
IF EXISTS (
    SELECT 1 
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('Users')
      AND c.name = 'PasswordHash'
      AND t.name = 'varchar'  -- If it's VARCHAR, we need to change it
)
BEGIN
    PRINT 'WARNING: PasswordHash column is VARCHAR. It should be NVARCHAR to support Unicode PBKDF2 hashes.';
    PRINT 'Consider running: ALTER TABLE Users ALTER COLUMN PasswordHash NVARCHAR(MAX);';
END
ELSE
BEGIN
    PRINT 'PasswordHash column type is OK (NVARCHAR or compatible)';
END
GO

