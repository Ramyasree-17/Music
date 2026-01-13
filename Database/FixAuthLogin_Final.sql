-- =============================================
-- FINAL FIX: sp_Auth_Login PasswordHash Unicode Issue
-- =============================================
-- This ensures PasswordHash is returned correctly to fix login after password reset

-- Drop and recreate with explicit NVARCHAR(MAX) cast
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
    DROP PROCEDURE sp_Auth_Login;
GO

CREATE PROCEDURE [dbo].[sp_Auth_Login]
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- CRITICAL: Explicitly cast PasswordHash to NVARCHAR(MAX)
    -- This ensures Unicode characters are preserved when returned to C#
    SELECT 
        UserID,
        FullName,
        Email,
        Role,
        CAST(PasswordHash AS NVARCHAR(MAX)) AS PasswordHash,  -- Explicit cast preserves Unicode
        Status,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users
    WHERE Email = @Email
      AND (IsActive = 1 OR IsActive IS NULL);
END;
GO

PRINT 'sp_Auth_Login fixed - PasswordHash now returns as NVARCHAR(MAX)';
GO

-- Test
EXEC sp_Auth_Login @Email = 'd@g.com';
GO







