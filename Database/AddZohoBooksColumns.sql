-- =============================================
-- Add Zoho Books Integration Columns to Enterprises Table
-- =============================================

-- Add Zoho Customer ID
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'ZohoCustomerId'
)
BEGIN
    ALTER TABLE Enterprises ADD ZohoCustomerId NVARCHAR(100) NULL;
    PRINT 'Added ZohoCustomerId column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'ZohoCustomerId column already exists';
END
GO

-- Add Zoho Recurring Invoice ID
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'ZohoRecurringInvoiceId'
)
BEGIN
    ALTER TABLE Enterprises ADD ZohoRecurringInvoiceId NVARCHAR(100) NULL;
    PRINT 'Added ZohoRecurringInvoiceId column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'ZohoRecurringInvoiceId column already exists';
END
GO

-- Add Billing Day of Month (e.g., 15 means 15th of every month)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'BillingDayOfMonth'
)
BEGIN
    ALTER TABLE Enterprises ADD BillingDayOfMonth INT NULL;
    PRINT 'Added BillingDayOfMonth column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'BillingDayOfMonth column already exists';
END
GO

-- Add Last Payment Date
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'LastPaymentDate'
)
BEGIN
    ALTER TABLE Enterprises ADD LastPaymentDate DATETIME NULL;
    PRINT 'Added LastPaymentDate column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'LastPaymentDate column already exists';
END
GO

-- Add Next Billing Date
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'NextBillingDate'
)
BEGIN
    ALTER TABLE Enterprises ADD NextBillingDate DATETIME NULL;
    PRINT 'Added NextBillingDate column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'NextBillingDate column already exists';
END
GO

-- Add Monthly Billing Amount (optional, for reference)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Enterprises') 
    AND name = 'MonthlyBillingAmount'
)
BEGIN
    ALTER TABLE Enterprises ADD MonthlyBillingAmount DECIMAL(18,2) NULL;
    PRINT 'Added MonthlyBillingAmount column to Enterprises table';
END
ELSE
BEGIN
    PRINT 'MonthlyBillingAmount column already exists';
END
GO

PRINT 'Zoho Books integration columns added successfully';
GO























