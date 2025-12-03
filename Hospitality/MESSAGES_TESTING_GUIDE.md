# Messages System - Quick Testing Guide

## Prerequisites
1. Database with client records
2. InnSight application running
3. Logged in as a client user

## Step-by-Step Testing

### 1. Database Setup (First Time Only)
```sql
-- Run this in SQL Server Management Studio
-- Open and execute: Hospitality/Database/MessagesSetup.sql

-- Verify tables created
SELECT * FROM Messages;

-- Check sample messages
SELECT TOP 5 * FROM Messages ORDER BY sent_date DESC;
```

### 2. Access Messages Page

**Option A - Via Navigation Menu:**
1. Log in as a client
2. Click "Messages" in the top navigation
3. URL should be: `/client/messages/{your-client-id}`

**Option B - Direct URL:**
Navigate to: `https://localhost:7XXX/client/messages/1`
(Replace 1 with your client_id)

### 3. Test Inbox Features

#### View All Messages
- Default view shows all messages
- Messages should display with:
  - Category icon and name
  - Subject line
  - Preview text
  - Date/time
  - Unread indicator (blue dot)

#### Filter Messages
Click each filter tab to test:
- **All** - Shows all messages
- **Unread (3)** - Shows only unread messages
- **Stays** - Shows Harbourkey Waterfront messages
- **Offers** - Shows promotional messages

#### Read a Message
1. Click any message in the inbox
2. Message detail should appear on the right
3. Unread dot should disappear
4. Message should be marked as read in database

### 4. Test Message Detail View

#### Features to Test:
- [ ] Message displays correctly
- [ ] Category badge shows at top
- [ ] Action buttons visible:
  - Mark as read/unread toggle
  - Reply button
  - More options (?)
- [ ] If message has action button (e.g., "Book a stay"):
  - [ ] Button displays
  - [ ] Clicking navigates to correct page
- [ ] Offer details section (for offer messages)
- [ ] Footer notes display

### 5. Test Email to Hotel Team

#### Send an Email:
1. Click "Email the hotel team" button
2. Fill in the form:
   - **To:** Front desk (Harbourkey Waterfront) - readonly
   - **Regarding:** Select from dropdown
   - **Subject:** Enter test subject
   - **Message:** Enter test message body
3. Click "Send email to hotel"
4. Email should be saved as outgoing message
5. Form should close, returning to inbox

#### Verify Email Sent:
```sql
-- Check outgoing messages in database
SELECT * FROM Messages 
WHERE message_type = 'outgoing' 
ORDER BY sent_date DESC;
```

### 6. Test Responsive Design

#### Desktop (1024px+):
- [ ] Two-column layout visible
- [ ] Inbox on left, detail on right
- [ ] Both panels visible simultaneously

#### Tablet (768px-1024px):
- [ ] Single column layout
- [ ] Detail view overlays inbox
- [ ] Back button appears in detail view

#### Mobile (<768px):
- [ ] Mobile-optimized layout
- [ ] Touch-friendly buttons
- [ ] Proper spacing and typography

## Expected Results

### Inbox List
```
?? Messages & notifications
Review updates about your stays, offers...

[All] [Unread (3)] [Stays] [Offers]

Showing messages from the last 90 days

???????????????????????????????????????
? ? MEMBERSHIP REWARDS     2 days ago?
? Exclusive Gold member offer         ?
? Save 8% on weekend stays with... ?
?     ? ?
???????????????????????????????????????
? ?? HARBOURKEY WATERFRONT  Yesterday ?
? Booking confirmation         ?
? Your stay has been confirmed...   ?
?              ? ?
???????????????????????????????????????
```

### Message Detail
```
???????????????????????????????????????????
? ? Membership Rewards  [??] [??] [?]   ?
???????????????????????????????????????????
?        ?
? Exclusive Gold member offer  ?
? 2 days ago • Regarding: Loyalty Program ?
?       ?
? Hi Alex,    ?
?             ?
? As a valued Gold member, you have       ?
? access to an exclusive offer...         ?
?         ?
? [?? Book a stay] [?? Reply via email] ?
? ?
? Offer details          ?
? • Valid for Saturday–Sunday check-ins   ?
? • Minimum stay: 2 nights      ?
? • Subject to availability               ?
???????????????????????????????????????????
```

## Common Test Scenarios

### Scenario 1: New Client Registration
```csharp
// After client signup, they should see welcome message
Expected: "Welcome to InnSight!" message in inbox
```

### Scenario 2: Booking Confirmation
```csharp
// After completing a booking, client receives confirmation
Expected: "Booking Confirmation" message with booking details
```

### Scenario 3: Loyalty Points Earned
```csharp
// After checkout, client sees points earned notification
Expected: "Points Earned" message with point amount
```

### Scenario 4: Special Offer
```csharp
// Marketing sends exclusive offer to Gold members
Expected: "Exclusive Gold member offer" with action button
```

## Database Queries for Testing

### Check Messages for Client
```sql
SELECT 
    m.message_id,
    m.message_subject,
    m.message_type,
    m.message_category,
 m.is_read,
    m.sent_date
FROM Messages m
WHERE m.client_id = 1  -- Replace with your client_id
ORDER BY m.sent_date DESC;
```

### Count Unread Messages
```sql
SELECT COUNT(*) as unread_count
FROM Messages
WHERE client_id = 1 AND is_read = 0;
```

### Get Recent Messages by Category
```sql
SELECT message_category, COUNT(*) as count
FROM Messages
WHERE client_id = 1
GROUP BY message_category;
```

### Insert Test Message
```sql
DECLARE @ClientId INT = 1; -- Your client_id

INSERT INTO Messages (
    client_id, message_subject, message_body, 
    message_type, message_category, is_read, 
    sent_date, action_label, action_url
)
VALUES (
    @ClientId, 
  'Test Message', 
'This is a test message to verify the system works correctly.',
    'general',
    'Member Services',
    0,
  GETDATE(),
  'View Dashboard',
    '/client/profile/' + CAST(@ClientId AS VARCHAR(10))
);
```

## Troubleshooting

### Issue: "No messages" displayed
**Fix:** 
```sql
-- Check if client exists
SELECT * FROM Clients WHERE client_id = 1;

-- Insert sample message
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category)
VALUES (1, 'Test', 'Test message', 'general', 'Member Services');
```

### Issue: Unread count not showing
**Fix:**
```sql
-- Check unread messages
SELECT * FROM Messages WHERE client_id = 1 AND is_read = 0;

-- Mark message as unread for testing
UPDATE Messages SET is_read = 0 WHERE message_id = 1;
```

### Issue: Email form not sending
**Check:**
1. MessageService is registered in DI
2. Database connection is working
3. Client_id is valid
4. Browser console for errors

## Performance Testing

### Load Test
```sql
-- Insert 100 test messages
DECLARE @Counter INT = 1;
WHILE @Counter <= 100
BEGIN
    INSERT INTO Messages (
        client_id, message_subject, message_body,
        message_type, message_category, is_read, sent_date
    )
  VALUES (
 1, 
 'Test Message ' + CAST(@Counter AS VARCHAR(10)),
        'This is test message number ' + CAST(@Counter AS VARCHAR(10)),
        'general',
 'Member Services',
        CASE WHEN @Counter % 3 = 0 THEN 1 ELSE 0 END,
        DATEADD(day, -@Counter, GETDATE())
    );
    SET @Counter = @Counter + 1;
END
```

### Check Query Performance
```sql
-- Should use indexes
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT * FROM Messages 
WHERE client_id = 1 
AND is_read = 0
ORDER BY sent_date DESC;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
```

## Validation Checklist

Before marking complete, verify:

### Functionality
- [ ] Messages load correctly
- [ ] Filters work (All, Unread, Stays, Offers)
- [ ] Mark as read/unread works
- [ ] Email form submits successfully
- [ ] Navigation links work
- [ ] Action buttons function correctly
- [ ] No console errors

### UI/UX
- [ ] Responsive on all screen sizes
- [ ] Smooth transitions and animations
- [ ] Proper loading states
- [ ] Empty states display correctly
- [ ] Error messages are clear
- [ ] Accessibility (keyboard navigation)

### Data
- [ ] Messages persist in database
- [ ] Unread count updates correctly
- [ ] Filters return correct results
- [ ] Email saves as outgoing message
- [ ] Foreign keys work (client_id, booking_id)

### Performance
- [ ] Page loads quickly (<2 seconds)
- [ ] No lag when scrolling messages
- [ ] Database queries are optimized
- [ ] Indexes are used effectively

## Next Steps

After successful testing:

1. **Integration:**
   - Connect to booking system
   - Connect to loyalty system
   - Connect to payment system

2. **Enhancement:**
   - Add push notifications
   - Implement email service
   - Add message search
   - Create admin panel for sending messages

3. **Production:**
   - Configure production database
   - Set up email server
   - Enable SSL/TLS
   - Configure rate limiting

## Support

If you encounter issues:
1. Check browser console for errors
2. Verify database connectivity
3. Review `MESSAGES_SYSTEM_README.md` for detailed documentation
4. Check SQL Server logs for database errors

---

**Happy Testing! ??**
