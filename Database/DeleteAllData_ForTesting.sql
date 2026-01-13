-- =============================================
-- DELETE ALL DATA FROM TUNEWAVE DATABASE (FOR TESTING)
-- =============================================
-- This script deletes all data from tables while preserving table structure
-- Run this on your TEST database only!
-- ⚠️ WARNING: This will delete ALL data permanently!

-- Disable all foreign key constraints
PRINT '=== Step 1: Disabling all foreign key constraints ===';
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

-- Delete data from all tables in reverse dependency order
PRINT '=== Step 2: Deleting data from all tables ===';

-- Child/dependent tables first
DELETE FROM TicketLogs;
DELETE FROM TicketParticipants;
DELETE FROM TicketMessages;
DELETE FROM SupportTickets;

DELETE FROM NotificationJobs;
DELETE FROM Notifications;

DELETE FROM RoyaltyMappingAudit;
DELETE FROM RoyaltyRows;
DELETE FROM RoyaltyStatements;

DELETE FROM PayoutTransactions;
DELETE FROM WalletBalances;
DELETE FROM LedgerEntries;

DELETE FROM InvoiceLines;
DELETE FROM Invoices;
DELETE FROM PlanChangeRequests;
DELETE FROM Subscriptions;

DELETE FROM SearchTokens;

DELETE FROM AuditLogs;

DELETE FROM Jobs;

DELETE FROM QCQueue;
DELETE FROM DeliveryPackages;

DELETE FROM ReleaseContributors;
DELETE FROM Files;
DELETE FROM Tracks;
DELETE FROM Releases;

-- Delete from Artists table (no separate claim/access request tables)
DELETE FROM Artists;

DELETE FROM UserLabelRoles;
DELETE FROM Labels;

DELETE FROM EnterpriseUserRoles;
DELETE FROM Enterprises;

DELETE FROM PasswordResetRequests;
DELETE FROM Settings;
DELETE FROM Users;

-- Any other tables that might exist
DELETE FROM FileTypes;
DELETE FROM PlanTypes;
DELETE FROM QCStatuses;

PRINT '=== Step 3: Re-enabling all foreign key constraints ===';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

-- Reset identity columns to start from 1
PRINT '=== Step 4: Resetting identity columns ===';
EXEC sp_MSforeachtable 'IF OBJECTPROPERTY(object_id(''?''), ''TableHasIdentity'') = 1 DBCC CHECKIDENT (''?'', RESEED, 0)';
GO

PRINT '=== ✅ All data deleted successfully! ===';
PRINT '=== Table structures preserved ===';
GO

-- Verify data is deleted (optional)
PRINT '=== Step 5: Verification - Counting remaining rows ===';
SELECT 
    t.name AS TableName,
    SUM(p.rows) AS [RowCount]
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
  AND t.is_ms_shipped = 0
GROUP BY t.name
HAVING SUM(p.rows) > 0
ORDER BY t.name;
GO

PRINT '=== If no results above, all tables are empty! ===';

