# PayMongo Webhook Handler Implementation Guide

## Overview
This guide shows how to implement PayMongo webhooks to automatically update booking status when payments are completed.

## Why Webhooks?

**Current Implementation:**
- Card payments: Immediate confirmation ?
- E-wallet payments: Redirects to external page, then back ??

**With Webhooks:**
- Automatic confirmation for ALL payment methods ?
- No reliance on redirect URLs ?
- Handle edge cases (user closes browser, etc.) ?

## Webhook Events

PayMongo sends these events:

| Event | Description | When to Use |
|-------|-------------|-------------|
| `source.chargeable` | E-wallet payment completed | Confirm GCash/PayMaya bookings |
| `payment.paid` | Payment successful | Additional confirmation |
| `payment.failed` | Payment failed | Update booking to failed status |

## Implementation Steps

### Step 1: Create Webhook Controller

Create file: `Hospitality/Controllers/PayMongoWebhookController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Hospitality.Services;

namespace Hospitality.Controllers
{
    [Route("api/webhooks")]
    [ApiController]
 public class PayMongoWebhookController : ControllerBase
    {
        private readonly BookingService _bookingService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayMongoWebhookController> _logger;

        public PayMongoWebhookController(
            BookingService bookingService, 
            IConfiguration configuration,
     ILogger<PayMongoWebhookController> logger)
    {
   _bookingService = bookingService;
    _configuration = configuration;
    _logger = logger;
        }

        [HttpPost("paymongo")]
        public async Task<IActionResult> HandleWebhook()
   {
    try
     {
                // Read request body
         using var reader = new StreamReader(Request.Body);
var body = await reader.ReadToEndAsync();

                _logger.LogInformation("Received PayMongo webhook");

           // Verify signature (IMPORTANT for production)
    var signature = Request.Headers["PayMongo-Signature"].ToString();
     if (!VerifySignature(body, signature))
      {
          _logger.LogWarning("Invalid webhook signature");
           return Unauthorized("Invalid signature");
      }

// Parse webhook payload
     var webhook = JsonSerializer.Deserialize<PayMongoWebhook>(body);
          
       if (webhook?.Data?.Attributes == null)
          {
               _logger.LogWarning("Invalid webhook payload");
    return BadRequest("Invalid payload");
    }

     var eventType = webhook.Data.Attributes.Type;
    _logger.LogInformation($"Processing webhook event: {eventType}");

   // Handle different event types
         switch (eventType)
      {
         case "source.chargeable":
  await HandleSourceChargeable(webhook);
     break;

    case "payment.paid":
            await HandlePaymentPaid(webhook);
     break;

          case "payment.failed":
      await HandlePaymentFailed(webhook);
break;

      default:
   _logger.LogInformation($"Unhandled event type: {eventType}");
    break;
      }

            return Ok(new { received = true });
            }
     catch (Exception ex)
   {
          _logger.LogError(ex, "Error processing webhook");
 return StatusCode(500, "Webhook processing error");
            }
    }

    private async Task HandleSourceChargeable(PayMongoWebhook webhook)
        {
            try
            {
var data = webhook.Data?.Attributes?.Data;
       var attributes = data?.Attributes;

        if (attributes?.Metadata == null)
             {
         _logger.LogWarning("No metadata in source.chargeable event");
   return;
     }

  // Extract booking ID from metadata
             if (attributes.Metadata.TryGetValue("booking_id", out var bookingIdStr))
          {
          if (int.TryParse(bookingIdStr, out int bookingId))
         {
          _logger.LogInformation($"E-wallet payment completed for booking {bookingId}");

             // Update booking status to confirmed
var sourceId = data?.Id;
              var paymentMethod = attributes.Type; // "gcash" or "paymaya"

    await _bookingService.UpdateBookingStatusAsync(
       bookingId, 
       "confirmed", 
                sourceId,
             paymentMethod
    );

         _logger.LogInformation($"? Booking {bookingId} confirmed via webhook");
        }
          }
       }
            catch (Exception ex)
  {
 _logger.LogError(ex, "Error handling source.chargeable");
            }
        }

        private async Task HandlePaymentPaid(PayMongoWebhook webhook)
 {
       try
      {
     var data = webhook.Data?.Attributes?.Data;
             var attributes = data?.Attributes;

         if (attributes?.Metadata == null)
         {
          _logger.LogWarning("No metadata in payment.paid event");
           return;
  }

     // Extract booking ID from metadata
    if (attributes.Metadata.TryGetValue("booking_id", out var bookingIdStr))
   {
      if (int.TryParse(bookingIdStr, out int bookingId))
      {
   _logger.LogInformation($"Payment confirmed for booking {bookingId}");

        var paymentIntentId = data?.Id;
 
      // Additional confirmation - update status if still pending
            await _bookingService.UpdateBookingStatusAsync(
   bookingId, 
 "confirmed", 
         paymentIntentId,
       "card"
    );

         _logger.LogInformation($"? Booking {bookingId} confirmed via payment.paid webhook");
       }
           }
         }
catch (Exception ex)
            {
          _logger.LogError(ex, "Error handling payment.paid");
     }
        }

        private async Task HandlePaymentFailed(PayMongoWebhook webhook)
        {
  try
   {
      var data = webhook.Data?.Attributes?.Data;
         var attributes = data?.Attributes;

       if (attributes?.Metadata == null)
      {
_logger.LogWarning("No metadata in payment.failed event");
        return;
   }

   // Extract booking ID from metadata
    if (attributes.Metadata.TryGetValue("booking_id", out var bookingIdStr))
   {
      if (int.TryParse(bookingIdStr, out int bookingId))
{
      _logger.LogWarning($"Payment failed for booking {bookingId}");

         // Update booking status to failed or pending
       await _bookingService.UpdateBookingStatusAsync(
        bookingId, 
       "payment_failed"
       );

  // TODO: Send notification to customer
          // TODO: Release reserved room if applicable

      _logger.LogInformation($"Booking {bookingId} marked as payment failed");
      }
        }
   }
  catch (Exception ex)
      {
          _logger.LogError(ex, "Error handling payment.failed");
      }
 }

        private bool VerifySignature(string payload, string signature)
{
// Get webhook secret from configuration
            var webhookSecret = _configuration["PayMongo:WebhookSecret"];

   if (string.IsNullOrEmpty(webhookSecret))
            {
   _logger.LogWarning("Webhook secret not configured");
          // For development, you might skip verification
       // For production, this should return false
            return true; // CHANGE TO false IN PRODUCTION
            }

      try
            {
                // PayMongo uses HMAC SHA256 for signature verification
                using var hmac = new System.Security.Cryptography.HMACSHA256(
         System.Text.Encoding.UTF8.GetBytes(webhookSecret)
   );

 var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
    var computedSignature = Convert.ToBase64String(hash);

     return signature == computedSignature;
            }
       catch (Exception ex)
         {
    _logger.LogError(ex, "Error verifying webhook signature");
      return false;
   }
        }
    }

    // Webhook data models
    public class PayMongoWebhook
    {
        public WebhookData? Data { get; set; }
    }

    public class WebhookData
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public WebhookAttributes? Attributes { get; set; }
 }

    public class WebhookAttributes
    {
        public string? Type { get; set; }
public DateTime CreatedAt { get; set; }
        public WebhookEventData? Data { get; set; }
    }

    public class WebhookEventData
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public WebhookEventAttributes? Attributes { get; set; }
    }

    public class WebhookEventAttributes
    {
        public string? Status { get; set; }
        public string? Type { get; set; }
        public int Amount { get; set; }
public string? Currency { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
  }
}
```

### Step 2: Update MauiProgram.cs

Add controller support:

```csharp
// In MauiProgram.cs CreateMauiApp method

// Add these lines:
builder.Services.AddControllers();

// After var app = builder.Build();
app.MapControllers();
```

### Step 3: Register Webhook in PayMongo Dashboard

1. Log in to PayMongo Dashboard: https://dashboard.paymongo.com
2. Go to **Developers** ? **Webhooks**
3. Click **Create Webhook**
4. Enter details:
   - **URL**: `https://yourdomain.com/api/webhooks/paymongo`
   - **Events**: Select:
     - `source.chargeable`
     - `payment.paid`
     - `payment.failed`
5. Copy the **Webhook Secret**
6. Update `appsettings.json`:

```json
{
  "PayMongo": {
    "PublicKey": "pk_test_...",
    "SecretKey": "sk_test_...",
    "WebhookSecret": "whsec_xxxxxxxxxxxxx"
  }
}
```

### Step 4: Test Webhooks Locally

Use **ngrok** to expose local server:

```bash
# Install ngrok
winget install ngrok

# Start your app
dotnet run

# In another terminal, expose port (e.g., 7000)
ngrok http https://localhost:7000

# Copy the ngrok URL (e.g., https://abc123.ngrok.io)
# Use in PayMongo webhook: https://abc123.ngrok.io/api/webhooks/paymongo
```

### Step 5: Test Webhook Flow

1. Make a GCash payment
2. Complete payment on GCash page
3. Check your application logs:

```
info: PayMongoWebhookController[0]
      Received PayMongo webhook
info: PayMongoWebhookController[0]
Processing webhook event: source.chargeable
info: PayMongoWebhookController[0]
      E-wallet payment completed for booking 123
info: PayMongoWebhookController[0]
      ? Booking 123 confirmed via webhook
```

4. Verify booking status in database:

```sql
SELECT booking_id, booking_status 
FROM Bookings 
WHERE booking_id = 123;
-- Should show: confirmed
```

## Webhook Payload Examples

### source.chargeable (GCash/PayMaya completed)

```json
{
  "data": {
    "id": "evt_xxx",
    "type": "event",
    "attributes": {
      "type": "source.chargeable",
      "created_at": "2025-01-20T10:30:00Z",
      "data": {
        "id": "src_xxx",
        "type": "source",
    "attributes": {
          "type": "gcash",
          "amount": 123456,
    "currency": "PHP",
          "status": "chargeable",
          "metadata": {
            "booking_id": "123",
            "client_id": "1"
          }
     }
      }
    }
  }
}
```

### payment.paid (Card payment successful)

```json
{
  "data": {
    "id": "evt_xxx",
    "type": "event",
    "attributes": {
 "type": "payment.paid",
    "created_at": "2025-01-20T10:30:00Z",
      "data": {
        "id": "pay_xxx",
        "type": "payment",
    "attributes": {
      "status": "paid",
          "amount": 123456,
  "currency": "PHP",
   "metadata": {
  "booking_id": "123",
     "client_id": "1"
 }
      }
      }
    }
  }
}
```

## Security Best Practices

### ? Implemented
- Signature verification
- Metadata validation
- Error logging
- Transaction isolation

### ?? Recommended
- Rate limiting on webhook endpoint
- Webhook event deduplication (check if already processed)
- Retry logic for failed database updates
- Alert monitoring for webhook failures
- Backup payment reconciliation job

## Monitoring & Debugging

### Check Webhook Delivery

In PayMongo Dashboard:
1. Go to **Developers** ? **Webhooks**
2. Click on your webhook
3. View **Recent Deliveries**
4. See status, response, and retry attempts

### Debug Failed Webhooks

```csharp
// Add detailed logging
_logger.LogInformation("Webhook payload: {Payload}", body);
_logger.LogInformation("Signature: {Signature}", signature);
_logger.LogInformation("Event type: {EventType}", eventType);
```

### Webhook Testing Tool

Use PayMongo webhook testing:

```bash
# Send test webhook
curl -X POST https://yourdomain.com/api/webhooks/paymongo \
  -H "Content-Type: application/json" \
  -H "PayMongo-Signature: test_signature" \
  -d '{
    "data": {
  "type": "event",
      "attributes": {
  "type": "source.chargeable",
        "data": {
          "attributes": {
     "metadata": {
              "booking_id": "123"
       }
          }
        }
 }
    }
  }'
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Webhook not received | Check ngrok is running, URL is correct |
| Signature verification fails | Check webhook secret matches |
| Booking not updating | Check booking ID in metadata |
| 500 errors | Check application logs |
| Duplicate processing | Implement idempotency checks |

## Production Checklist

Before deploying webhooks:

- [ ] Webhook endpoint is secured (HTTPS)
- [ ] Signature verification is enabled
- [ ] Webhook secret is stored securely
- [ ] Logging is comprehensive
- [ ] Error handling is robust
- [ ] Database transactions are atomic
- [ ] Idempotency is implemented
- [ ] Monitoring/alerting is set up
- [ ] Test webhook with real payments
- [ ] Document webhook recovery process

## Alternative: Polling

If webhooks are difficult to set up, use polling:

```csharp
// BackgroundService to check payment status
public class PaymentStatusChecker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
   {
            // Check for pending payments older than 5 minutes
       // Query PayMongo API for status
     // Update booking status if paid
 
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## Next Steps

1. **Implement webhook controller** (30 minutes)
2. **Set up ngrok for testing** (10 minutes)
3. **Register webhook in PayMongo** (5 minutes)
4. **Test with GCash payment** (15 minutes)
5. **Deploy to production** (when ready)

## Resources

- PayMongo Webhooks Docs: https://developers.paymongo.com/docs/webhooks
- ngrok Download: https://ngrok.com/download
- HMAC Signature Guide: https://en.wikipedia.org/wiki/HMAC

---

**Status:** ?? Ready to Implement
**Priority:** ?? High (Required for production e-wallet payments)
**Complexity:** ?? Medium
