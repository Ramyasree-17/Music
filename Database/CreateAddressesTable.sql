-- =============================================
-- Create Addresses Table (Generic for Enterprise/Label/User/etc.)
-- =============================================
-- Key design:
--   Type   = 'Enterprise' | 'Label' | 'SuperAdmin' | 'User' | etc.
--   OwnerId = EntityId (EnterpriseId/LabelId/UserId)

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Addresses]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Addresses]
    (
        AddressId INT IDENTITY(1,1) PRIMARY KEY,

        [Type] NVARCHAR(30) NOT NULL,
        OwnerId INT NOT NULL,

        AddressLine1 NVARCHAR(200) NULL,
        AddressLine2 NVARCHAR(200) NULL,
        City NVARCHAR(100) NULL,
        [State] NVARCHAR(100) NULL,
        Country NVARCHAR(100) NULL,
        Pincode NVARCHAR(10) NULL,

        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Addresses_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL
    );

    -- Ensure no duplicate address row for same owner
    CREATE UNIQUE INDEX UX_Addresses_Type_OwnerId
        ON [dbo].[Addresses]([Type], OwnerId);

    PRINT 'Addresses table created successfully';
END
ELSE
BEGIN
    PRINT 'Addresses table already exists';
END
GO


