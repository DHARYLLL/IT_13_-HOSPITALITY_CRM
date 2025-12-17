-- Create RedeemedRewards table if it doesn't exist
-- Run this script on BOTH local and online databases

USE CRM;
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

-- Create index for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RedeemedRewards_LoyaltyId')
BEGIN
    CREATE INDEX IX_RedeemedRewards_LoyaltyId ON RedeemedRewards(loyalty_id);
    PRINT 'Index created on RedeemedRewards.loyalty_id';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RedeemedRewards_RewardId')
BEGIN
    CREATE INDEX IX_RedeemedRewards_RewardId ON RedeemedRewards(reward_id);
    PRINT 'Index created on RedeemedRewards.reward_id';
END
GO

PRINT '';
PRINT 'RedeemedRewards table setup completed!';
PRINT 'Make sure to run this script on BOTH local and online databases.';
GO

