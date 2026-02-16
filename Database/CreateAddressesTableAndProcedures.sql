-- =============================================
-- Generic Addresses Table + Stored Procedures
-- =============================================
-- Stores addresses for multiple owner types (User / Enterprise / Label / SuperAdmin)
-- Keyed by: Type + OwnerId

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Addresses]') AND type in (N'U'))
BEGIN
    CREATE TABLE Addresses
    (
        AddressId INT IDENTITY(1,1) PRIMARY KEY,
        [Type] NVARCHAR(30) NOT NULL,
        OwnerId INT NOT NULL,

        AddressLine1 NVARCHAR(200),
        AddressLine2 NVARCHAR(200),
        City NVARCHAR(100),
        State NVARCHAR(100),
        Country NVARCHAR(100),
        Pincode NVARCHAR(10),

        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,

        CONSTRAINT UQ_Addresses_Type_Owner UNIQUE ([Type], OwnerId)
    );

    PRINT 'Addresses table created successfully';
END
ELSE
BEGIN
    PRINT 'Addresses table already exists';
END
GO

-- 1) UPSERT (INSERT + UPDATE)
CREATE OR ALTER PROCEDURE sp_Addresses_Upsert
(
    @Type NVARCHAR(30),
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
        SELECT 1 FROM Addresses
        WHERE [Type] = @Type
          AND OwnerId = @OwnerId
    )
    BEGIN
        UPDATE Addresses
        SET
            AddressLine1 = @AddressLine1,
            AddressLine2 = @AddressLine2,
            City = @City,
            State = @State,
            Country = @Country,
            Pincode = @Pincode,
            UpdatedAt = SYSUTCDATETIME()
        WHERE [Type] = @Type
          AND OwnerId = @OwnerId;
    END
    ELSE
    BEGIN
        INSERT INTO Addresses
        (
            [Type], OwnerId,
            AddressLine1, AddressLine2,
            City, State, Country, Pincode
        )
        VALUES
        (
            @Type, @OwnerId,
            @AddressLine1, @AddressLine2,
            @City, @State, @Country, @Pincode
        );
    END
END;
GO

-- 2) DELETE ADDRESS
CREATE OR ALTER PROCEDURE sp_Addresses_Delete
(
    @Type NVARCHAR(30),
    @OwnerId INT
)
AS
BEGIN
    DELETE FROM Addresses
    WHERE [Type] = @Type
      AND OwnerId = @OwnerId;
END;
GO

-- 3) GET ADDRESS BY OWNER
CREATE OR ALTER PROCEDURE sp_Addresses_GetByOwner
(
    @Type NVARCHAR(30),
    @OwnerId INT
)
AS
BEGIN
    SELECT
        AddressId,
        [Type],
        OwnerId,
        AddressLine1,
        AddressLine2,
        City,
        State,
        Country,
        Pincode,
        CreatedAt,
        UpdatedAt
    FROM Addresses
    WHERE [Type] = @Type
      AND OwnerId = @OwnerId;
END;
GO

PRINT 'Addresses table + procedures are ready (sp_Addresses_Upsert / Delete / GetByOwner)';
GO


