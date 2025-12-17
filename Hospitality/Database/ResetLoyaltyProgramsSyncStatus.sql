-- Reset sync_status for LoyaltyPrograms to force re-sync
-- Run this on your LOCAL database to mark loyalty programs as pending

USE CRM;
GO

-- Reset all loyalty programs to 'pending' so they get synced
UPDATE LoyaltyPrograms 
SET sync_status = 'pending', 
    last_modified = GETDATE()
WHERE sync_status = 'synced';

PRINT 'Reset sync_status for all LoyaltyPrograms to pending';
PRINT 'These will be synced in the next sync cycle';
GO

-- Verify the update
SELECT loyalty_id, client_id, current_tier, sync_status 
FROM LoyaltyPrograms;
GO

