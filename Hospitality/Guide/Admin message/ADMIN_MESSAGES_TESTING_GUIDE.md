# Admin Messages - Quick Testing Guide

## ?? Quick Start

### Access Admin Messages
1. Navigate to: **http://localhost:xxxx/admin/messages**
2. Or click **?? MESSAGES** in admin sidebar

---

## ?? Testing Checklist

### ? Test 1: View Inbox
- [ ] Page loads without errors
- [ ] Filter tabs display (All, Unassigned, Open, Resolved)
- [ ] Conversation count badges show correctly
- [ ] Conversations list is populated

### ? Test 2: Client Sends Message
**From Client Dashboard** (`/clientprofile`):
1. Navigate to Messages section
2. Click "?? Email" button
3. Fill form:
   - **Regarding**: Front desk (HarborKey Waterfront)
   - **Subject**: Early check-in request
   - **Message**: Hi, I'll be arriving at 8 AM. Can I check in early?
4. Click "Send Email to Hotel Team"

**Expected Result**:
- Message appears in admin inbox
- Shows in "Unassigned" tab (count increases)
- Shows in "Open" tab (count increases)
- Conversation card displays client name and preview

### ? Test 3: Select Conversation
**In Admin Messages**:
1. Click on conversation card
2. Full thread loads on right panel
3. Client avatar and name display
4. All messages in thread show
5. Client messages have white background
6. Reply form appears at bottom

### ? Test 4: Reply to Client
1. Select reply category: **"Front Desk"**
2. Add context: *"Guest arriving early, checking room availability"*
3. Write message:
   ```
   Hello! Yes, we can accommodate an early check-in at 8 AM. 
   There will be a $30 early check-in fee. Your room will be ready.
   Looking forward to welcoming you!
   ```
4. Click **"?? Send Reply"**

**Expected Result**:
- Success message: "Reply sent successfully!"
- New message appears in thread (blue background)
- Reply form clears
- Client can see reply in their Messages section

### ? Test 5: Mark as Resolved
1. Click **"? Resolve"** button
2. Conversation closes
3. Unread count decreases
4. Find conversation in "Resolved" filter

### ? Test 6: Search Functionality
1. Enter client name in search box: "Alex"
2. Results filter in real-time
3. Clear search to see all again
4. Search by booking ID: "12345"

### ? Test 7: Filter Tabs
1. Click **"Unassigned"** - see only new messages
2. Click **"Open"** - see active conversations
3. Click **"Resolved"** - see closed conversations
4. Click **"All"** - see everything

---

## ?? Sample Test Data

### Create Test Messages (Run in SQL Server)

```sql
-- Get a test client ID
DECLARE @ClientId INT;
SELECT TOP 1 @ClientId = client_id FROM Clients;

-- Insert test inquiry from client
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@ClientId, 'Early check-in request', 'Hi, I will be arriving at 8 AM on Tuesday. Is early check-in available?', 
 'outgoing', 'Front Desk', 0, GETDATE(), 'Regarding: Early check-in inquiry');

-- Insert another test message
INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, regarding_text)
VALUES 
(@ClientId, 'Question about airport pickup', 'Do you offer airport pickup service? My flight lands at 6 PM.', 
 'outgoing', 'Member Services', 0, DATEADD(hour, -2, GETDATE()), 'Regarding: Transportation services');

-- Insert booking-related message (if you have a booking)
DECLARE @BookingId INT;
SELECT TOP 1 @BookingId = booking_id FROM Bookings WHERE client_id = @ClientId;

INSERT INTO Messages (client_id, message_subject, message_body, message_type, message_category, is_read, sent_date, booking_id, regarding_text)
VALUES 
(@ClientId, 'Special request for booking', 'Could I request a room on a higher floor? Booking #' + CAST(@BookingId AS VARCHAR), 
 'outgoing', 'Front Desk', 0, DATEADD(hour, -4, GETDATE()), @BookingId, 'Regarding: Room preference for booking #' + CAST(@BookingId AS VARCHAR));
```

---

## ?? Expected UI Elements

### Conversations Panel (Left)
```
???????????????????????????????????????
? Client Messages      ?
? 12 open • 3 waiting   ?
???????????????????????????????????????
? [All 15] [Unassigned 4] [Open 8]   ?
? [Resolved]     ?
???????????????????????????????????????
? ?? Search by guest name...      ?
???????????????????????????????????????
? ??????????????????????????????? ?
? ? AJ Alex Johnson      5m  ?    ?
? ? Early check-in • Booking #12?    ?
? ? Hi, I'll be arriving at...  ? 2  ?
? ???????????????????????????????    ?
?        ?
? ???????????????????????????????    ?
? ? MS Maria Santos    2h   ?    ?
? ? Airport pickup question     ?    ?
? ? Do you offer airport...     ?    ?
? ???????????????????????????????  ?
???????????????????????????????????????
```

### Conversation Detail (Right)
```
???????????????????????????????????????????????
? ?? Alex Johnson      ? Resolve ?
?    alex@email.com • Booking #12345 ?
???????????????????????????????????????????????
?  ?
?  ????????????????????????????????????      ?
?  ? Alex Johnson    Jun 15, 2:30 PM  ?      ?
?  ? Re: Early check-in request       ?      ?
?  ?    ?      ?
?  ? Hi, I'll be arriving at 8 AM...  ?      ?
?  ???????????????????????????????????? ?
?               ?
?      ????????????????????????????????      ?
?  ? You (Admin)  Jun 15, 2:45 PM ?      ?
?      ? Re: Early check-in request   ?      ?
?      ?  ?      ?
?      ? Hello! Yes, we can...    ?   ?
?      ????????????????????????????????    ?
?        ?
???????????????????????????????????????????????
? Reply as: [Front Desk ?] ?
? Context: [Checking room availability...]   ?
? Message: [Your reply here...]         ?
?            [Clear] [?? Send Reply]  ?
???????????????????????????????????????????????
```

---

## ?? Troubleshooting

### Problem: No conversations appear
**Check**:
```sql
-- Verify messages exist
SELECT COUNT(*) FROM Messages;

-- Check for client messages (type = 'outgoing')
SELECT * FROM Messages WHERE message_type = 'outgoing';
```

### Problem: Client name shows as "Guest"
**Fix**: Ensure Users table has proper data:
```sql
SELECT c.client_id, u.user_fname, u.user_lname 
FROM Clients c 
LEFT JOIN Users u ON c.user_id = u.user_id;
```

### Problem: Reply doesn't send
**Check browser console** (F12) for errors
**Verify** client_id exists:
```sql
SELECT * FROM Clients WHERE client_id = [your_id];
```

---

## ?? Verification Queries

### Check Admin Inbox Data
```sql
-- View all messages with client info
SELECT 
    m.message_id,
    m.client_id,
    u.user_fname + ' ' + u.user_lname as client_name,
    m.message_subject,
    m.message_type,
    m.is_read,
    m.sent_date
FROM Messages m
LEFT JOIN Clients c ON m.client_id = c.client_id
LEFT JOIN Users u ON c.user_id = u.user_id
ORDER BY m.sent_date DESC;
```

### Check Unread Counts
```sql
-- Count unread client messages
SELECT 
    COUNT(*) as unread_count
FROM Messages
WHERE message_type = 'outgoing' 
AND is_read = 0;
```

### Check Conversation Thread
```sql
-- View full conversation for specific client
DECLARE @ClientId INT = 1; -- Replace with actual client_id

SELECT 
    message_id,
    message_subject,
    message_body,
    message_type,
    is_read,
  sent_date
FROM Messages
WHERE client_id = @ClientId
ORDER BY sent_date ASC;
```

---

## ? Success Indicators

When everything is working correctly, you should see:

? **Visual Indicators**:
- Badge dots show unread counts
- Status badges appear ("Waiting for reply")
- Timestamps format correctly ("5m ago", "2h ago")
- Client avatars display initials
- Booking badges show when applicable

? **Functionality**:
- Filters update counts dynamically
- Search filters results in real-time
- Conversations load without delay
- Messages send successfully
- Notifications appear on client dashboard

? **Database**:
- Messages insert correctly
- is_read updates when viewed
- Relationships maintained (client_id, booking_id)
- Timestamps accurate

---

## ?? Demo Flow

### Complete User Story Test

**Act 1: Client Needs Help**
1. Login as client ? Navigate to Messages
2. Send: "I need to cancel my booking #12345"
3. Logout

**Act 2: Admin Responds**
1. Login as admin ? Go to Admin Messages
2. See new message in "Unassigned" (count: 1)
3. Click conversation ? Read message
4. Reply: "I'll help you with that. Let me check our cancellation policy..."
5. Send reply

**Act 3: Continued Conversation**
1. Logout admin
2. Login as client ? Check Messages
3. See admin reply with timestamp
4. Send follow-up: "Thank you! What about refund?"
5. Logout

**Act 4: Resolution**
1. Login as admin ? Messages
2. See new message from client (unread count updated)
3. Read and reply with refund policy
4. Mark conversation as "Resolved"
5. Verify conversation moves to "Resolved" tab

**Expected Timeline**:
```
Client message     ? Admin inbox (Unassigned)
Admin reads        ? Auto-marked as read
Admin replies      ? Client inbox (unread)
Client reads       ? Client's is_read = true
Client replies  ? Admin inbox (Unassigned again)
Admin resolves     ? Resolved tab
```

---

## ?? Mobile/Responsive Check

Test on different screen sizes:
- Desktop (1920x1080): Two-panel layout
- Tablet (768x1024): Single panel with toggle
- Mobile (375x667): Stacked layout

---

## ?? Integration Points

### With Bookings System
```csharp
// When replying about a booking
var message = new Message {
  client_id = clientId,
    booking_id = 12345, // Links to booking
    message_body = "Your booking is confirmed"
};
```

### With Loyalty System
```csharp
// When informing about points
var message = new Message {
    client_id = clientId,
    message_category = "Membership Rewards",
    message_body = "You earned 500 points!"
};
```

---

## ?? Performance Benchmarks

Expected load times:
- Initial inbox load: < 2 seconds
- Conversation select: < 1 second
- Send reply: < 1 second
- Search results: Instant (< 100ms)

---

## ?? Training Checklist for Staff

Train admin staff on:
- [ ] Accessing the messages page
- [ ] Understanding filter categories
- [ ] Selecting and reading conversations
- [ ] Choosing appropriate reply categories
- [ ] Writing professional responses
- [ ] Marking conversations as resolved
- [ ] Using search to find specific guests
- [ ] Handling booking-related inquiries

---

## ?? Go-Live Checklist

Before production:
- [ ] Database indexes created
- [ ] Sample messages tested
- [ ] All filters working
- [ ] Search functionality verified
- [ ] Reply system tested
- [ ] Client-side updates confirmed
- [ ] Error handling validated
- [ ] CSS loads correctly
- [ ] Mobile responsive
- [ ] Admin access restricted

---

## ?? Support

**If you encounter issues**:
1. Check browser console (F12)
2. Review database connection
3. Verify all tables exist
4. Check foreign key constraints
5. Review ADMIN_MESSAGES_COMPLETE_GUIDE.md

**Common Solutions**:
- Clear browser cache
- Restart application
- Check SQL Server connection
- Verify user authentication
- Review appsettings.json connection string

---

## ?? Success!

If all tests pass, your admin messaging system is ready to use!

**Next Steps**:
1. Train admin staff
2. Monitor initial usage
3. Gather feedback
4. Consider adding templates
5. Plan for real-time notifications (SignalR)

---

*Last Updated: [Current Date]*
*Version: 1.0*
