-- Comprehensive script to check and fix pending bookings
-- Run this on your LOCAL database (CRM)

USE CRM;
GO

PRINT '========================================';
PRINT 'Step 1: Check Pending Bookings';
PRINT '========================================';
SELECT 
    booking_id,
    client_id,
    booking_status,
    sync_status,
    last_modified
FROM Bookings
WHERE sync_status = 'pending'
ORDER BY booking_id;
GO

PRINT '';
PRINT '========================================';
PRINT 'Step 2: Update last_modified to trigger sync';
PRINT '========================================';
UPDATE Bookings
SET last_modified = GETDATE()
WHERE sync_status = 'pending';

PRINT '✅ Updated last_modified timestamp';
PRINT '';
PRINT 'Next: Check your application console/logs for sync errors';
PRINT 'Then manually trigger sync from admin dashboard';
GO

-- If you want to check if bookings are already in online database,
-- run this query on your ONLINE database (db32979):
/*
USE db32979;
GO

SELECT 
    booking_id,
    client_id,
    booking_status
FROM Bookings
WHERE booking_id IN (1001, 1002, 1003);
GO
*/

-- If the bookings ARE already in the online database,
-- uncomment the following to mark them as synced locally:
/*
USE CRM;
GO

UPDATE Bookings
SET sync_status = 'synced',
    last_modified = GETDATE()
WHERE sync_status = 'pending'
  AND booking_id IN (1001, 1002, 1003);

PRINT '✅ Marked bookings as synced';
GO
*/

