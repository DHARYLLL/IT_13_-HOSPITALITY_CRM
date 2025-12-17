-- Sync LoyaltyRewards from local to online database
-- Run this on your ONLINE database (db32979)
-- This will insert all 8 rewards with the same IDs as your local database

USE db32979;
GO

-- Enable IDENTITY_INSERT to allow inserting specific reward_id values
SET IDENTITY_INSERT LoyaltyRewards ON;
GO

-- Insert all 8 rewards (matching your local database)
IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 1)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (1, 'Free Breakfast for 2', 'Enjoy a complimentary breakfast buffet for two guests during your next stay', 2000, 'service', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 1: Free Breakfast for 2';
END
ELSE
BEGIN
    PRINT 'Reward_id 1 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 2)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (2, 'Room Upgrade Voucher', 'Upgrade to the next room category (subject to availability)', 3500, 'upgrade', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 2: Room Upgrade Voucher';
END
ELSE
BEGIN
    PRINT 'Reward_id 2 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 3)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (3, '$50 Stay Credit', 'Get $50 off your next booking at any InnSight property', 5000, 'voucher', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 3: $50 Stay Credit';
END
ELSE
BEGIN
    PRINT 'Reward_id 3 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 4)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (4, 'Late Checkout Pass', 'Enjoy late checkout until 4:00 PM on your next stay', 1500, 'service', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 4: Late Checkout Pass';
END
ELSE
BEGIN
    PRINT 'Reward_id 4 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 5)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (5, 'Welcome Package', 'Receive a welcome package with premium amenities and treats', 2500, 'service', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 5: Welcome Package';
END
ELSE
BEGIN
    PRINT 'Reward_id 5 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 6)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (6, 'Spa Treatment Voucher', 'Complimentary 60-minute massage or spa treatment', 8000, 'voucher', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 6: Spa Treatment Voucher';
END
ELSE
BEGIN
    PRINT 'Reward_id 6 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 7)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (7, 'Airport Transfer', 'Free round-trip airport transfer service', 6000, 'service', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 7: Airport Transfer';
END
ELSE
BEGIN
    PRINT 'Reward_id 7 already exists';
END
GO

IF NOT EXISTS (SELECT * FROM LoyaltyRewards WHERE reward_id = 8)
BEGIN
    INSERT INTO LoyaltyRewards (reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date, created_date)
    VALUES (8, '$100 Dining Credit', 'Use at any of our in-house restaurants', 10000, 'voucher', 1, NULL, '2025-12-10 20:20:52.210');
    PRINT 'Inserted reward_id 8: $100 Dining Credit';
END
ELSE
BEGIN
    PRINT 'Reward_id 8 already exists';
END
GO

-- Disable IDENTITY_INSERT
SET IDENTITY_INSERT LoyaltyRewards OFF;
GO

PRINT '';
PRINT '========================================';
PRINT 'LoyaltyRewards sync completed!';
PRINT 'All 8 rewards have been inserted into the online database.';
PRINT 'Redeemed rewards should now sync successfully.';
PRINT '========================================';
GO

