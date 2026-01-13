-- =============================================
-- Add Phone Column to Enterprises Table
-- =============================================
-- This column stores the phone number for Zoho customer creation

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'Phone'
)
BEGIN
    ALTER TABLE Enterprises ADD Phone NVARCHAR(20) NULL;
    PRINT 'Added Phone column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'Phone column already exists';
END
GO

PRINT 'Phone column check completed';
GO


















