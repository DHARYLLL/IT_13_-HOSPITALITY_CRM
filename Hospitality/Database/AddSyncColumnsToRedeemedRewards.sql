-- Add sync_status and last_modified columns to RedeemedRewards table
-- Run this on your ONLINE database (db32979)

USE db32979;
GO

-- Add sync_status column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RedeemedRewards') AND name = 'sync_status')
BEGIN
    ALTER TABLE RedeemedRewards ADD sync_status NVARCHAR(20) DEFAULT 'synced';
    PRINT 'Added sync_status column to RedeemedRewards table';
END
ELSE
BEGIN
    PRINT 'sync_status column already exists in RedeemedRewards table';
END
GO

-- Add last_modified column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RedeemedRewards') AND name = 'last_modified')
BEGIN
    ALTER TABLE RedeemedRewards ADD last_modified DATETIME DEFAULT GETDATE();
    PRINT 'Added last_modified column to RedeemedRewards table';
END
ELSE
BEGIN
    PRINT 'last_modified column already exists in RedeemedRewards table';
END
GO

-- Update existing records to have synced status
UPDATE RedeemedRewards 
SET sync_status = 'synced', 
    last_modified = GETDATE()
WHERE sync_status IS NULL;
GO

PRINT '';
PRINT 'Sync columns added to RedeemedRewards table successfully!';
PRINT 'The table now has sync_status and last_modified columns.';
GO

