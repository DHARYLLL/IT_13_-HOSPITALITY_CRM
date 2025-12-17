-- Mark existing LoyaltyRewards as synced in local database
-- Run this on your LOCAL database (CRM)
-- Since the rewards are already in the online database, mark them as synced

USE CRM;
GO

-- First, ensure sync_status column exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LoyaltyRewards') AND name = 'sync_status')
BEGIN
    ALTER TABLE LoyaltyRewards ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    ALTER TABLE LoyaltyRewards ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT '✅ Added sync columns to LoyaltyRewards table';
END
ELSE
BEGIN
    PRINT '✅ LoyaltyRewards table already has sync columns';
END
GO

-- Update all existing rewards to 'synced' status
UPDATE LoyaltyRewards 
SET sync_status = 'synced',
    last_modified = GETDATE()
WHERE sync_status IS NULL OR sync_status = 'pending';

PRINT '';
PRINT '========================================';
PRINT '✅ Updated all existing LoyaltyRewards to synced status';
PRINT '========================================';
PRINT '';
PRINT 'All rewards are now marked as synced.';
PRINT 'New rewards you create will automatically sync to online database.';
GO

