-- Fix Existing Bookings and Room Statuses
-- Run this script ONCE to fix your existing data

USE [Hospitality_DB];
GO

PRINT 'Starting data fix...';

-- Step 1: Fix NULL booking_status values
UPDATE Bookings 
SET booking_status = 'confirmed' 
WHERE booking_status IS NULL;

PRINT 'Step 1: Fixed NULL booking_status values';

-- Step 2: Update room statuses for future bookings (Reserved)
UPDATE r
SET r.room_status = 'Reserved'
FROM rooms r
INNER JOIN Booking_rooms br ON r.room_id = br.room_id
INNER JOIN Bookings b ON br.booking_id = b.booking_id
WHERE b.[check-in_date] > CAST(GETDATE() AS DATE)
    AND b.booking_status NOT IN ('cancelled', 'completed')
    AND r.room_status = 'Available';

PRINT 'Step 2: Updated rooms to Reserved for future bookings';

-- Step 3: Update room statuses for current bookings (Occupied)
UPDATE r
SET r.room_status = 'Occupied'
FROM rooms r
INNER JOIN Booking_rooms br ON r.room_id = br.room_id
INNER JOIN Bookings b ON br.booking_id = b.booking_id
WHERE b.[check-in_date] <= CAST(GETDATE() AS DATE)
    AND b.[check-out_date] > CAST(GETDATE() AS DATE)
    AND b.booking_status NOT IN ('cancelled', 'completed')
    AND r.room_status != 'Occupied';

PRINT 'Step 3: Updated rooms to Occupied for current bookings';

-- Step 4: Set rooms back to Available for past bookings
UPDATE r
SET r.room_status = 'Available'
FROM rooms r
WHERE r.room_id IN (
    SELECT DISTINCT br.room_id
    FROM Booking_rooms br
    INNER JOIN Bookings b ON br.booking_id = b.booking_id
    WHERE b.[check-out_date] <= CAST(GETDATE() AS DATE)
     AND b.booking_status NOT IN ('cancelled')
)
AND r.room_status IN ('Occupied', 'Reserved')
AND r.room_id NOT IN (
    SELECT DISTINCT br2.room_id
    FROM Booking_rooms br2
  INNER JOIN Bookings b2 ON br2.booking_id = b2.booking_id
    WHERE b2.[check-in_date] <= CAST(GETDATE() AS DATE)
        AND b2.[check-out_date] > CAST(GETDATE() AS DATE)
        AND b2.booking_status NOT IN ('cancelled', 'completed')
);

PRINT 'Step 4: Set rooms back to Available for completed bookings';

-- Step 5: Auto-complete past bookings
UPDATE Bookings
SET booking_status = 'completed'
WHERE [check-out_date] < CAST(GETDATE() AS DATE)
    AND booking_status NOT IN ('cancelled', 'completed');

PRINT 'Step 5: Auto-completed past bookings';

-- Verify the results
PRINT '';
PRINT 'Verification Results:';
PRINT '--------------------';

SELECT 
    booking_status,
    COUNT(*) as count
FROM Bookings
GROUP BY booking_status;

PRINT '';
PRINT 'Room Status Summary:';
SELECT 
    room_status,
    COUNT(*) as count
FROM rooms
GROUP BY room_status;

PRINT '';
PRINT 'Data fix completed successfully!';
