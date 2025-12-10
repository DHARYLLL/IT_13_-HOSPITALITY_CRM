-- Fix Sync Status for Existing Rooms
-- Run this on your LOCAL database to mark existing rooms as needing sync

USE CRM;
GO

-- First, set all NULL sync_status to 'pending' so they get synced
UPDATE rooms 
SET sync_status = 'pending', 
  last_modified = GETDATE()
WHERE sync_status IS NULL;

PRINT 'Updated rooms with NULL sync_status to pending';

-- Show current status
SELECT room_id, room_name, sync_status, last_modified 
FROM rooms 
ORDER BY room_id;

-- Also add these rooms to the SyncQueue if not already there
INSERT INTO SyncQueue (entity_type, entity_id, change_type, table_name, sync_status)
SELECT 'Room', room_id, 'INSERT', 'rooms', 'pending'
FROM rooms r
WHERE NOT EXISTS (
    SELECT 1 FROM SyncQueue sq 
    WHERE sq.entity_type = 'Room' 
    AND sq.entity_id = r.room_id 
    AND sq.sync_status = 'pending'
);

PRINT 'Added rooms to SyncQueue';

-- Show SyncQueue
SELECT * FROM SyncQueue WHERE entity_type = 'Room' ORDER BY created_at DESC;
GO
