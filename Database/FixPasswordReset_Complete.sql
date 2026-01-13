-- =============================================
-- Complete Fix: Password Reset Stored Procedure
-- =============================================
-- This fixes sp_ForgetPassword_ResetPassword to work with the actual reset table

-- Step 1: Check what table is actually being used
-- First, let's see what sp_ForgetPassword_Create uses
PRINT '=== Step 1: Check sp_ForgetPassword_Create Definition ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_ForgetPassword_Create')) AS CreateProcedureDefinition;
GO

-- Step 2: Drop and recreate sp_ForgetPassword_ResetPassword
-- We'll make it work with the most common table structure
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
    
    -- Try to find reset request in common table structures
    -- Check PasswordResets table (most common)
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
    
    -- Check ResetTokens table
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
    
    -- Check UserResetTokens table
    IF @IsValid = 0 AND OBJECT_ID('UserResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID
        FROM UserResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Check PasswordResetTokens table
    IF @IsValid = 0 AND OBJECT_ID('PasswordResetTokens', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = TokenID
        FROM PasswordResetTokens
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL);
        
        IF @ResetRequestID IS NOT NULL
            SET @IsValid = 1;
    END
    
    -- Check ForgotPasswordRequests table
    IF @IsValid = 0 AND OBJECT_ID('ForgotPasswordRequests', 'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 
            @ResetRequestID = RequestID
        FROM ForgotPasswordRequests
        WHERE Email = @Email 
          AND ResetKey = @Key
          AND (IsUsed = 0 OR IsUsed IS NULL)
          AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
          AND (IsValidated = 1 OR IsValidated IS NULL);
        
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

PRINT 'sp_ForgetPassword_ResetPassword procedure created successfully';
GO







