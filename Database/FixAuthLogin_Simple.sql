-- =============================================
-- Simple Fix: sp_Auth_Login PasswordHash
-- =============================================
-- Fix: Ensure PasswordHash is returned correctly with Unicode preservation

-- Drop and recreate
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
        -- CRITICAL: Cast to NVARCHAR(MAX) to preserve Unicode PBKDF2 hash
        CAST(PasswordHash AS NVARCHAR(MAX)) AS PasswordHash,
        Status,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users
    WHERE Email = @Email
      AND (IsActive = 1 OR IsActive IS NULL);
END;
GO

PRINT 'sp_Auth_Login fixed successfully';
GO

-- Test
EXEC sp_Auth_Login @Email = 'd@g.com';
GO







