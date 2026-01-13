-- =============================================
-- Check actual column names in Enterprises table
-- =============================================
-- Run this first to see what columns actually exist

SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Enterprises'
ORDER BY ORDINAL_POSITION;







