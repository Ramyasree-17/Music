-- =============================================
-- Test Password Verification for d@g.com
-- =============================================
-- This script helps debug why login fails after password reset

DECLARE @TestEmail NVARCHAR(255) = 'd@g.com';
DECLARE @TestPassword NVARCHAR(255) = 'd12345678';

-- Step 1: Check what sp_Auth_Login returns
PRINT '=== Step 1: Check sp_Auth_Login Output ===';
SELECT OBJECT_DEFINITION(OBJECT_ID('sp_Auth_Login')) AS ProcedureDefinition;

-- Step 2: Check the actual stored password hash
PRINT '=== Step 2: Check Stored Password Hash ===';
SELECT 
    UserID,
    Email,
    PasswordHash,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,
    -- Check if there are any NULL characters or issues
    CASE 
        WHEN PasswordHash IS NULL THEN 'NULL'
        WHEN LEN(PasswordHash) = 0 THEN 'EMPTY'
        WHEN PasswordHash LIKE '%' + CHAR(0) + '%' THEN 'Contains NULL char'
        ELSE 'OK'
    END AS HashStatus,
    UpdatedAt
FROM Users
WHERE Email = @TestEmail;

-- Step 3: Simulate what sp_Auth_Login might be doing
PRINT '=== Step 3: Simulate Login Query ===';
SELECT 
    UserID,
    FullName,
    Email,
    Role,
    PasswordHash,
    LEN(PasswordHash) AS HashLength,
    DATALENGTH(PasswordHash) AS HashBytes,
    -- Check data type
    SQL_VARIANT_PROPERTY(PasswordHash, 'BaseType') AS BaseType,
    SQL_VARIANT_PROPERTY(PasswordHash, 'MaxLength') AS MaxLength
FROM Users
WHERE Email = @TestEmail;

-- Step 4: Check if PasswordHash column has any constraints or issues
PRINT '=== Step 4: Check Column Constraints ===';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    c.collation_name AS Collation,
    dc.definition AS DefaultValue
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID('Users')
  AND c.name = 'PasswordHash';

-- Step 5: Check for any triggers that might modify PasswordHash
PRINT '=== Step 5: Check for Triggers ===';
SELECT 
    t.name AS TriggerName,
    OBJECT_DEFINITION(t.object_id) AS TriggerDefinition
FROM sys.triggers t
WHERE t.parent_id = OBJECT_ID('Users')
  AND OBJECT_DEFINITION(t.object_id) LIKE '%PasswordHash%';







