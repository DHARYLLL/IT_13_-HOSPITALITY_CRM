-- Loyalty Program Tables for InnSight Hospitality CRM
-- Run this script to create the loyalty program database tables

-- First, check if booking_status column exists in Bookings table, if not add it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Bookings]') AND name = 'booking_status')
BEGIN
    ALTER TABLE Bookings ADD booking_status VARCHAR(50) DEFAULT 'pending';
    PRINT 'booking_status column added to Bookings table';
END
ELSE
BEGIN
    PRINT 'booking_status column already exists in Bookings table';
END
GO

-- Create LoyaltyPrograms table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyPrograms')
BEGIN
    CREATE TABLE LoyaltyPrograms (
        loyalty_id INT IDENTITY(1,1) PRIMARY KEY,
        client_id INT NOT NULL,
    total_points INT DEFAULT 0,
      current_tier VARCHAR(50) DEFAULT 'Bronze',
        member_since DATETIME DEFAULT GETDATE(),
        lifetime_stays INT DEFAULT 0,
    lifetime_spend DECIMAL(18,2) DEFAULT 0,
        last_stay_date DATETIME NULL,
   next_tier_expiry DATETIME NULL,
        FOREIGN KEY (client_id) REFERENCES Clients(client_id) ON DELETE CASCADE
    );
    
    PRINT 'LoyaltyPrograms table created successfully';
END
ELSE
BEGIN
    PRINT 'LoyaltyPrograms table already exists';
END
GO

-- Create LoyaltyRewards table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyRewards')
BEGIN
    CREATE TABLE LoyaltyRewards (
     reward_id INT IDENTITY(1,1) PRIMARY KEY,
      reward_name VARCHAR(200) NOT NULL,
        reward_description TEXT,
        points_required INT NOT NULL,
      reward_type VARCHAR(50) NOT NULL, -- voucher, upgrade, service
        is_active BIT DEFAULT 1,
        expiry_date DATETIME NULL,
        created_date DATETIME DEFAULT GETDATE()
    );
    
    PRINT 'LoyaltyRewards table created successfully';
END
ELSE
BEGIN
    PRINT 'LoyaltyRewards table already exists';
END
GO

-- Create LoyaltyTransactions table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyTransactions')
BEGIN
    CREATE TABLE LoyaltyTransactions (
   transaction_id INT IDENTITY(1,1) PRIMARY KEY,
  loyalty_id INT NOT NULL,
        points_earned INT DEFAULT 0,
        points_redeemed INT DEFAULT 0,
        transaction_type VARCHAR(20) NOT NULL, -- earn, redeem
 description VARCHAR(500),
        transaction_date DATETIME DEFAULT GETDATE(),
    booking_id INT NULL,
        FOREIGN KEY (loyalty_id) REFERENCES LoyaltyPrograms(loyalty_id) ON DELETE CASCADE,
        FOREIGN KEY (booking_id) REFERENCES Bookings(booking_id) ON DELETE SET NULL
    );
    
    PRINT 'LoyaltyTransactions table created successfully';
END
ELSE
BEGIN
    PRINT 'LoyaltyTransactions table already exists';
END
GO

-- Create RedeemedRewards table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RedeemedRewards')
BEGIN
    CREATE TABLE RedeemedRewards (
        redeemed_id INT IDENTITY(1,1) PRIMARY KEY,
        loyalty_id INT NOT NULL,
        reward_id INT NOT NULL,
        redemption_date DATETIME DEFAULT GETDATE(),
        status VARCHAR(20) DEFAULT 'active',
        used_date DATETIME NULL,
        booking_id INT NULL,
        expiry_date DATETIME NULL,
        voucher_code VARCHAR(100) NULL,
        notes TEXT NULL,
        sync_status NVARCHAR(20) DEFAULT 'synced',
        last_modified DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (loyalty_id) REFERENCES LoyaltyPrograms(loyalty_id) ON DELETE CASCADE,
        FOREIGN KEY (reward_id) REFERENCES LoyaltyRewards(reward_id) ON DELETE CASCADE,
        FOREIGN KEY (booking_id) REFERENCES Bookings(booking_id) ON DELETE SET NULL
    );
    
    PRINT 'RedeemedRewards table created successfully';
END
ELSE
BEGIN
    PRINT 'RedeemedRewards table already exists';
    
    -- Add sync columns if they don't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RedeemedRewards') AND name = 'sync_status')
    BEGIN
        ALTER TABLE RedeemedRewards ADD sync_status NVARCHAR(20) DEFAULT 'synced';
        PRINT 'Added sync_status column to RedeemedRewards table';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('RedeemedRewards') AND name = 'last_modified')
    BEGIN
        ALTER TABLE RedeemedRewards ADD last_modified DATETIME DEFAULT GETDATE();
        PRINT 'Added last_modified column to RedeemedRewards table';
    END
END
GO

-- Insert sample rewards
IF NOT EXISTS (SELECT * FROM LoyaltyRewards)
BEGIN
    INSERT INTO LoyaltyRewards (reward_name, reward_description, points_required, reward_type, is_active)
    VALUES 
    ('Free Breakfast for 2', 'Enjoy a complimentary breakfast buffet for two guests during your next stay', 2000, 'service', 1),
    ('Room Upgrade Voucher', 'Upgrade to the next room category (subject to availability)', 3500, 'upgrade', 1),
    ('$50 Stay Credit', 'Get $50 off your next booking at any InnSight property', 5000, 'voucher', 1),
    ('Late Checkout Pass', 'Enjoy late checkout until 4:00 PM on your next stay', 1500, 'service', 1),
    ('Welcome Package', 'Receive a welcome package with premium amenities and treats', 2500, 'service', 1),
    ('Spa Treatment Voucher', 'Complimentary 60-minute massage or spa treatment', 8000, 'voucher', 1),
    ('Airport Transfer', 'Free round-trip airport transfer service', 6000, 'service', 1),
    ('$100 Dining Credit', 'Use at any of our in-house restaurants', 10000, 'voucher', 1);
  
    PRINT 'Sample rewards inserted successfully';
END
GO

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LoyaltyPrograms_ClientId')
BEGIN
    CREATE INDEX IX_LoyaltyPrograms_ClientId ON LoyaltyPrograms(client_id);
    PRINT 'Index created on LoyaltyPrograms.client_id';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LoyaltyTransactions_LoyaltyId')
BEGIN
    CREATE INDEX IX_LoyaltyTransactions_LoyaltyId ON LoyaltyTransactions(loyalty_id);
    PRINT 'Index created on LoyaltyTransactions.loyalty_id';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LoyaltyTransactions_BookingId')
BEGIN
    CREATE INDEX IX_LoyaltyTransactions_BookingId ON LoyaltyTransactions(booking_id);
 PRINT 'Index created on LoyaltyTransactions.booking_id';
END
GO

PRINT 'Loyalty Program database setup completed!';
