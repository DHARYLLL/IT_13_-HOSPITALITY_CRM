-- Check pending bookings and their dependencies
-- Run this on your LOCAL database (CRM)

USE CRM;
GO

-- Check pending bookings
PRINT '========================================';
PRINT 'Pending Bookings:';
PRINT '========================================';
SELECT 
    booking_id,
    client_id,
    [check-in_date],
    [check-out_date],
    booking_status,
    sync_status,
    last_modified
FROM Bookings
WHERE sync_status = 'pending'
ORDER BY booking_id;
GO

-- Check if clients exist for pending bookings
PRINT '';
PRINT '========================================';
PRINT 'Client Status for Pending Bookings:';
PRINT '========================================';
SELECT DISTINCT
    b.booking_id,
    b.client_id,
    c.client_id AS client_exists,
    c.sync_status AS client_sync_status,
    u.user_id,
    u.sync_status AS user_sync_status
FROM Bookings b
LEFT JOIN Clients c ON b.client_id = c.client_id
LEFT JOIN Users u ON c.user_id = u.user_id
WHERE b.sync_status = 'pending'
ORDER BY b.booking_id;
GO

-- Summary
PRINT '';
PRINT '========================================';
PRINT 'Summary:';
PRINT '========================================';
SELECT 
    COUNT(*) AS total_pending_bookings,
    COUNT(DISTINCT client_id) AS unique_clients
FROM Bookings
WHERE sync_status = 'pending';
GO

