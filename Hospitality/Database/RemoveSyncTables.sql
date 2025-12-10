-- Remove SyncQueue and SyncStatus Tables
-- Run this on your LOCAL database to clean up unused sync tracking tables
-- The system now uses table-level sync_status columns instead

USE CRM;
GO

PRINT '';
PRINT '========================================';
PRINT ' Removing Legacy Sync Tables';
PRINT '========================================';
PRINT '';

-- Drop SyncQueue table if it exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SyncQueue')
BEGIN
    DROP TABLE SyncQueue;
    PRINT '? Dropped SyncQueue table';
END
ELSE
BEGIN
    PRINT '?? SyncQueue table does not exist';
END

-- Drop SyncStatus table if it exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SyncStatus')
BEGIN
    DROP TABLE SyncStatus;
    PRINT '? Dropped SyncStatus table';
END
ELSE
BEGIN
    PRINT '?? SyncStatus table does not exist';
END

PRINT '';
PRINT '========================================';
PRINT ' Cleanup Complete!';
PRINT '========================================';
PRINT '';
PRINT 'The system now uses simplified table-level sync tracking:';
PRINT '  - Each table has sync_status column (pending/synced)';
PRINT '  - Each table has last_modified column';
PRINT '';
PRINT 'No SyncQueue or SyncStatus tables needed!';
PRINT '';

-- Show which tables have sync tracking enabled
PRINT 'Tables with sync tracking:';
PRINT '';

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('rooms') AND name = 'sync_status')
    PRINT '  ? rooms';
    
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Bookings') AND name = 'sync_status')
    PRINT '  ? Bookings';
    
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'sync_status')
    PRINT '  ? Payments';
    
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Messages') AND name = 'sync_status')
    PRINT '  ? Messages';

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyPrograms') AND name = 'sync_status')
    PRINT '  ? LoyaltyPrograms';
    
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyTransactions') AND name = 'sync_status')
    PRINT '  ? LoyaltyTransactions';
    
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'sync_status')
    PRINT '  ? Users';
    
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'sync_status')
    PRINT '  ? Clients';

PRINT '';
GO
