-- Manually retry sync for pending bookings
-- This script will help identify and fix stuck pending bookings
-- Run this on your LOCAL database (CRM)

USE CRM;
GO

PRINT '========================================';
PRINT 'Current Pending Bookings Status:';
PRINT '========================================';
SELECT 
    booking_id,
    client_id,
    booking_status,
    sync_status,
    last_modified,
    DATEDIFF(MINUTE, last_modified, GETDATE()) AS minutes_since_last_update
FROM Bookings
WHERE sync_status = 'pending'
ORDER BY booking_id;
GO

-- Option 1: Force update last_modified to trigger sync
-- This ensures they'll be picked up in the next sync cycle
PRINT '';
PRINT '========================================';
PRINT 'Updating last_modified to trigger sync...';
PRINT '========================================';

UPDATE Bookings
SET last_modified = GETDATE()
WHERE sync_status = 'pending';

PRINT '✅ Updated last_modified for all pending bookings';
PRINT 'They should be picked up in the next sync cycle';
GO

-- Option 2: Check if bookings exist in online database
-- If they exist but are marked as pending locally, there might be a sync issue
PRINT '';
PRINT '========================================';
PRINT 'Next Steps:';
PRINT '========================================';
PRINT '1. Check your application logs for sync errors';
PRINT '2. Manually trigger sync from admin dashboard';
PRINT '3. Wait for the next automatic sync cycle (every 15 seconds)';
PRINT '4. If still pending, check online database to see if they exist there';
PRINT '';
PRINT 'If bookings are already in online database but marked pending locally,';
PRINT 'you can mark them as synced using the script below:';
GO

-- Uncomment the following section ONLY if you've verified the bookings
-- are already in the online database and you want to mark them as synced:
/*
PRINT '';
PRINT '========================================';
PRINT 'Marking as synced (ONLY if already in online DB):';
PRINT '========================================';

UPDATE Bookings
SET sync_status = 'synced',
    last_modified = GETDATE()
WHERE sync_status = 'pending'
  AND booking_id IN (1001, 1002, 1003); -- Replace with actual booking IDs

PRINT '✅ Marked bookings as synced';
*/
GO

