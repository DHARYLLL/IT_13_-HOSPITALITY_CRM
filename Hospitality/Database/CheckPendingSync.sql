-- Check Pending Sync Records
-- Run this on your LOCAL database (CRM) to see what's pending sync

PRINT '========================================';
PRINT '  PENDING SYNC DIAGNOSTIC REPORT';
PRINT '========================================';
PRINT '';

-- 1. Check pending Users
PRINT '=== PENDING USERS ===';
SELECT user_id, role_id, user_fname, user_lname, user_email, sync_status
FROM Users 
WHERE sync_status = 'pending';

PRINT '';

-- 2. Check pending Clients
PRINT '=== PENDING CLIENTS ===';
SELECT client_id, user_id, sync_status
FROM Clients 
WHERE sync_status = 'pending';

PRINT '';

-- 3. Check pending Bookings
PRINT '=== PENDING BOOKINGS ===';
SELECT booking_id, client_id, [check-in_date], [check-out_date], booking_status, sync_status
FROM Bookings 
WHERE sync_status = 'pending';

PRINT '';

-- 4. Check pending Payments
PRINT '=== PENDING PAYMENTS ===';
SELECT payment_id, booking_id, amount, payment_status, sync_status
FROM Payments 
WHERE sync_status = 'pending';

PRINT '';

-- 5. Check pending Messages
PRINT '=== PENDING MESSAGES ===';
SELECT message_id, client_id, message_subject, sync_status
FROM Messages 
WHERE sync_status = 'pending';

PRINT '';

-- 6. Check pending Rooms
PRINT '=== PENDING ROOMS ===';
SELECT room_id, room_name, room_number, room_status, sync_status
FROM rooms 
WHERE sync_status = 'pending';

PRINT '';

-- 7. Summary count
PRINT '=== SUMMARY ===';
SELECT 'Users' as TableName, COUNT(*) as PendingCount FROM Users WHERE sync_status = 'pending'
UNION ALL
SELECT 'Clients', COUNT(*) FROM Clients WHERE sync_status = 'pending'
UNION ALL
SELECT 'Bookings', COUNT(*) FROM Bookings WHERE sync_status = 'pending'
UNION ALL
SELECT 'Payments', COUNT(*) FROM Payments WHERE sync_status = 'pending'
UNION ALL
SELECT 'Messages', COUNT(*) FROM Messages WHERE sync_status = 'pending'
UNION ALL
SELECT 'rooms', COUNT(*) FROM rooms WHERE sync_status = 'pending';

PRINT '';
PRINT '========================================';
PRINT '  Check if foreign keys exist online';
PRINT '========================================';

-- Check if the clients referenced by pending bookings exist
PRINT '';
PRINT '=== BOOKINGS WITH MISSING CLIENTS (potential FK issue) ===';
SELECT b.booking_id, b.client_id, 
       CASE WHEN c.client_id IS NULL THEN 'CLIENT MISSING!' ELSE 'OK' END as ClientStatus
FROM Bookings b
LEFT JOIN Clients c ON b.client_id = c.client_id
WHERE b.sync_status = 'pending';

-- Check if users referenced by pending clients exist
PRINT '';
PRINT '=== CLIENTS WITH MISSING USERS (potential FK issue) ===';
SELECT c.client_id, c.user_id,
       CASE WHEN u.user_id IS NULL THEN 'USER MISSING!' ELSE 'OK' END as UserStatus
FROM Clients c
LEFT JOIN Users u ON c.user_id = u.user_id
WHERE c.sync_status = 'pending';
