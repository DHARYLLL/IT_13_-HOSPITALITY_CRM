-- Fix Password Column Size for BCrypt Hashes
-- BCrypt hashes are always 60 characters, so we need at least that much space
-- This script increases the user_password column to accommodate hashed passwords

USE CRM;
GO

-- Check current column size
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users' 
  AND COLUMN_NAME = 'user_password';
GO

-- Alter the column to support BCrypt hashes (60 chars) with some buffer
-- Using NVARCHAR(255) to be safe and allow for future changes
ALTER TABLE users
ALTER COLUMN user_password NVARCHAR(255);
GO

-- Verify the change
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users' 
  AND COLUMN_NAME = 'user_password';
GO

PRINT 'Password column size updated successfully!';
PRINT 'The user_password column can now store BCrypt hashes (60 characters).';

