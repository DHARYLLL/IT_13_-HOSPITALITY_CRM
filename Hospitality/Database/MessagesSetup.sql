-- Messages & Notifications Tables for InnSight Hospitality CRM
-- Run this script to create the messages and notifications database tables

-- Create Messages table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Messages')
BEGIN
    CREATE TABLE Messages (
        message_id INT IDENTITY(1,1) PRIMARY KEY,
        client_id INT NOT NULL,
    message_subject VARCHAR(500),
        message_body TEXT,
  message_type VARCHAR(50), -- 'offer', 'service', 'billing', 'general', 'outgoing'
  is_read BIT DEFAULT 0,
        sent_date DATETIME DEFAULT GETDATE(),
  booking_id INT NULL,
    action_label VARCHAR(200) NULL,
      action_url VARCHAR(500) NULL,
        regarding_text VARCHAR(500) NULL,
        FOREIGN KEY (client_id) REFERENCES Clients(client_id) ON DELETE CASCADE,
        FOREIGN KEY (booking_id) REFERENCES Bookings(booking_id) ON DELETE SET NULL
    );
    
    PRINT 'Messages table created successfully';
END
ELSE
BEGIN
    PRINT 'Messages table already exists';
    
    -- Drop message_category column if it exists
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Messages') AND name = 'message_category')
    BEGIN
     ALTER TABLE Messages DROP COLUMN message_category;
        PRINT 'Dropped message_category column';
    END
END
GO

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_ClientId')
BEGIN
    CREATE INDEX IX_Messages_ClientId ON Messages(client_id);
    PRINT 'Index created on Messages.client_id';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_IsRead')
BEGIN
    CREATE INDEX IX_Messages_IsRead ON Messages(is_read);
    PRINT 'Index created on Messages.is_read';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_SentDate')
BEGIN
    CREATE INDEX IX_Messages_SentDate ON Messages(sent_date DESC);
    PRINT 'Index created on Messages.sent_date';
END
GO

-- Insert sample messages for testing
DECLARE @SampleClientId INT;
SELECT TOP 1 @SampleClientId = client_id FROM Clients ORDER BY client_id;

IF @SampleClientId IS NOT NULL AND NOT EXISTS (SELECT * FROM Messages WHERE client_id = @SampleClientId)
BEGIN
    -- Insert welcome message
    INSERT INTO Messages (client_id, message_subject, message_body, message_type, is_read, sent_date)
    VALUES 
    (@SampleClientId, 'Welcome to InnSight!', 
     'Thank you for joining InnSight. We are excited to have you as a member. Start earning points with every stay!', 
     'general', 0, DATEADD(day, -5, GETDATE()));

    -- Insert exclusive offer message
    INSERT INTO Messages (client_id, message_subject, message_body, message_type, is_read, sent_date, action_label, action_url)
    VALUES 
    (@SampleClientId, 'Exclusive Gold member offer', 
     'Hi, As a valued Gold member, you have access to an exclusive offer on upcoming weekend stays. Save 8% on weekend stays, +2x loyalty points on your stay, Flexible cancellation up to 24 hours before check-in.', 
     'offer', 0, DATEADD(day, -2, GETDATE()), 'Book a stay', '/booking/new');

    -- Insert booking confirmation message
    INSERT INTO Messages (client_id, message_subject, message_body, message_type, is_read, sent_date, regarding_text)
    VALUES 
    (@SampleClientId, 'Booking Confirmation - Deluxe Suite', 
     'Your booking has been confirmed! Check-in: Tomorrow at 3:00 PM. We look forward to welcoming you.', 
     'service', 1, DATEADD(hour, -18, GETDATE()), 'Regarding: Upcoming stay');

    -- Insert thank you message
    INSERT INTO Messages (client_id, message_subject, message_body, message_type, is_read, sent_date)
    VALUES 
    (@SampleClientId, 'Thank you for staying with us', 
     'We hope you enjoyed your stay. You earned 1,248 points from this visit!', 
     'service', 1, DATEADD(day, -15, GETDATE()));

    -- Insert billing receipt message
    INSERT INTO Messages (client_id, message_subject, message_body, message_type, is_read, sent_date)
    VALUES 
    (@SampleClientId, 'Receipt available for your stay', 
     'Your receipt for booking #H0000012 is now available for download. Total amount: $1,248.50', 
     'billing', 1, DATEADD(day, -14, GETDATE()));

    PRINT 'Sample messages inserted successfully for client_id: ' + CAST(@SampleClientId AS VARCHAR(10));
END
ELSE IF @SampleClientId IS NULL
BEGIN
    PRINT 'No clients found in database. Please add clients first.';
END
ELSE
BEGIN
    PRINT 'Sample messages already exist for this client.';
END
GO

PRINT 'Messages & Notifications database setup completed!';
