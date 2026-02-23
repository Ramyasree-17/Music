-- =============================================
-- Check if Bulk Upload Tables Exist
-- =============================================
-- Run this to verify tables are created

PRINT '=== Checking Bulk Upload Tables ===';

-- Check BulkJobs table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BulkJobs]') AND type in (N'U'))
BEGIN
    PRINT '✅ BulkJobs table EXISTS';
    
    -- Show table structure
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'BulkJobs'
    ORDER BY ORDINAL_POSITION;
END
ELSE
BEGIN
    PRINT '❌ BulkJobs table DOES NOT EXIST';
    PRINT '   Run: Database/CreateBulkUploadTables.sql';
END

PRINT '';

-- Check BulkJobLogs table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BulkJobLogs]') AND type in (N'U'))
BEGIN
    PRINT '✅ BulkJobLogs table EXISTS';
    
    -- Show table structure
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        IS_NULLABLE,
        COLUMN_DEFAULT
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'BulkJobLogs'
    ORDER BY ORDINAL_POSITION;
END
ELSE
BEGIN
    PRINT '❌ BulkJobLogs table DOES NOT EXIST';
    PRINT '   Run: Database/CreateBulkUploadTables.sql';
END

GO


