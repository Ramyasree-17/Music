-- Remove PrimaryLanguage column from Artists table
-- This script safely checks if the column exists before dropping it

-- Check if column exists
IF EXISTS (
    SELECT COLUMN_NAME
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Artists'
    AND COLUMN_NAME = 'PrimaryLanguage'
)
BEGIN
    -- Drop the column
    ALTER TABLE Artists
    DROP COLUMN PrimaryLanguage;
    
    PRINT 'PrimaryLanguage column has been removed from Artists table.';
END
ELSE
BEGIN
    PRINT 'PrimaryLanguage column does not exist in Artists table.';
END
GO






