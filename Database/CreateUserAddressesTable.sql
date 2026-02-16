-- =============================================
-- UserAddresses Table (Dedicated for Users)
-- =============================================
-- Stores addresses ONLY for Users (not Enterprise/Label)
-- Keyed by: OwnerId (which is UserID)

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserAddresses]') AND type in (N'U'))
BEGIN
    CREATE TABLE UserAddresses
    (
        AddressId INT IDENTITY(1,1) PRIMARY KEY,
        OwnerId INT NOT NULL,  -- FK -> Users.UserID

        AddressLine1 NVARCHAR(200),
        AddressLine2 NVARCHAR(200),
        City NVARCHAR(100),
        State NVARCHAR(100),
        Country NVARCHAR(100),
        Pincode NVARCHAR(10),

        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,

        CONSTRAINT FK_UserAddresses_Users FOREIGN KEY (OwnerId) REFERENCES Users(UserID),
        CONSTRAINT UQ_UserAddresses_OwnerId UNIQUE (OwnerId)  -- One address per user
    );

    PRINT 'UserAddresses table created successfully';
END
ELSE
BEGIN
    PRINT 'UserAddresses table already exists';
END
GO

-- UPSERT PROCEDURE (INSERT + UPDATE)
CREATE OR ALTER PROCEDURE sp_UserAddresses_Upsert
(
    @OwnerId INT,
    @AddressLine1 NVARCHAR(200),
    @AddressLine2 NVARCHAR(200),
    @City NVARCHAR(100),
    @State NVARCHAR(100),
    @Country NVARCHAR(100),
    @Pincode NVARCHAR(10)
)
AS
BEGIN
    IF EXISTS (
        SELECT 1 FROM UserAddresses
        WHERE OwnerId = @OwnerId
    )
    BEGIN
        UPDATE UserAddresses
        SET
            AddressLine1 = @AddressLine1,
            AddressLine2 = @AddressLine2,
            City = @City,
            State = @State,
            Country = @Country,
            Pincode = @Pincode,
            UpdatedAt = SYSUTCDATETIME()
        WHERE OwnerId = @OwnerId;
    END
    ELSE
    BEGIN
        INSERT INTO UserAddresses
        (
            OwnerId,
            AddressLine1, AddressLine2,
            City, State, Country, Pincode
        )
        VALUES
        (
            @OwnerId,
            @AddressLine1, @AddressLine2,
            @City, @State, @Country, @Pincode
        );
    END
END;
GO

-- GET ADDRESS BY OWNER
CREATE OR ALTER PROCEDURE sp_UserAddresses_GetByOwner
(
    @OwnerId INT
)
AS
BEGIN
    SELECT
        AddressId,
        OwnerId,
        AddressLine1,
        AddressLine2,
        City,
        State,
        Country,
        Pincode,
        CreatedAt,
        UpdatedAt
    FROM UserAddresses
    WHERE OwnerId = @OwnerId;
END;
GO

-- CHECK IF ADDRESS EXISTS
CREATE OR ALTER PROCEDURE sp_UserAddresses_Exists
(
    @OwnerId INT
)
AS
BEGIN
    SELECT COUNT(1)
    FROM UserAddresses
    WHERE OwnerId = @OwnerId;
END;
GO

PRINT 'UserAddresses table + procedures are ready (sp_UserAddresses_Upsert / GetByOwner / Exists)';
GO














