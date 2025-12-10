 -- Sync Tracking Tables for Offline-First Architecture
-- Run this on your LOCAL database to track pending changes

USE CRM;
GO

-- Table to track pending changes that need to sync to online database
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SyncQueue')
BEGIN
    CREATE TABLE SyncQueue (
        sync_id INT IDENTITY(1,1) PRIMARY KEY,
        entity_type NVARCHAR(50) NOT NULL,          -- 'Booking', 'Payment', 'Message', etc.
        entity_id INT NOT NULL,          -- The ID of the record
        change_type NVARCHAR(20) NOT NULL,          -- 'INSERT', 'UPDATE', 'DELETE'
        table_name NVARCHAR(100) NOT NULL,  -- The actual table name
        change_data NVARCHAR(MAX) NULL,      -- JSON of the changed data
        created_at DATETIME NOT NULL DEFAULT GETDATE(),
     retry_count INT NOT NULL DEFAULT 0,
        last_retry_at DATETIME NULL,
      sync_status NVARCHAR(20) NOT NULL DEFAULT 'pending', -- 'pending', 'syncing', 'completed', 'failed'
    error_message NVARCHAR(MAX) NULL
    );

    CREATE INDEX IX_SyncQueue_Status ON SyncQueue(sync_status);
    CREATE INDEX IX_SyncQueue_EntityType ON SyncQueue(entity_type);
    CREATE INDEX IX_SyncQueue_CreatedAt ON SyncQueue(created_at);

    PRINT '? Created SyncQueue table';
END
ELSE
BEGIN
    PRINT '? SyncQueue table already exists';
END
GO

-- Table to track last sync timestamps for each table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SyncStatus')
BEGIN
    CREATE TABLE SyncStatus (
   table_name NVARCHAR(100) PRIMARY KEY,
 last_sync_time DATETIME NULL,
    last_sync_direction NVARCHAR(10) NULL,      -- 'push' or 'pull'
        records_synced INT NULL DEFAULT 0,
        sync_errors INT NULL DEFAULT 0
    );

    -- Initialize with main tables
    INSERT INTO SyncStatus (table_name, last_sync_time) VALUES
        ('Users', NULL),
  ('Clients', NULL),
 ('Bookings', NULL),
 ('rooms', NULL),
        ('Payments', NULL),
        ('Messages', NULL),
('LoyaltyPrograms', NULL),
        ('LoyaltyTransactions', NULL);

    PRINT '? Created SyncStatus table';
END
ELSE
BEGIN
    PRINT '? SyncStatus table already exists';
END
GO

-- Add sync tracking columns to main tables if they don't exist
-- These columns help track which records need syncing

-- Bookings table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Bookings') AND name = 'sync_status')
BEGIN
    ALTER TABLE Bookings ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Bookings ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Bookings table';
END
GO

-- Payments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'sync_status')
BEGIN
    ALTER TABLE Payments ADD sync_status NVARCHAR(20) DEFAULT 'synced';
  ALTER TABLE Payments ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Payments table';
END
GO

-- Messages table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Messages') AND name = 'sync_status')
BEGIN
    ALTER TABLE Messages ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Messages ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Messages table';
END
GO

-- rooms table (IMPORTANT - was missing!)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('rooms') AND name = 'sync_status')
BEGIN
    ALTER TABLE rooms ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE rooms ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to rooms table';
END
GO

-- LoyaltyPrograms table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyPrograms') AND name = 'sync_status')
BEGIN
    ALTER TABLE LoyaltyPrograms ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE LoyaltyPrograms ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to LoyaltyPrograms table';
END
GO

-- LoyaltyTransactions table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyTransactions') AND name = 'sync_status')
BEGIN
    ALTER TABLE LoyaltyTransactions ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE LoyaltyTransactions ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to LoyaltyTransactions table';
END
GO

-- Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'sync_status')
BEGIN
    ALTER TABLE Users ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Users ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Users table';
END
GO

-- Clients table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'sync_status')
BEGIN
    ALTER TABLE Clients ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE Clients ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '? Added sync columns to Clients table';
END
GO

PRINT '';
PRINT '========================================';
PRINT '? Sync tracking tables setup complete!';
PRINT '========================================';
PRINT '';
PRINT 'Tables created/verified:';
PRINT '  - SyncQueue (tracks pending changes)';
PRINT '  - SyncStatus (tracks last sync times)';
PRINT '';
PRINT 'Sync columns added to:';
PRINT '  - Bookings';
PRINT '  - Payments';
PRINT '  - Messages';
PRINT '  - rooms';
PRINT '  - LoyaltyPrograms';
PRINT '  - LoyaltyTransactions';
PRINT '  - Users';
PRINT '  - Clients';
GO
