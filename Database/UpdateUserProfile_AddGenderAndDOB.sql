-- ===========================================================
-- Update User Profile: Add Gender + DateOfBirth + proc support
-- Safe to run multiple times (idempotent)
-- ===========================================================

-- 1) Add columns if missing
IF COL_LENGTH('dbo.Users', 'Gender') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD Gender NVARCHAR(20) NULL;
    PRINT 'Added dbo.Users.Gender';
END
ELSE
BEGIN
    PRINT 'dbo.Users.Gender already exists';
END
GO

IF COL_LENGTH('dbo.Users', 'DateOfBirth') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD DateOfBirth DATE NULL;
    PRINT 'Added dbo.Users.DateOfBirth';
END
ELSE
BEGIN
    PRINT 'dbo.Users.DateOfBirth already exists';
END
GO

-- 2) Extend stored procedure to accept and save the new fields
CREATE OR ALTER PROCEDURE dbo.sp_Users_UpdateProfile
(
    @UserId INT,
    @FullName NVARCHAR(200) = NULL,
    @Mobile NVARCHAR(30) = NULL,
    @CountryCode NVARCHAR(10) = NULL,
    @Gender NVARCHAR(20) = NULL,
    @DateOfBirth DATE = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET
        FullName = COALESCE(@FullName, FullName),
        Mobile = @Mobile,
        CountryCode = @CountryCode,
        Gender = @Gender,
        DateOfBirth = @DateOfBirth
    WHERE UserID = @UserId;

    SELECT @@ROWCOUNT;
END;
GO

PRINT 'sp_Users_UpdateProfile updated (Gender, DateOfBirth supported)';
GO















