-- Diagnose Sync Issues
-- Run this on BOTH databases to compare column names

-- 1. Check Bookings table columns
PRINT '=== BOOKINGS TABLE COLUMNS ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
  CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Bookings'
ORDER BY ORDINAL_POSITION;

-- 2. Check for pending records in local database
PRINT '';
PRINT '=== PENDING BOOKINGS (run on local CRM database) ===';
SELECT 
    booking_id,
    client_id,
    [check-in_date],
    [check-out_date],
    person_count,
    booking_status,
    sync_status
FROM Bookings
WHERE sync_status = 'pending';

-- 3. Check if client_id exists in online database
PRINT '';
PRINT '=== CHECK CLIENT EXISTS (run on both) ===';
SELECT client_id, user_id FROM Clients;

-- 4. Sample insert test (don't run, just for reference)
PRINT '';
PRINT '=== SAMPLE INSERT FOR TESTING ===';
PRINT 'If the above shows the columns, try this insert manually:';
PRINT 'INSERT INTO Bookings (booking_id, client_id, [check-in_date], [check-out_date], person_count, booking_status)';
PRINT 'VALUES (999, 1, ''2025-01-01'', ''2025-01-02'', 2, ''test'');';
PRINT 'DELETE FROM Bookings WHERE booking_id = 999;';
