-- =============================================
-- WhatsappConfig Table
-- =============================================
-- Stores WhatsApp configuration for branding
-- Keyed by: BrandingId

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WhatsappConfig]') AND type in (N'U'))
BEGIN
    CREATE TABLE WhatsappConfig
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        BrandingId INT NOT NULL,
        AppKey NVARCHAR(200) NOT NULL,
        AuthKey NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        
        CONSTRAINT UQ_WhatsappConfig_BrandingId UNIQUE (BrandingId)
    );

    PRINT 'WhatsappConfig table created successfully';
END
ELSE
BEGIN
    PRINT 'WhatsappConfig table already exists';
END
GO












