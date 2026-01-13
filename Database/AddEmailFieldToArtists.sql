-- =============================================
-- Add Email field to Artists table
-- =============================================

-- Step 1: Add Email column to Artists table if it doesn't exist
PRINT '=== Step 1: Adding Email column to Artists table ===';
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('Artists')
      AND name = 'Email'
)
BEGIN
    ALTER TABLE Artists 
    ADD Email NVARCHAR(255) NULL;
    PRINT 'Email column added successfully!';
END
ELSE
BEGIN
    PRINT 'Email column already exists.';
END
GO

-- Step 2: Verify the column was added
PRINT '=== Step 2: Verifying column ===';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Artists')
  AND c.name = 'Email';
GO

PRINT '=== Artists table Email field added successfully! ===';
GO





