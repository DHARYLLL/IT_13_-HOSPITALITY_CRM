# Admin Messages System - Complete Guide

## Overview
The Admin Messages system allows administrators to view, manage, and reply to all client messages from a centralized inbox interface. This feature enables efficient communication between hotel staff and guests.

## Database Structure

### Messages Table (Already exists in your CRM database)
```sql
CREATE TABLE Messages (
    message_id INT IDENTITY(1,1) PRIMARY KEY,
    client_id INT NOT NULL,
    message_subject VARCHAR(500),
    message_body TEXT,
    message_type VARCHAR(50), -- 'offer', 'service', 'billing', 'general', 'outgoing'
    message_category VARCHAR(200), -- 'Member Services', 'Billing & Receipts', etc.
    is_read BIT DEFAULT 0,
    sent_date DATETIME DEFAULT GETDATE(),
    booking_id INT NULL,
 action_label VARCHAR(200) NULL,
    action_url VARCHAR(500) NULL,
    regarding_text VARCHAR(500) NULL,
    FOREIGN KEY (client_id) REFERENCES Clients(client_id) ON DELETE CASCADE,
    FOREIGN KEY (booking_id) REFERENCES Bookings(booking_id) ON DELETE SET NULL
);
```

## Message Types

### Client to Admin (message_type = 'outgoing')
- Messages sent by clients through their dashboard
- These appear in the admin inbox as unread until responded to
- Used to track client inquiries and requests

### Admin to Client (message_type = 'service', 'offer', etc.)
- Messages sent by admin as replies
- Appear in client's message inbox
- Can include action buttons and booking references

## Features Implemented

### 1. Conversation List (Left Panel)
- **Filter Tabs**: All, Unassigned, Open, Resolved
- **Search**: Search by guest name, email, or booking ID
- **Conversation Cards**: Show latest message, client info, unread count
- **Status Indicators**: Visual badges for waiting/unread messages
- **Real-time counts**: Display counts for each filter category

### 2. Conversation Detail (Right Panel)
- **Full Message Thread**: View complete conversation history
- **Client Information**: Name, email, booking reference
- **Message Bubbles**: Different styling for client vs admin messages
- **Timestamps**: Clear time indicators for each message
- **Booking Links**: Display associated booking numbers

### 3. Reply System
- **Category Selection**: Choose reply source (Member Services, Front Desk, etc.)
- **Context Field**: Add internal notes about the inquiry
- **Message Composition**: Rich text area for crafting replies
- **Send & Clear**: Easy to send or reset reply form
- **Success/Error Feedback**: Visual confirmation of actions

### 4. Admin Actions
- **Mark as Resolved**: Close conversations when handled
- **Auto-read**: Automatically mark client messages as read when viewed
- **Filter & Search**: Quickly find specific conversations

## Implementation Files

### 1. Backend Service
**File**: `Hospitality/Services/MessageService.cs`

New methods added:
```csharp
// Get all messages for admin with filtering
GetAllMessagesForAdminAsync(MessageFilter? filter)

// Get complete conversation thread
GetConversationAsync(int clientId)

// Send reply to client
ReplyToClientAsync(Message reply)

// Mark conversation as resolved
MarkConversationResolvedAsync(int clientId)
```

### 2. Frontend Page
**File**: `Hospitality/Components/Pages/AdminMessages.razor`

Key components:
- Conversation list with filtering
- Message thread display
- Reply composition form
- Real-time status updates

### 3. Styling
**File**: `Hospitality/wwwroot/css/admin-messages.css`

Modern inbox-style interface with:
- Two-panel layout (conversations + detail)
- Hover effects and transitions
- Status badges and indicators
- Responsive design

## How to Use

### For Administrators

#### 1. Access the Messages Page
Navigate to: `/admin/messages` or click **MESSAGES** in the admin sidebar

#### 2. View Conversations
- **All**: See all conversations
- **Unassigned**: View new messages needing attention (unread client messages)
- **Open**: See ongoing conversations
- **Resolved**: View closed conversations

#### 3. Search for Specific Messages
Use the search box to find conversations by:
- Guest name (e.g., "Alex Johnson")
- Email address
- Booking ID (e.g., "12345")

#### 4. Select a Conversation
Click on any conversation card to view the full message thread

#### 5. Reply to a Client
1. **Choose Category**: Select who the reply is from:
   - Member Services
   - Front Desk
   - Billing & Receipts
   - Management

2. **Add Context** (Optional): Internal note for tracking
   - Example: "Guest arriving early, check availability for 8 AM check-in"

3. **Write Message**: Compose your reply
   - Be clear and professional
   - Reference booking numbers if applicable
   - Provide actionable information

4. **Send**: Click "?? Send Reply"

#### 6. Mark as Resolved
When conversation is complete:
1. Click "? Resolve" button
2. Conversation moves to "Resolved" filter
3. All messages marked as read

### For Clients (How they send messages)

Clients can send messages from their dashboard using the existing `ClientMessages.razor` page:

1. Navigate to Messages section
2. Click "?? Email"
3. Fill in:
   - Regarding (topic)
   - Subject
   - Message body
4. Click "Send Email to Hotel Team"
5. Message appears in admin inbox as "Unassigned" and "Open"

## Message Flow

```
Client Dashboard ? Send Message ? Database (message_type: 'outgoing')
          ?
Admin Inbox ? View Conversation ? Appears in "Unassigned" / "Open"
       ?
Admin ? Read Message ? Auto-mark as read
       ?
Admin ? Reply ? Database (message_type: 'service')
  ?
Client Dashboard ? View Reply ? Notification badge updates
                ?
Admin ? Mark Resolved ? Conversation closed
```

## Database Queries Used

### Get All Messages for Admin
```sql
SELECT m.*, 
    COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name,
    u.user_email as client_email,
    b.booking_id as booking_reference
FROM Messages m
LEFT JOIN Clients c ON m.client_id = c.client_id
LEFT JOIN Users u ON c.user_id = u.user_id
LEFT JOIN Bookings b ON m.booking_id = b.booking_id
WHERE [filter conditions]
ORDER BY m.sent_date DESC
```

### Get Conversation Thread
```sql
SELECT m.*, 
    COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name,
    u.user_email as client_email
FROM Messages m
LEFT JOIN Clients c ON m.client_id = c.client_id
LEFT JOIN Users u ON c.user_id = u.user_id
WHERE m.client_id = @clientId
ORDER BY m.sent_date ASC
```

### Send Admin Reply
```sql
INSERT INTO Messages (
    client_id, message_subject, message_body, message_type, 
    message_category, is_read, sent_date, booking_id, 
    action_label, action_url, regarding_text
)
VALUES (
    @clientId, @subject, @body, 'service', 
    @category, 0, GETDATE(), @bookingId,
    @actionLabel, @actionUrl, @regarding
)
```

## Testing the System

### Test Scenario 1: New Client Inquiry
1. From client dashboard, send a message about early check-in
2. Check admin inbox - message should appear in "Unassigned" (4) and "Open" (8)
3. Click on conversation to view details
4. Reply with check-in time options
5. Check client dashboard - reply should appear

### Test Scenario 2: Booking-Related Question
1. Client sends message referencing booking #12345
2. Admin views conversation - booking badge should show
3. Admin replies with booking details
4. Mark conversation as resolved
5. Verify it moves to "Resolved" filter

### Test Scenario 3: Multiple Conversations
1. Have 3 different clients send messages
2. Admin views "All" - should see 3 conversations
3. Reply to one - count in "Open" should update
4. Search for specific client name
5. Filter results should update dynamically

## Customization Options

### Change Reply Categories
Edit `AdminMessages.razor`, line ~226:
```razor
<select @bind="replyCategory">
    <option value="Member Services">Member Services</option>
    <option value="Front Desk">Front Desk</option>
    <option value="Billing & Receipts">Billing & Receipts</option>
    <option value="Management">Management</option>
    <!-- Add more categories -->
</select>
```

### Modify Filter Logic
Edit `MessageService.cs`, `GetAllMessagesForAdminAsync()` method:
```csharp
if (filter.FilterType == "YourNewFilter")
{
    sql += " AND m.your_condition = 'value'";
}
```

### Adjust Message Styling
Edit `admin-messages.css`:
```css
.client-message {
    background: white; /* Change background */
    border: 1px solid #e5e9f0;
}

.admin-message {
    background: #dbeafe; /* Change admin message color */
}
```

## Integration with Existing Systems

### Link to Bookings
Messages automatically link to bookings when `booking_id` is provided:
```csharp
var message = new Message {
    client_id = 123,
    booking_id = 456, // Links to specific booking
    message_body = "Your booking is confirmed"
};
```

### Link to Loyalty System
When replying about loyalty points:
```csharp
var message = new Message {
    client_id = 123,
    message_category = "Membership Rewards",
    message_body = "You earned 500 points!"
};
```

## Security Considerations

### Access Control
Only authenticated admin users should access `/admin/messages`:
```csharp
// Add to AdminMessages.razor if needed
@attribute [Authorize(Roles = "Admin")]
```

### SQL Injection Prevention
All queries use parameterized commands:
```csharp
cmd.Parameters.AddWithValue("@clientId", clientId);
```

### Data Validation
Validate input before saving:
```csharp
if (string.IsNullOrWhiteSpace(replyMessage)) return;
```

## Performance Optimization

### Indexes
The following indexes are already created:
- `IX_Messages_ClientId` - Fast client lookups
- `IX_Messages_IsRead` - Quick unread filtering
- `IX_Messages_SentDate` - Efficient date sorting

### Lazy Loading
Conversations load on demand - only selected conversation's full details are fetched

### Pagination
For large datasets, consider adding pagination:
```csharp
// Add to GetAllMessagesForAdminAsync
sql += " OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
```

## Troubleshooting

### Issue: Messages not appearing in admin inbox
**Solution**: Check that:
1. Message has `message_type = 'outgoing'` (client-sent)
2. Client exists in Clients table
3. User is linked to client via `user_id`

### Issue: Reply not sending
**Solution**: Verify:
1. Client ID is valid
2. Message body is not empty
3. Database connection is active
4. No foreign key constraint violations

### Issue: Unread count not updating
**Solution**: 
1. Check `MarkAsReadAsync()` is being called
2. Verify `is_read` column is updated
3. Refresh the page to reload counts

## Future Enhancements

### Potential Improvements
1. **Real-time Updates**: Use SignalR for live message notifications
2. **File Attachments**: Allow admins to attach documents
3. **Templates**: Pre-written reply templates for common scenarios
4. **Assignments**: Assign conversations to specific staff members
5. **Priority Flags**: Mark urgent messages
6. **Analytics**: Track response times and satisfaction

### Example Template System
```csharp
var templates = new Dictionary<string, string> {
    ["early_checkin"] = "Thank you for contacting us. Early check-in...",
    ["late_checkout"] = "We can accommodate late checkout...",
    ["booking_change"] = "I'll be happy to help modify your booking..."
};
```

## Support

For issues or questions:
1. Check database connection in `DbConnection.cs`
2. Review error logs in console
3. Verify all referenced tables exist
4. Ensure foreign keys are properly configured

## Summary

? **What's Working**:
- Complete admin inbox interface
- Conversation threading and grouping
- Filter and search functionality
- Reply system with categorization
- Automatic read status management
- Integration with existing database

? **Key Benefits**:
- Centralized client communication
- Professional inbox interface
- Easy conversation tracking
- Quick response capabilities
- Booking integration
- Search and filtering

? **Files Modified/Created**:
1. `Services/MessageService.cs` - Added 4 new methods
2. `Components/Pages/AdminMessages.razor` - Complete admin UI
3. `wwwroot/css/admin-messages.css` - Modern styling

The admin messaging system is now fully functional and ready to use! ??
