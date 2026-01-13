-- =============================================
-- DELETE ENTERPRISES AND LABELS DATA
-- =============================================
-- ⚠️ WARNING: This will delete ALL data from Enterprises and Labels tables!
-- This script handles foreign key constraints properly
-- Run this on TEST database only!

PRINT '=== Step 1: Checking foreign key relationships ===';
GO

-- =============================================
-- METHOD 1: Delete in correct order (RECOMMENDED)
-- =============================================
PRINT '=== Step 2: Deleting child tables first (Labels and related) ===';

-- Delete UserLabelRoles first (if exists - depends on Labels)
IF OBJECT_ID('UserLabelRoles', 'U') IS NOT NULL
BEGIN
    DELETE FROM UserLabelRoles;
    PRINT 'Deleted from UserLabelRoles';
END

-- Delete Labels (child of Enterprises)
DELETE FROM Labels;
PRINT 'Deleted from Labels';

-- Delete EnterpriseUserRoles (if exists - depends on Enterprises)
IF OBJECT_ID('EnterpriseUserRoles', 'U') IS NOT NULL
BEGIN
    DELETE FROM EnterpriseUserRoles;
    PRINT 'Deleted from EnterpriseUserRoles';
END

-- Now delete Enterprises (parent table)
PRINT '=== Step 3: Deleting parent table (Enterprises) ===';
DELETE FROM Enterprises;
PRINT 'Deleted from Enterprises';

PRINT '=== ✅ All Enterprises and Labels data deleted successfully! ===';
GO

-- =============================================
-- METHOD 2: Disable constraints temporarily (ALTERNATIVE)
-- =============================================
-- Uncomment below if Method 1 doesn't work
/*
PRINT '=== Step 1: Disabling foreign key constraints ===';
ALTER TABLE Labels NOCHECK CONSTRAINT FK_Labels_Enterprise;
GO

PRINT '=== Step 2: Deleting data ===';
DELETE FROM Labels;
DELETE FROM Enterprises;
GO

PRINT '=== Step 3: Re-enabling foreign key constraints ===';
ALTER TABLE Labels WITH CHECK CHECK CONSTRAINT FK_Labels_Enterprise;
GO

PRINT '=== ✅ All data deleted! ===';
GO
*/

-- =============================================
-- METHOD 3: Delete specific Enterprise and its Labels
-- =============================================
-- Use this if you want to delete a specific enterprise
/*
DECLARE @EnterpriseId INT = 1; -- Change this to your EnterpriseId

-- Delete Labels for this Enterprise first
DELETE FROM Labels WHERE EnterpriseId = @EnterpriseId;

-- Delete EnterpriseUserRoles for this Enterprise
IF OBJECT_ID('EnterpriseUserRoles', 'U') IS NOT NULL
    DELETE FROM EnterpriseUserRoles WHERE EnterpriseId = @EnterpriseId;

-- Delete the Enterprise
DELETE FROM Enterprises WHERE EnterpriseId = @EnterpriseId;

PRINT '=== ✅ Enterprise and related Labels deleted! ===';
*/




















