# Messages System - Quick Reference Card

## ?? Quick Start

### 1. Database Setup
```sql
-- Execute once
USE [your_database];
GO
EXEC [path_to_script]\MessagesSetup.sql;
```

### 2. Access Page
```
URL: /client/messages/{clientId}
Example: /client/messages/1
```

### 3. Navigation Link
```razor
<a href="/client/messages/@clientId" class="nav-link">Messages</a>
```

## ?? Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ClientMessages.razor` | Components/Pages/ | Main messages page UI |
| `MessageService.cs` | Services/ | Business logic & data access |
| `Message.cs` | Models/ | Data models |
| `client-messages.css` | wwwroot/css/ | Styling |
| `MessagesSetup.sql` | Database/ | Database schema |

## ?? Common Operations

### Create Notification
```csharp
await MessageService.CreateMessageAsync(new Message
{
    client_id = clientId,
    message_subject = "Booking Confirmed",
    message_body = "Your booking has been confirmed...",
    message_type = "service",
    message_category = "Harbourkey Waterfront",
    booking_id = bookingId,
    action_label = "View Booking",
    action_url = $"/booking/confirmation/{bookingId}"
});
```

### Send Email to Hotel
```csharp
await MessageService.SendEmailToHotelAsync(new EmailRequest
{
    client_id = clientId,
    regarding = "Billing question",
    subject = "Invoice request",
    message_body = "Please send my invoice...",
    email_to = "billing@innsight.com"
});
```

### Get Messages with Filter
```csharp
var messages = await MessageService.GetClientMessagesAsync(
    clientId,
    new MessageFilter { FilterType = "Unread", TimeRange = "Last 90 days" }
);
```

### Mark as Read
```csharp
await MessageService.MarkAsReadAsync(messageId);
```

### Get Unread Count
```csharp
int unreadCount = await MessageService.GetUnreadCountAsync(clientId);
```

## ?? Message Types & Categories

### Message Types
- `offer` - Promotions and special deals
- `service` - Booking confirmations, reminders
- `billing` - Receipts, invoices
- `general` - Welcome messages, updates
- `outgoing` - Client-sent emails

### Message Categories
- `Member Services` ??
- `Billing & Receipts` ??
- `Harbourkey Waterfront` ??
- `Membership Rewards` ?
- `Email Sent` ??

## ?? CSS Classes

### Inbox
```css
.inbox-item   /* Message in list */
.inbox-item.unread       /* Unread message */
.inbox-item.selected     /* Selected message */
.unread-dot        /* Unread indicator */
.filter-tab.active       /* Active filter */
```

### Detail View
```css
.message-detail          /* Detail container */
.message-title/* Message heading */
.message-body      /* Message content */
.message-cta /* Action buttons */
.offer-details           /* Offer information */
```

### Email Form
```css
.email-form            /* Form container */
.form-input           /* Text input */
.form-textarea           /* Message area */
.btn-primary             /* Send button */
.btn-secondary           /* Cancel/draft */
```

## ?? Integration Points

### After Booking
```csharp
// In BookingService.CreateBookingAsync()
await MessageService.CreateMessageAsync(new Message {
    client_id = clientId,
    message_subject = "Booking Confirmation",
  message_body = $"Room: {roomName}, Check-in: {checkIn:MMM dd}",
    message_type = "service",
    message_category = "Harbourkey Waterfront",
    booking_id = bookingId
});
```

### After Payment
```csharp
// In PaymentService.ProcessPaymentAsync()
await MessageService.CreateMessageAsync(new Message {
client_id = clientId,
    message_subject = "Payment Received",
    message_body = $"Amount: ${amount:N2}, Booking: #{bookingId}",
    message_type = "billing",
    message_category = "Billing & Receipts",
    booking_id = bookingId
});
```

### After Points Earned
```csharp
// In LoyaltyService.AddPointsAsync()
await MessageService.CreateMessageAsync(new Message {
    client_id = clientId,
    message_subject = "Points Earned!",
    message_body = $"You've earned {points} points!",
    message_type = "service",
    message_category = "Membership Rewards"
});
```

### Marketing Offers
```csharp
// In MarketingService (create this)
await MessageService.CreateMessageAsync(new Message {
    client_id = clientId,
    message_subject = "Exclusive Weekend Offer",
    message_body = "Save 15% on weekend stays...",
    message_type = "offer",
    message_category = "Membership Rewards",
    action_label = "Book Now",
    action_url = "/booking/new"
});
```

## ??? Database Queries

### Get All Messages
```sql
SELECT * FROM Messages 
WHERE client_id = @clientId 
ORDER BY sent_date DESC;
```

### Get Unread Count
```sql
SELECT COUNT(*) FROM Messages 
WHERE client_id = @clientId AND is_read = 0;
```

### Mark as Read
```sql
UPDATE Messages 
SET is_read = 1 
WHERE message_id = @messageId;
```

### Get by Category
```sql
SELECT * FROM Messages 
WHERE client_id = @clientId 
  AND message_category = @category
ORDER BY sent_date DESC;
```

### Delete Old Messages
```sql
DELETE FROM Messages 
WHERE sent_date < DATEADD(day, -90, GETDATE());
```

## ?? Debugging

### Check Service Registration
```csharp
// In MauiProgram.cs
builder.Services.AddSingleton<MessageService>(); // ?
```

### Check Navigation
```razor
<!-- In nav bar -->
<a href="/client/messages/@clientId" class="nav-link">Messages</a>
```

### Check Database Connection
```csharp
using var con = Database.DbConnection.GetConnection();
await con.OpenAsync(); // Should not throw
```

### Console Logging
```csharp
Console.WriteLine($"Loading messages for client {clientId}");
Console.WriteLine($"Found {messages.Count} messages");
Console.WriteLine($"Unread count: {unreadCount}");
```

## ? Performance Tips

### Use Indexes
```sql
CREATE INDEX IX_Messages_ClientId ON Messages(client_id);
CREATE INDEX IX_Messages_IsRead ON Messages(is_read);
CREATE INDEX IX_Messages_SentDate ON Messages(sent_date DESC);
```

### Paginate Results
```csharp
// Load 50 messages at a time
SELECT TOP 50 * FROM Messages 
WHERE client_id = @clientId 
ORDER BY sent_date DESC;
```

### Cache Unread Count
```csharp
// Cache for 5 minutes
private static Dictionary<int, (int count, DateTime expiry)> _cache = new();

public async Task<int> GetUnreadCountAsync(int clientId)
{
    if (_cache.TryGetValue(clientId, out var cached))
    {
        if (cached.expiry > DateTime.Now)
return cached.count;
    }
 
    var count = await GetFromDatabase(clientId);
    _cache[clientId] = (count, DateTime.Now.AddMinutes(5));
    return count;
}
```

## ?? Best Practices

### Message Creation
```csharp
// ? Good: Provide all relevant details
await MessageService.CreateMessageAsync(new Message
{
    client_id = clientId,
    message_subject = "Clear, descriptive subject",
    message_body = "Full details with context...",
    message_type = "service",
    message_category = "Harbourkey Waterfront",
    booking_id = bookingId, // Link to related entity
    action_label = "View Booking",
    action_url = "/booking/123"
});

// ? Bad: Missing important fields
await MessageService.CreateMessageAsync(new Message
{
    client_id = clientId,
    message_subject = "Message",
    message_body = "Info"
});
```

### Error Handling
```csharp
try
{
    await MessageService.CreateMessageAsync(message);
}
catch (SqlException ex)
{
    Console.WriteLine($"Database error: {ex.Message}");
    // Log error, notify admin
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
  // Handle gracefully
}
```

### UI Updates
```csharp
// Update UI after operations
await MarkAsReadAsync(messageId);
unreadCount = await MessageService.GetUnreadCountAsync(clientId);
StateHasChanged(); // Refresh UI
```

## ?? Responsive Breakpoints

| Breakpoint | Width | Layout |
|------------|-------|--------|
| Desktop | 1024px+ | Two columns |
| Tablet | 768px-1023px | Single column, overlay |
| Mobile | <768px | Mobile-optimized |

## ?? Security Checklist

- [ ] Verify client owns messages before display
- [ ] Use parameterized SQL queries
- [ ] Sanitize user input in email form
- [ ] Implement rate limiting for emails
- [ ] Validate client_id from authenticated session
- [ ] Escape HTML in message body

## ?? Support

**Documentation:**
- `MESSAGES_SYSTEM_README.md` - Full documentation
- `MESSAGES_TESTING_GUIDE.md` - Testing procedures
- `MessagesSetup.sql` - Database schema

**Common Issues:**
1. Messages not loading ? Check database connection
2. Unread count wrong ? Verify is_read field updates
3. Email not sending ? Check MessageService registration
4. Navigation broken ? Verify route parameters

---

**Version:** 1.0.0  
**Last Updated:** 2024  
**Quick Access:** `/client/messages/{clientId}`
