# Client Messages & Notifications System

## Overview
The Client Messages & Notifications system provides a comprehensive inbox for clients to:
- View system notifications, offers, and updates
- Communicate with the hotel team via email
- Track booking-related messages
- Manage notification preferences

## Database Schema

### Messages Table
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

## Setup Instructions

### 1. Database Setup
Run the SQL script to create the necessary tables:
```bash
# Execute the MessagesSetup.sql script in SQL Server Management Studio
# Location: Hospitality/Database/MessagesSetup.sql
```

### 2. Service Registration
The MessageService is already registered in `MauiProgram.cs`:
```csharp
builder.Services.AddSingleton<MessageService>();
```

### 3. Navigation
Access the messages page via:
- URL: `/client/messages/{clientId}`
- Navigation link in the top menu: "Messages"

## Features

### 1. Inbox Management
- **Filter Options:**
  - All messages
  - Unread messages only
  - Stays-related messages
  - Offers and promotions
  
- **Time Range Filter:**
  - Last 30 days
  - Last 90 days
  - Custom date range

### 2. Message Categories
Messages are organized into categories:
- **Member Services** ?? - Account updates, loyalty program info
- **Billing & Receipts** ?? - Payment confirmations, invoices
- **Harbourkey Waterfront** ?? - Hotel-specific communications
- **Membership Rewards** ? - Loyalty offers and promotions
- **Email Sent** ?? - Outgoing messages to hotel team

### 3. Message Types
- **offer** - Special deals and promotions
- **service** - Booking confirmations, check-in reminders
- **billing** - Payment receipts, invoices
- **general** - Welcome messages, updates
- **outgoing** - Client-sent emails

### 4. Email Hotel Team
Clients can send emails to the hotel team:
- **Regarding Options:**
  - Upcoming stay, billing, or general question
  - Current reservation
  - Past stay inquiry
  - Billing question
  - Loyalty program

- **Features:**
  - Subject and message body
  - Saved as outgoing message
  - Email sent to hotel team

### 5. Message Actions
- Mark as read/unread
- Reply via email
- Action buttons (e.g., "Book a stay", "See eligible dates")
- Link to related bookings

## Component Structure

### ClientMessages.razor
Main component for the messages page:
- Inbox list (left panel)
- Message detail view (right panel)
- Email form for contacting hotel team

### MessageService.cs
Service layer for message operations:
- `GetClientMessagesAsync()` - Retrieve messages with filters
- `GetUnreadCountAsync()` - Get unread message count
- `MarkAsReadAsync()` - Mark message as read
- `MarkAllAsReadAsync()` - Mark all messages as read
- `SendEmailToHotelAsync()` - Send email to hotel team
- `CreateMessageAsync()` - Create new notification
- `DeleteMessageAsync()` - Delete message
- `GetMessageByIdAsync()` - Get single message details

### Message.cs
Model classes:
- `Message` - Message entity
- `MessageFilter` - Filter criteria
- `EmailRequest` - Email sending request

## Usage Examples

### Creating a System Notification
```csharp
var message = new Message
{
    client_id = clientId,
    message_subject = "Booking Confirmation",
    message_body = "Your booking has been confirmed...",
    message_type = "service",
    message_category = "Harbourkey Waterfront",
    booking_id = bookingId,
    action_label = "View Booking",
    action_url = $"/booking/confirmation/{bookingId}",
    regarding_text = "Regarding: Upcoming stay"
};

await MessageService.CreateMessageAsync(message);
```

### Sending an Email to Hotel Team
```csharp
var emailRequest = new EmailRequest
{
    client_id = clientId,
    regarding = "Upcoming stay, billing, or general question",
    subject = "Early check-in request",
    message_body = "I would like to request early check-in...",
    email_to = "frontdesk@innsight.com"
};

await MessageService.SendEmailToHotelAsync(emailRequest);
```

### Loading Messages with Filters
```csharp
var filter = new MessageFilter
{
    FilterType = "Unread", // "All", "Unread", "Stays", "Offers"
TimeRange = "Last 90 days"
};

var messages = await MessageService.GetClientMessagesAsync(clientId, filter);
```

## Styling

### CSS Classes
Located in `wwwroot/css/client-messages.css`:

**Inbox List:**
- `.inbox-item` - Message item in list
- `.inbox-item.unread` - Unread message
- `.inbox-item.selected` - Selected message
- `.unread-dot` - Unread indicator

**Message Detail:**
- `.message-detail` - Detail view container
- `.message-header` - Header with actions
- `.message-body` - Message content
- `.message-cta` - Call-to-action buttons

**Email Form:**
- `.email-form` - Email form container
- `.form-input` - Text inputs
- `.form-textarea` - Message textarea
- `.email-actions` - Action buttons

## Integration Points

### 1. Booking System
When a booking is created, send a confirmation message:
```csharp
await MessageService.CreateMessageAsync(new Message
{
    client_id = clientId,
    message_subject = "Booking Confirmation",
    message_body = $"Your booking for {roomName} has been confirmed...",
    message_type = "service",
    message_category = "Harbourkey Waterfront",
    booking_id = bookingId
});
```

### 2. Loyalty System
When points are earned, send a notification:
```csharp
await MessageService.CreateMessageAsync(new Message
{
    client_id = clientId,
    message_subject = "Points Earned",
    message_body = $"You've earned {points} points from your recent stay!",
    message_type = "service",
    message_category = "Membership Rewards"
});
```

### 3. Payment System
After payment, send a receipt notification:
```csharp
await MessageService.CreateMessageAsync(new Message
{
    client_id = clientId,
    message_subject = "Receipt Available",
    message_body = $"Your receipt for booking #{bookingId} is now available.",
    message_type = "billing",
    message_category = "Billing & Receipts",
    booking_id = bookingId
});
```

## Testing

### Sample Messages
The setup script creates sample messages for testing:
- Welcome message
- Exclusive offer
- Booking confirmation
- Thank you message
- Receipt notification

### Testing Checklist
- [ ] View all messages
- [ ] Filter by unread
- [ ] Filter by category
- [ ] Mark as read/unread
- [ ] Send email to hotel team
- [ ] Navigate to related booking
- [ ] Responsive design (mobile/tablet)
- [ ] Action buttons work correctly

## Responsive Design

The messages page is fully responsive:
- **Desktop (1024px+):** Two-column layout with inbox and detail view
- **Tablet (768px-1024px):** Single column with overlay detail view
- **Mobile (<768px):** Mobile-optimized with back buttons

## Future Enhancements

### Planned Features
1. **Push Notifications** - Real-time notifications for new messages
2. **Message Search** - Full-text search across messages
3. **Message Archiving** - Archive old messages
4. **Attachments** - Support file attachments in emails
5. **Templates** - Pre-defined email templates
6. **Read Receipts** - Track when hotel team reads emails
7. **Threaded Conversations** - Group related messages
8. **Notification Preferences** - Customize notification settings

### Integration Opportunities
- Email service integration (SendGrid, AWS SES)
- SMS notifications
- In-app notifications
- Desktop notifications
- Mobile push notifications

## Troubleshooting

### Common Issues

**Issue:** Messages not loading
**Solution:** Verify database connection and run MessagesSetup.sql

**Issue:** "No messages" displayed
**Solution:** Check client_id exists in Clients table, insert sample messages

**Issue:** Email not sending
**Solution:** Verify MessageService is registered in DI container

**Issue:** Unread count not updating
**Solution:** Call `MarkAsReadAsync()` when message is opened

## API Reference

### MessageService Methods

```csharp
// Get messages with optional filter
Task<List<Message>> GetClientMessagesAsync(int clientId, MessageFilter? filter = null)

// Get unread message count
Task<int> GetUnreadCountAsync(int clientId)

// Mark message as read
Task MarkAsReadAsync(int messageId)

// Mark all messages as read
Task MarkAllAsReadAsync(int clientId)

// Send email to hotel team
Task<int> SendEmailToHotelAsync(EmailRequest request)

// Create new message
Task<int> CreateMessageAsync(Message message)

// Delete message
Task DeleteMessageAsync(int messageId)

// Get message by ID
Task<Message?> GetMessageByIdAsync(int messageId)
```

## Security Considerations

1. **Authorization:** Verify client can only access their own messages
2. **SQL Injection:** All queries use parameterized commands
3. **XSS Protection:** Message content is sanitized for display
4. **Rate Limiting:** Implement rate limiting for email sending
5. **Spam Prevention:** Add CAPTCHA for email forms

## Performance Optimization

1. **Indexing:** Database indexes on client_id, is_read, sent_date
2. **Pagination:** Load messages in batches
3. **Caching:** Cache frequently accessed messages
4. **Lazy Loading:** Load message details on demand
5. **Image Optimization:** Compress images in messages

## Accessibility

- Semantic HTML structure
- ARIA labels for screen readers
- Keyboard navigation support
- Focus indicators
- Color contrast compliance (WCAG 2.1 AA)

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+
- Mobile browsers (iOS Safari, Chrome Mobile)

## Version History

### v1.0.0 (Current)
- Initial release
- Basic inbox functionality
- Email to hotel team
- Message filtering
- Responsive design

---

**Last Updated:** 2024
**Maintained By:** InnSight Development Team
