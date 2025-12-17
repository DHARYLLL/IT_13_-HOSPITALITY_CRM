-- Force sync pending bookings by resetting their sync status
-- This will mark them for sync again on the next sync cycle
-- Run this on your LOCAL database (CRM)

USE CRM;
GO

-- First, let's see what we're working with
PRINT '========================================';
PRINT 'Current Pending Bookings:';
PRINT '========================================';
SELECT 
    booking_id,
    client_id,
    booking_status,
    sync_status,
    last_modified
FROM Bookings
WHERE sync_status = 'pending';
GO

-- Update last_modified to trigger sync
-- This ensures they'll be picked up in the next sync cycle
UPDATE Bookings
SET last_modified = GETDATE()
WHERE sync_status = 'pending';

PRINT '';
PRINT '========================================';
PRINT '✅ Updated last_modified for pending bookings';
PRINT 'They will be synced on the next sync cycle';
PRINT '========================================';
GO

-- If you want to manually mark them for retry (in case of previous errors)
-- Uncomment the following:
/*
UPDATE Bookings
SET sync_status = 'pending',
    last_modified = GETDATE()
WHERE sync_status = 'pending';
*/
GO

