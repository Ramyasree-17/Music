-- =============================================
-- DELETE ALL DATA AND RESET IDENTITY TO START FROM 1
-- =============================================
-- ⚠️ WARNING: This will delete ALL data from tables!
-- Table structure will remain intact
-- New data will start from ID = 1

PRINT '=== Step 1: Disabling foreign key constraints ===';
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

PRINT '=== Step 2: Deleting all data from tables ===';

-- Delete child/dependent tables first
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
DELETE FROM Artists;

-- Label and Enterprise related
DELETE FROM UserLabelRoles;
DELETE FROM Labels;
DELETE FROM EnterpriseUserRoles;
DELETE FROM Enterprises;

-- User related
DELETE FROM PasswordResetRequests;
DELETE FROM Settings;
DELETE FROM Users;

-- Reference tables (optional - keep if you want to preserve reference data)
-- DELETE FROM FileTypes;
-- DELETE FROM PlanTypes;
-- DELETE FROM QCStatuses;

PRINT '=== Step 3: Re-enabling foreign key constraints ===';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

PRINT '=== Step 4: Resetting all identity columns to start from 1 ===';
-- Reset identity columns for all tables (new data will start from 1)
EXEC sp_MSforeachtable 'IF OBJECTPROPERTY(object_id(''?''), ''TableHasIdentity'') = 1 DBCC CHECKIDENT (''?'', RESEED, 0)';
GO

PRINT '=== ✅ All data deleted and identity columns reset! ===';
PRINT '=== Table structures preserved - new data will start from ID = 1 ===';
GO

