-- =============================================
-- Fix sp_Auth_Login to Preserve Unicode PasswordHash
-- =============================================
-- This ensures PasswordHash is returned as NVARCHAR to preserve Unicode

-- Step 1: Check current procedure
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
BEGIN
    PRINT 'Current sp_Auth_Login exists. Checking definition...';
    SELECT OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS CurrentDefinition;
END
ELSE
BEGIN
    PRINT 'sp_Auth_Login does not exist. Creating new procedure...';
END
GO

-- Step 2: Create/Update procedure to ensure NVARCHAR return
IF OBJECT_ID('sp_Auth_Login', 'P') IS NOT NULL
    DROP PROCEDURE sp_Auth_Login;
GO

CREATE PROCEDURE [dbo].[sp_Auth_Login]
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- CRITICAL: Use CAST to ensure PasswordHash is returned as NVARCHAR(MAX)
    -- This preserves Unicode characters in PBKDF2 hash
    SELECT 
        UserID,
        FullName,
        Email,
        Role,
        CAST(PasswordHash AS NVARCHAR(MAX)) AS PasswordHash,  -- Explicit NVARCHAR cast
        Status,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users
    WHERE Email = @Email
      AND IsActive = 1;
END;
GO

PRINT 'sp_Auth_Login procedure created/updated successfully';
GO

-- Step 3: Test the procedure
PRINT '=== Step 3: Test sp_Auth_Login ===';
DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';

EXEC sp_Auth_Login @Email = @TestEmail;
GO







