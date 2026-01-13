-- =============================================
-- Add Email and OwnerEmail fields to Labels table
-- =============================================

-- Step 1: Add Email column to Labels table if it doesn't exist
PRINT '=== Step 1: Adding Email column to Labels table ===';
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('Labels')
      AND name = 'Email'
)
BEGIN
    ALTER TABLE Labels 
    ADD Email NVARCHAR(255) NULL;
    PRINT 'Email column added successfully!';
END
ELSE
BEGIN
    PRINT 'Email column already exists.';
END
GO

-- Step 2: Add OwnerEmail column to Labels table if it doesn't exist
PRINT '=== Step 2: Adding OwnerEmail column to Labels table ===';
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('Labels')
      AND name = 'OwnerEmail'
)
BEGIN
    ALTER TABLE Labels 
    ADD OwnerEmail NVARCHAR(255) NULL;
    PRINT 'OwnerEmail column added successfully!';
END
ELSE
BEGIN
    PRINT 'OwnerEmail column already exists.';
END
GO

-- Step 3: Verify the columns were added
PRINT '=== Step 3: Verifying columns ===';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Labels')
  AND c.name IN ('Email', 'OwnerEmail')
ORDER BY c.name;
GO

PRINT '=== Labels table Email fields added successfully! ===';
GO





