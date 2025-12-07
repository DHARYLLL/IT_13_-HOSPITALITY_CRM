-- Create Payments table to track multiple payments per booking
-- Run this script to set up the payments system

-- Create Payments table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Payments' AND xtype='U')
BEGIN
    CREATE TABLE Payments (
        payment_id INT IDENTITY(1,1) PRIMARY KEY,
        booking_id INT NOT NULL,
        amount DECIMAL(10,2) NOT NULL,
        payment_method NVARCHAR(50) NOT NULL, -- card, gcash, paymaya
        payment_status NVARCHAR(50) NOT NULL DEFAULT 'pending', -- pending, completed, failed, refunded
        payment_intent_id NVARCHAR(255) NULL, -- PayMongo reference
        checkout_session_id NVARCHAR(255) NULL,
   payment_date DATETIME NOT NULL DEFAULT GETDATE(),
        payment_type NVARCHAR(50) NOT NULL DEFAULT 'full', -- full, downpayment, partial, balance
        notes NVARCHAR(500) NULL,
        
    CONSTRAINT FK_Payments_Bookings FOREIGN KEY (booking_id) REFERENCES Bookings(booking_id)
    );
    
    PRINT 'Payments table created successfully';
END
ELSE
BEGIN
    PRINT 'Payments table already exists';
END

-- Create index for faster lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_BookingId')
BEGIN
    CREATE INDEX IX_Payments_BookingId ON Payments(booking_id);
    PRINT 'Index IX_Payments_BookingId created';
END

-- Add booking_status column to Bookings if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Bookings') AND name = 'booking_status')
BEGIN
 ALTER TABLE Bookings ADD booking_status NVARCHAR(50) NULL DEFAULT 'pending';
    PRINT 'Added booking_status column to Bookings table';
END

-- Update existing bookings to have a default status
UPDATE Bookings SET booking_status = 'confirmed' WHERE booking_status IS NULL;

-- Verify the setup
SELECT 
  'Payments' AS TableName,
    COUNT(*) AS RecordCount
FROM Payments
UNION ALL
SELECT 
    'Bookings with Status' AS TableName,
    COUNT(*) AS RecordCount  
FROM Bookings WHERE booking_status IS NOT NULL;

PRINT 'Payments system setup complete!';
PRINT 'Supported payment methods: card, gcash, paymaya';
PRINT 'Currency: PHP (Philippine Peso)';
