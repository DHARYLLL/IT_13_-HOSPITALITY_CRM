-- Create/Update Rooms Table on Online Database (MonsterASP)
-- Run this on your ONLINE database (db32979)

-- Check if rooms table exists, if not create it
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rooms')
BEGIN
    CREATE TABLE rooms (
        room_id INT IDENTITY(1,1) PRIMARY KEY,
        room_name NVARCHAR(100) NOT NULL,
        room_number INT NOT NULL,
        room_floor INT NOT NULL,
      room_price DECIMAL(10,2) NOT NULL,
        room_status NVARCHAR(50) NOT NULL DEFAULT 'Available',
        room_picture VARBINARY(MAX) NULL,
        room_amenities NVARCHAR(MAX) NULL,
        room_occupancy INT NULL
    );
    PRINT '? Created rooms table';
END
ELSE
BEGIN
    PRINT '? rooms table already exists';
END
GO

-- Allow IDENTITY INSERT so we can sync with same IDs
-- This is needed for the MERGE statement to work correctly
SET IDENTITY_INSERT rooms ON;
GO

-- Verify table structure
SELECT 
    c.name AS column_name,
    t.name AS data_type,
    c.max_length,
    c.is_nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('rooms')
ORDER BY c.column_id;
GO
