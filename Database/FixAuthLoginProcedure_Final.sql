-- =============================================
-- CRITICAL FIX: sp_Auth_Login PasswordHash Unicode Issue
-- =============================================
-- The issue: sp_Auth_Login returns PasswordHash as VARCHAR, corrupting Unicode PBKDF2 hashes
-- This fix ensures PasswordHash is returned as NVARCHAR(MAX) to preserve Unicode

-- Step 1: Drop existing procedure
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
BEGIN
    PRINT 'Dropping existing sp_Auth_Login...';
    DROP PROCEDURE sp_Auth_Login;
END
GO

-- Step 2: Create fixed procedure with NVARCHAR(MAX) for PasswordHash
CREATE PROCEDURE [dbo].[sp_Auth_Login]
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- CRITICAL: Use CAST to NVARCHAR(MAX) to preserve Unicode characters
    -- Without this, Unicode PBKDF2 hashes get corrupted when read
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

PRINT 'sp_Auth_Login procedure created successfully with NVARCHAR(MAX) PasswordHash';
GO

-- Step 3: Test the procedure
PRINT '=== Testing sp_Auth_Login ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

EXEC sp_Auth_Login @Email = @TestEmail;
GO







