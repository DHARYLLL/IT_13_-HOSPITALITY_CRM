-- Admin Messages Test Data Setup
-- Run this script to create sample conversations for testing the admin messages feature

USE CRM;
GO

-- First, let's verify we have the Messages table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Messages')
BEGIN
    PRINT 'ERROR: Messages table does not exist. Please run MessagesSetup.sql first.';
    RETURN;
END
GO

-- Get test client IDs (we'll use existing clients)
DECLARE @Client1 INT, @Client2 INT, @Client3 INT;
DECLARE @Booking1 INT, @Booking2 INT;

-- Get 3 different clients
SELECT TOP 1 @Client1 = client_id FROM Clients ORDER BY client_id;
SELECT TOP 1 @Client2 = client_id FROM Clients WHERE client_id > @Client1 ORDER BY client_id;
SELECT TOP 1 @Client3 = client_id FROM Clients WHERE client_id > @Client2 ORDER BY client_id;

-- Get some booking IDs for testing
SELECT TOP 1 @Booking1 = booking_id FROM Bookings WHERE client_id = @Client1 ORDER BY booking_id;
SELECT TOP 1 @Booking2 = booking_id FROM Bookings WHERE client_id = @Client2 ORDER BY booking_id;

PRINT '========================================';
PRINT 'Creating Test Conversations';
PRINT '========================================';
PRINT 'Client 1 ID: ' + CAST(@Client1 AS VARCHAR);
PRINT 'Client 2 ID: ' + CAST(@Client2 AS VARCHAR);
PRINT 'Client 3 ID: ' + CAST(@Client3 AS VARCHAR);
PRINT '';

-- Clear existing test messages (optional - comment out if you want to keep existing data)
-- DELETE FROM Messages WHERE message_subject LIKE '%TEST%' OR message_subject LIKE 'Early check-in%';
-- PRINT 'Cleared existing test messages';

-- ============================================
-- SCENARIO 1: Early Check-in Request
-- Client sends inquiry, admin hasn't responded yet
-- ============================================
PRINT 'Creating Scenario 1: Early Check-in Request (Unassigned)';

INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, booking_id, regarding_text)
VALUES 
(@Client1, 
 'Early check-in request', 
 'Hi! I''ll be arriving at the hotel around 8:00 AM on Tuesday. Is it possible to arrange an early check-in? I know the standard check-in time is 3:00 PM, but I have an important meeting at 10:00 AM and would like to freshen up first. Please let me know if this can be accommodated and if there are any additional fees. Thank you!',
 'outgoing', 
 'Front Desk', 
 0, -- Unread by admin
 DATEADD(MINUTE, -15, GETDATE()), 
 @Booking1,
 'Regarding: Early check-in for booking #' + CAST(@Booking1 AS VARCHAR));

PRINT '? Created unread client inquiry about early check-in';

-- ============================================
-- SCENARIO 2: Airport Pickup Service
-- Complete conversation with replies
-- ============================================
PRINT '';
PRINT 'Creating Scenario 2: Airport Pickup (Conversation Thread)';

-- Client's initial message
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client2,
 'Airport pickup service inquiry',
 'Hello, I''m arriving at the airport on Friday at 6:30 PM. Do you offer airport pickup service? If so, what are the rates and how do I arrange it? My flight is AA1234 from New York. Looking forward to my stay!',
 'outgoing',
 'Member Services',
 1, -- Already read by admin
 DATEADD(HOUR, -4, GETDATE()),
 'Regarding: Transportation and airport services');

PRINT '? Created client inquiry about airport pickup';

-- Admin's reply
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client2,
 'Re: Airport pickup service inquiry',
 'Hello! Thank you for reaching out. Yes, we do offer airport pickup service. The rate is $45 for a standard sedan (up to 3 passengers) or $65 for an SUV (up to 6 passengers). We can absolutely arrange a pickup for your 6:30 PM arrival on Friday. Please reply with your preferred vehicle type, and I''ll coordinate with our transportation team. We''ll track your flight AA1234 to adjust for any delays. Looking forward to welcoming you!',
 'service',
 'Member Services',
 0, -- Client hasn't read yet
 DATEADD(HOUR, -3, GETDATE()),
 'Regarding: Transportation and airport services');

PRINT '? Created admin reply with service details';

-- Client's follow-up
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client2,
 'Re: Airport pickup service inquiry',
 'Perfect! I''d like to book the standard sedan for $45. Is it okay if I have two large suitcases? Also, will the driver have a sign with my name at arrivals? Thanks so much!',
 'outgoing',
 'Member Services',
 0, -- Admin needs to respond
 DATEADD(HOUR, -2, GETDATE()),
 'Regarding: Transportation and airport services');

PRINT '? Created client follow-up question (needs admin response)';

-- ============================================
-- SCENARIO 3: Special Dietary Requirements
-- New unread message
-- ============================================
PRINT '';
PRINT 'Creating Scenario 3: Dietary Requirements (Unassigned)';

INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, booking_id, regarding_text)
VALUES 
(@Client3,
 'Special dietary requirements',
 'Hi there! I have a confirmed booking for next weekend and wanted to inform you that I have a severe peanut allergy. Could you please ensure that the room minibar and any complimentary items are peanut-free? Also, are your restaurant kitchen staff trained to handle food allergies? This is very important for my safety. Thank you for your understanding.',
 'outgoing',
 'Front Desk',
 0, -- Unread by admin
 DATEADD(MINUTE, -45, GETDATE()),
 @Booking2,
 'Regarding: Health and dietary requirements for upcoming stay');

PRINT '? Created urgent message about dietary requirements';

-- ============================================
-- SCENARIO 4: Billing Question (Resolved)
-- Complete and resolved conversation
-- ============================================
PRINT '';
PRINT 'Creating Scenario 4: Billing Question (Resolved)';

-- Client's question
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client1,
 'Question about receipt',
 'I stayed at your hotel last week and need a detailed receipt for my company reimbursement. The email receipt doesn''t break down the charges. Could you send me an itemized bill showing room rate, taxes, and any additional charges separately? My booking number was ' + CAST(@Booking1 AS VARCHAR) + '. Thanks!',
 'outgoing',
 'Billing & Receipts',
 1, -- Already read
 DATEADD(DAY, -2, GETDATE()),
 'Regarding: Receipt and billing documentation');

PRINT '? Created billing inquiry';

-- Admin's resolution
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, booking_id, regarding_text)
VALUES 
(@Client1,
 'Re: Question about receipt',
 'Hello! I''ve generated a detailed itemized receipt for your stay (booking #' + CAST(@Booking1 AS VARCHAR) + '). The breakdown is as follows:
 
Room Rate (3 nights): $450.00
Taxes (12%): $54.00
Resort Fee: $25.00
Parking: $15.00
-------------
Total: $544.00

I''ve sent the official itemized receipt to your email as a PDF attachment. It should arrive within the next few minutes. If you need any additional documentation or have questions about specific charges, please don''t hesitate to reach out. Have a great day!',
 'billing',
 'Billing & Receipts',
 1, -- Client has read it
 DATEADD(DAY, -2, DATEADD(HOUR, 2, GETDATE())),
 @Booking1,
 'Regarding: Receipt and billing documentation');

PRINT '? Created admin resolution with itemized details';

-- ============================================
-- SCENARIO 5: Room Upgrade Request
-- Multiple messages showing negotiation
-- ============================================
PRINT '';
PRINT 'Creating Scenario 5: Room Upgrade Request (Open)';

-- Initial request
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, booking_id, regarding_text)
VALUES 
(@Client2,
 'Room upgrade inquiry',
 'Hello! I''ve booked a Standard Room for my anniversary next month. I''d love to surprise my wife with an upgrade to a Deluxe Suite. What would be the price difference? Also, is it possible to have champagne and flowers in the room when we arrive? This is our 10th anniversary, so I want to make it special!',
 'outgoing',
 'Member Services',
 1,
 DATEADD(DAY, -1, GETDATE()),
 @Booking2,
 'Regarding: Room upgrade and special requests for anniversary');

PRINT '? Created room upgrade request';

-- Admin's offer
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, booking_id, regarding_text)
VALUES 
(@Client2,
 'Re: Room upgrade inquiry',
 'Congratulations on your 10th anniversary! ?? We''d be delighted to help make it memorable. The upgrade from Standard to Deluxe Suite would be $120/night ($240 total for 2 nights). As a special anniversary package, we can include:

? Bottle of champagne ($65 value)
? Rose bouquet ($45 value)
? Chocolate-covered strawberries ($35 value)
? Late checkout (2 PM instead of 11 AM)

Package price: $150 (that''s $145 in savings!)

Would you like to proceed with the upgrade and anniversary package?',
 'offer',
 'Member Services',
 0, -- Client needs to respond
 DATEADD(DAY, -1, DATEADD(HOUR, 3, GETDATE())),
 @Booking2,
 'Regarding: Room upgrade and special requests for anniversary');

PRINT '? Created admin offer with anniversary package';

-- ============================================
-- SCENARIO 6: Loyalty Program Question
-- ============================================
PRINT '';
PRINT 'Creating Scenario 6: Loyalty Points (Waiting for reply)';

INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client3,
 'Loyalty points not credited',
 'Hi, I completed my stay last week but I don''t see the loyalty points in my account yet. According to your website, I should have earned 500 points. My booking confirmation was #' + CAST(COALESCE(@Booking2, 99999) AS VARCHAR) + '. Can you please look into this? I''m hoping to use those points for my next booking. Thank you!',
 'outgoing',
 'Membership Rewards',
 0, -- Needs admin attention
 DATEADD(MINUTE, -90, GETDATE()),
 'Regarding: Loyalty points and rewards program');

PRINT '? Created loyalty program inquiry';

-- ============================================
-- SCENARIO 7: Positive Feedback (Low Priority)
-- ============================================
PRINT '';
PRINT 'Creating Scenario 7: Positive Feedback (Read)';

INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client1,
 'Thank you for wonderful stay!',
 'I just wanted to send a quick message to thank your entire team for the exceptional service during my recent stay. The front desk staff were incredibly helpful, the room was spotless, and the breakfast buffet was delicious. Special shoutout to Maria at the front desk who went above and beyond to help with my late checkout request. I''ve already recommended your hotel to several colleagues. Will definitely be back!',
 'outgoing',
 'Member Services',
 1, -- Admin has read
 DATEADD(DAY, -3, GETDATE()),
 'Regarding: General feedback and compliments');

PRINT '? Created positive feedback message';

-- Admin's thank you reply
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@Client1,
 'Re: Thank you for wonderful stay!',
 'Thank you so much for taking the time to share your wonderful feedback! We''re thrilled to hear you had such a positive experience. I''ve passed your kind words along to Maria and the entire team – they''ll be delighted to know their efforts made a difference. As a token of our appreciation, I''ve added 200 bonus loyalty points to your account. We look forward to welcoming you back soon! Safe travels!',
 'service',
 'Member Services',
 1,
 DATEADD(DAY, -3, DATEADD(HOUR, 5, GETDATE())),
 'Regarding: General feedback and compliments');

PRINT '? Created admin thank you reply';

-- ============================================
-- Summary Report
-- ============================================
PRINT '';
PRINT '========================================';
PRINT 'Test Data Creation Complete!';
PRINT '========================================';

PRINT '';
PRINT 'SUMMARY OF CREATED CONVERSATIONS:';
PRINT '-------------------';

-- Count messages by type
DECLARE @TotalMessages INT, @ClientMessages INT, @AdminMessages INT, @UnreadCount INT;

SELECT @TotalMessages = COUNT(*) FROM Messages;
SELECT @ClientMessages = COUNT(*) FROM Messages WHERE message_type = 'outgoing';
SELECT @AdminMessages = COUNT(*) FROM Messages WHERE message_type != 'outgoing';
SELECT @UnreadCount = COUNT(*) FROM Messages WHERE is_read = 0 AND message_type = 'outgoing';

PRINT 'Total Messages: ' + CAST(@TotalMessages AS VARCHAR);
PRINT 'Client Messages: ' + CAST(@ClientMessages AS VARCHAR);
PRINT 'Admin Replies: ' + CAST(@AdminMessages AS VARCHAR);
PRINT 'Unread (Needs Response): ' + CAST(@UnreadCount AS VARCHAR);
PRINT '';

-- List scenarios created
PRINT 'SCENARIOS CREATED:';
PRINT '1. ? Early check-in request (UNASSIGNED - needs immediate response)';
PRINT '2. ? Airport pickup conversation (OPEN - awaiting client confirmation)';
PRINT '3. ? Dietary requirements (UNASSIGNED - urgent, health-related)';
PRINT '4. ? Billing question (RESOLVED - complete)';
PRINT '5. ? Room upgrade request (OPEN - awaiting client decision)';
PRINT '6. ? Loyalty points issue (UNASSIGNED - needs investigation)';
PRINT '7. ? Positive feedback (RESOLVED - no action needed)';
PRINT '';

PRINT 'FILTER EXPECTATIONS:';
PRINT '• ALL Tab: ~13-15 messages';
PRINT '• UNASSIGNED Tab: 3-4 conversations (early check-in, dietary, loyalty)';
PRINT '• OPEN Tab: 5-6 conversations (all except resolved)';
PRINT '• RESOLVED Tab: 2 conversations (billing, feedback)';
PRINT '';

PRINT '========================================';
PRINT 'Next Steps:';
PRINT '========================================';
PRINT '1. Navigate to /admin/messages';
PRINT '2. Verify all scenarios appear in correct filter tabs';
PRINT '3. Click on "Unassigned" to see urgent messages';
PRINT '4. Test replying to the early check-in request';
PRINT '5. Search for specific clients by name';
PRINT '6. Mark a conversation as resolved';
PRINT '';
PRINT '?? Ready to test! Good luck!';
PRINT '';

-- View created messages
PRINT 'VIEW ALL CREATED TEST MESSAGES:';
SELECT 
    m.message_id,
    COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name,
    m.message_subject,
    m.message_type,
    CASE WHEN m.is_read = 0 THEN '? UNREAD' ELSE '? Read' END as read_status,
    m.sent_date,
    CASE 
        WHEN m.booking_id IS NOT NULL THEN 'Booking #' + CAST(m.booking_id AS VARCHAR)
        ELSE 'No booking'
    END as booking_ref
FROM Messages m
LEFT JOIN Clients c ON m.client_id = c.client_id
LEFT JOIN Users u ON c.user_id = u.user_id
ORDER BY m.sent_date DESC;

GO
