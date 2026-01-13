-- =============================================
-- QUICK DELETE ALL DATA - Disables Constraints
-- =============================================
-- ⚠️ WARNING: This will delete ALL data from ALL tables!
-- This method disables all foreign key constraints temporarily
-- Use this if you want to delete everything quickly

PRINT '=== Step 1: Disabling ALL foreign key constraints ===';
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

PRINT '=== Step 2: Deleting all data from tables ===';

-- Delete in any order since constraints are disabled
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
DELETE FROM ReleaseStores;
DELETE FROM Files;
DELETE FROM TrackArtists;
DELETE FROM Tracks;
DELETE FROM Releases;
DELETE FROM Artists;
DELETE FROM UserLabelRoles;
DELETE FROM Labels;
DELETE FROM EnterpriseUserRoles;
DELETE FROM Enterprises;
DELETE FROM PasswordResetRequests;
DELETE FROM Settings;
DELETE FROM Users;

PRINT '=== Step 3: Re-enabling ALL foreign key constraints ===';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO

PRINT '=== Step 4: Resetting identity columns ===';
EXEC sp_MSforeachtable 'IF OBJECTPROPERTY(object_id(''?''), ''TableHasIdentity'') = 1 DBCC CHECKIDENT (''?'', RESEED, 0)';
GO

PRINT '=== ✅ All data deleted and constraints re-enabled! ===';
GO




















