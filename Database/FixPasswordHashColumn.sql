-- =============================================
-- Fix PasswordHash Column to Support Unicode
-- =============================================
-- This script ensures PasswordHash column can store Unicode PBKDF2 hashes

-- Step 1: Check current column type
PRINT '=== Step 1: Check Current Column Type ===';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Users')
  AND c.name = 'PasswordHash';

-- Step 2: Fix column if it's VARCHAR
-- WARNING: This will modify the table structure
-- Make sure to backup your database first!

IF EXISTS (
    SELECT 1 
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('Users')
      AND c.name = 'PasswordHash'
      AND t.name = 'varchar'  -- If it's VARCHAR, we need to change it
)
BEGIN
    PRINT 'WARNING: PasswordHash is VARCHAR. Changing to NVARCHAR(MAX) to support Unicode...';
    
    -- Change column to NVARCHAR(MAX)
    ALTER TABLE Users 
    ALTER COLUMN PasswordHash NVARCHAR(MAX) NULL;
    
    PRINT 'PasswordHash column changed to NVARCHAR(MAX) successfully';
END
ELSE IF EXISTS (
    SELECT 1 
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID('Users')
      AND c.name = 'PasswordHash'
      AND t.name = 'nvarchar'
)
BEGIN
    PRINT 'PasswordHash column is already NVARCHAR. Checking max length...';
    
    -- Check if it's NVARCHAR(MAX) or has a limit
    DECLARE @MaxLength INT;
    SELECT @MaxLength = c.max_length
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID('Users')
      AND c.name = 'PasswordHash';
    
    IF @MaxLength != -1  -- -1 means MAX
    BEGIN
        PRINT 'PasswordHash has length limit. Changing to NVARCHAR(MAX)...';
        ALTER TABLE Users 
        ALTER COLUMN PasswordHash NVARCHAR(MAX) NULL;
        PRINT 'PasswordHash column changed to NVARCHAR(MAX) successfully';
    END
    ELSE
    BEGIN
        PRINT 'PasswordHash is already NVARCHAR(MAX). No changes needed.';
    END
END
ELSE
BEGIN
    PRINT 'PasswordHash column type is: ' + 
          (SELECT t.name FROM sys.columns c
           INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
           WHERE c.object_id = OBJECT_ID('Users') AND c.name = 'PasswordHash');
END
GO

-- Step 3: Verify the change
PRINT '=== Step 3: Verify Column Type ===';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    CASE 
        WHEN c.max_length = -1 THEN 'MAX'
        ELSE CAST(c.max_length AS VARCHAR(10))
    END AS MaxLengthDisplay
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('Users')
  AND c.name = 'PasswordHash';
GO







