-- =============================================
-- Bulk Upload Tables
-- =============================================
-- Tables for bulk upload job tracking and logging

-- BulkJobs Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BulkJobs]') AND type in (N'U'))
BEGIN
    CREATE TABLE BulkJobs
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FileName NVARCHAR(500) NOT NULL,
        FilePath NVARCHAR(1000) NOT NULL,
        TotalRows INT NOT NULL DEFAULT 0,
        ProcessedRows INT NOT NULL DEFAULT 0,
        SuccessCount INT NOT NULL DEFAULT 0,
        FailureCount INT NOT NULL DEFAULT 0,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        StartedAt DATETIME2 NULL,
        CompletedAt DATETIME2 NULL,
        CreatedByUserId INT NULL,
        ErrorMessage NVARCHAR(MAX) NULL
    );

    CREATE INDEX IX_BulkJobs_Status ON BulkJobs(Status);
    CREATE INDEX IX_BulkJobs_CreatedAt ON BulkJobs(CreatedAt);
    CREATE INDEX IX_BulkJobs_CreatedByUserId ON BulkJobs(CreatedByUserId);

    PRINT 'BulkJobs table created successfully';
END
ELSE
BEGIN
    PRINT 'BulkJobs table already exists';
END
GO

-- BulkJobLogs Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BulkJobLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE BulkJobLogs
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        JobId INT NOT NULL,
        RowNumber INT NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- Success, Failed
        Message NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        ReleaseTitle NVARCHAR(500) NULL,
        TrackTitle NVARCHAR(500) NULL,
        ReleaseId INT NULL,
        TrackId INT NULL,
        
        CONSTRAINT FK_BulkJobLogs_BulkJobs FOREIGN KEY (JobId) REFERENCES BulkJobs(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_BulkJobLogs_JobId ON BulkJobLogs(JobId);
    CREATE INDEX IX_BulkJobLogs_JobId_RowNumber ON BulkJobLogs(JobId, RowNumber);
    CREATE INDEX IX_BulkJobLogs_Status ON BulkJobLogs(Status);

    PRINT 'BulkJobLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'BulkJobLogs table already exists';
END
GO


