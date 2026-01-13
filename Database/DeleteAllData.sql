-- =============================================
-- DELETE ALL DATA AND RESET IDENTITY TO START FROM 1
-- =============================================
-- ⚠️ WARNING: This will delete ALL data from tables!
-- Table structure will remain intact
-- New data will start from ID = 1
-- Run this on TEST database only!

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
-- Delete ReleaseStores before Releases (foreign key constraint)
IF OBJECT_ID('ReleaseStores', 'U') IS NOT NULL
    DELETE FROM ReleaseStores;
DELETE FROM Files;
-- Delete TrackArtists before Tracks and Artists (foreign key constraints)
IF OBJECT_ID('TrackArtists', 'U') IS NOT NULL
    DELETE FROM TrackArtists;
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

-- Reference tables (only delete if they exist)
IF OBJECT_ID('FileTypes', 'U') IS NOT NULL
    DELETE FROM FileTypes;

IF OBJECT_ID('PlanTypes', 'U') IS NOT NULL
    DELETE FROM PlanTypes;

IF OBJECT_ID('QCStatuses', 'U') IS NOT NULL
    DELETE FROM QCStatuses;

PRINT '=== Step 3: Re-enabling foreign key constraints ===';
-- Re-enable constraints without checking (since all data is deleted, constraints are valid)
-- Use NOCHECK to avoid validation errors during re-enable
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH NOCHECK CHECK CONSTRAINT ALL';
GO

PRINT '=== Step 4: Resetting all identity columns to start from 1 ===';
-- Reset identity columns for all tables (new data will start from 1)
EXEC sp_MSforeachtable 'IF OBJECTPROPERTY(object_id(''?''), ''TableHasIdentity'') = 1 DBCC CHECKIDENT (''?'', RESEED, 0)';
GO

PRINT '=== ✅ All data deleted and identity columns reset! ===';
PRINT '=== Table structures preserved - new data will start from ID = 1 ===';
GO

