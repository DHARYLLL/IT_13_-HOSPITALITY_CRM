-- Simplified Sync Tracking Setup
-- Run this on your LOCAL database to add sync tracking columns to tables
-- No SyncQueue or SyncStatus tables needed!

USE CRM;
GO

PRINT '';
PRINT '========================================';
PRINT ' Simplified Sync Tracking Setup';
PRINT '========================================';
PRINT '';
PRINT 'Adding sync_status and last_modified columns to tables...';
PRINT '';

-- Add sync tracking columns to main tables if they don't exist
-- These columns help track which records need syncing

-- Bookings table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Bookings') AND name = 'sync_status')
BEGIN
    ALTER TABLE Bookings ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Bookings ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Bookings table';
END
ELSE
BEGIN
    PRINT '?? Bookings table already has sync columns';
END
GO

-- Payments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'sync_status')
BEGIN
    ALTER TABLE Payments ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Payments ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Payments table';
END
ELSE
BEGIN
    PRINT '?? Payments table already has sync columns';
END
GO

-- Messages table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Messages') AND name = 'sync_status')
BEGIN
    ALTER TABLE Messages ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Messages ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Messages table';
END
ELSE
BEGIN
    PRINT '?? Messages table already has sync columns';
END
GO

-- rooms table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('rooms') AND name = 'sync_status')
BEGIN
    ALTER TABLE rooms ADD sync_status NVARCHAR(20) DEFAULT 'synced';
  ALTER TABLE rooms ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to rooms table';
END
ELSE
BEGIN
    PRINT '?? rooms table already has sync columns';
END
GO

-- LoyaltyPrograms table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyPrograms') AND name = 'sync_status')
BEGIN
    ALTER TABLE LoyaltyPrograms ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE LoyaltyPrograms ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to LoyaltyPrograms table';
END
ELSE
BEGIN
    PRINT '?? LoyaltyPrograms table already has sync columns';
END
GO

-- LoyaltyTransactions table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyTransactions') AND name = 'sync_status')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE LoyaltyTransactions ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to LoyaltyTransactions table';
END
ELSE
BEGIN
    PRINT '?? LoyaltyTransactions table already has sync columns';
END
GO

-- Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'sync_status')
BEGIN
    ALTER TABLE Users ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Users ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Users table';
END
ELSE
BEGIN
    PRINT '?? Users table already has sync columns';
END
GO

-- Clients table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'sync_status')
BEGIN
    ALTER TABLE Clients ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Clients ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Clients table';
END
ELSE
BEGIN
    PRINT '?? Clients table already has sync columns';
END
GO

-- LoyaltyRewards table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyRewards') AND name = 'sync_status')
BEGIN
    ALTER TABLE LoyaltyRewards ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE LoyaltyRewards ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to LoyaltyRewards table';
END
ELSE
BEGIN
    PRINT '?? LoyaltyRewards table already has sync columns';
END
GO

-- RedeemedRewards table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RedeemedRewards') AND name = 'sync_status')
BEGIN
    ALTER TABLE RedeemedRewards ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE RedeemedRewards ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to RedeemedRewards table';
END
ELSE
BEGIN
    PRINT '?? RedeemedRewards table already has sync columns';
END
GO

PRINT '';
PRINT '========================================';
PRINT '? Simplified sync tracking setup complete!';
PRINT '========================================';
PRINT '';
PRINT 'Sync columns added to:';
PRINT '  - Bookings';
PRINT '  - Payments';
PRINT '  - Messages';
PRINT '  - rooms';
PRINT '  - LoyaltyPrograms';
PRINT '  - LoyaltyTransactions';
PRINT '  - LoyaltyRewards';
PRINT '  - Users';
PRINT '  - Clients';
PRINT '  - RedeemedRewards';
PRINT '';
PRINT 'How it works:';
PRINT '  � sync_status = ''pending'' ? Needs sync to online DB';
PRINT '  � sync_status = ''synced'' ? Already synced';
PRINT '  � last_modified ? Timestamp of last change';
PRINT '';
PRINT 'No SyncQueue or SyncStatus tables needed!';
PRINT '';
GO
