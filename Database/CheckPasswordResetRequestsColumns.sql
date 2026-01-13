-- Check actual column names in PasswordResetRequests table
SELECT 
    c.name AS ColumnName,
    ty.name AS DataType,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    CASE 
        WHEN pk.column_name IS NOT NULL THEN 'PRIMARY KEY'
        ELSE ''
    END AS KeyType
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
INNER JOIN sys.tables t ON c.object_id = t.object_id
LEFT JOIN (
    SELECT 
        kcu.table_name,
        kcu.column_name
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
        ON tc.constraint_name = kcu.constraint_name
    WHERE tc.constraint_type = 'PRIMARY KEY'
) pk ON t.name = pk.table_name AND c.name = pk.column_name
WHERE t.name = 'PasswordResetRequests'
ORDER BY c.column_id;






