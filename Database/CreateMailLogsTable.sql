-- =============================================
-- MailLogs Table
-- =============================================
-- Stores email sending logs for tracking and auditing
-- Keyed by: BrandingId

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MailLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE MailLogs
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        BrandingId INT NOT NULL,
        FromEmail NVARCHAR(200) NOT NULL,
        ToEmail NVARCHAR(200) NOT NULL,
        Subject NVARCHAR(300) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        SentAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );

    -- Index for faster queries by BrandingId
    CREATE INDEX IX_MailLogs_BrandingId ON MailLogs(BrandingId);
    
    -- Index for faster queries by Status
    CREATE INDEX IX_MailLogs_Status ON MailLogs(Status);
    
    -- Index for faster queries by SentAt
    CREATE INDEX IX_MailLogs_SentAt ON MailLogs(SentAt);

    PRINT 'MailLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'MailLogs table already exists';
END
GO



