-- =============================================
-- Simple Fix for Password Reset Login Issue
-- =============================================
-- This fixes the password reset so login works after reset

-- Step 1: Ensure PasswordHash column supports Unicode
PRINT '=== Step 1: Fix PasswordHash Column ===';
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

-- Step 2: Fix stored procedure
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
    DECLARE @Success BIT = 0;
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
    
    -- Check if reset request exists and is valid
    -- Try common table names
    DECLARE @ResetFound BIT = 0;
    
    IF OBJECT_ID('PasswordResets', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM PasswordResets
            WHERE Email = @Email 
              AND ResetKey = @Key
              AND (IsUsed = 0 OR IsUsed IS NULL)
              AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
              AND (IsValidated = 1 OR IsValidated IS NULL)
        )
        BEGIN
            SET @ResetFound = 1;
            UPDATE PasswordResets SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
            WHERE Email = @Email AND ResetKey = @Key;
        END
    END
    
    -- If not found, try other tables
    IF @ResetFound = 0 AND OBJECT_ID('ResetTokens', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1 FROM ResetTokens
            WHERE Email = @Email 
              AND ResetKey = @Key
              AND (IsUsed = 0 OR IsUsed IS NULL)
              AND (ExpiresAt > SYSUTCDATETIME() OR ExpiresAt IS NULL)
              AND (IsValidated = 1 OR IsValidated IS NULL)
        )
        BEGIN
            SET @ResetFound = 1;
            UPDATE ResetTokens SET IsUsed = 1, UsedAt = SYSUTCDATETIME()
            WHERE Email = @Email AND ResetKey = @Key;
        END
    END
    
    -- Update password if reset request is valid
    IF @ResetFound = 1
    BEGIN
        BEGIN TRY
            -- CRITICAL: Store password hash as NVARCHAR to preserve Unicode characters
            UPDATE Users 
            SET PasswordHash = @NewPassword,  -- Already NVARCHAR(MAX), preserves Unicode
                UpdatedAt = SYSUTCDATETIME()
            WHERE UserID = @UserID;
            
            SET @Success = 1;
            SET @Message = 'Password updated successfully';
        END TRY
        BEGIN CATCH
            SET @Message = 'Failed to update password: ' + ERROR_MESSAGE();
        END CATCH
    END
    
    SELECT @Success AS Success, @Message AS Message;
END;
GO

PRINT 'sp_ForgetPassword_ResetPassword procedure created successfully';
GO

-- Step 3: Verify the fix
PRINT '=== Step 3: Verify Column Type ===';
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







