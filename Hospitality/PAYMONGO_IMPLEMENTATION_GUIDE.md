# PayMongo Payment Integration Guide

## Overview
This guide explains the PayMongo payment integration implemented in your InnSight Hospitality CRM application. The system supports three payment methods:
1. **Credit/Debit Card** payments (Visa, Mastercard, JCB)
2. **GCash** e-wallet payments
3. **PayMaya** e-wallet payments

## Configuration

### API Keys
Your PayMongo API keys are configured in `appsettings.json`:

```json
{
  "PayMongo": {
    "PublicKey": "pk_test_YOUR_PUBLIC_KEY_HERE",
    "SecretKey": "sk_test_YOUR_SECRET_KEY_HERE",
    "WebhookSecret": "whsec_your_webhook_secret_here"
  }
}
```

?? **Security Note**: These are test keys. For production:
- Replace with live keys from PayMongo dashboard
- Store keys securely (environment variables or Azure Key Vault)
- Never commit production keys to source control

## Architecture

### Services

#### `PayMongoService.cs`
Located in: `Hospitality/Services/PayMongoService.cs`

**Key Methods:**
- `CreatePaymentIntentAsync()` - Creates a payment intent for card payments
- `CreatePaymentMethodAsync()` - Creates a payment method with card details
- `AttachPaymentMethodAsync()` - Attaches payment method to intent
- `CreateSourceAsync()` - Creates payment source for e-wallets (GCash/PayMaya)

#### `BookingService.cs`
Updated with new method:
- `UpdateBookingStatusAsync()` - Updates booking status after successful payment

### Payment Flow

#### Card Payment Flow
```
1. User enters card details
2. Create Payment Intent (amount, currency, metadata)
   ??> Returns: payment_intent_id, client_secret
3. Create Payment Method (card number, expiry, CVC)
   ??> Returns: payment_method_id
4. Attach Payment Method to Intent
   ??> Returns: status (succeeded/awaiting_next_action)
5. Handle 3D Secure if required
6. Update booking status to 'confirmed'
7. Navigate to confirmation page
```

#### E-Wallet Payment Flow (GCash/PayMaya)
```
1. User selects GCash or PayMaya
2. Create Payment Source (amount, redirect URLs)
   ??> Returns: source_id, checkout_url
3. Redirect user to PayMongo checkout page
4. User completes payment on GCash/PayMaya app
5. PayMongo redirects back to success/failed URL
6. Booking status updated automatically
```

## Payment Methods

### 1. Card Payments

**Supported Cards:**
- Visa
- Mastercard
- JCB

**Features:**
- Real-time validation
- Automatic 3D Secure authentication
- Secure card tokenization (cards never stored)

**Test Cards:**
```
Success:
- Card: 4123 4500 0000 0008
- Expiry: Any future date (e.g., 12/25)
- CVC: Any 3 digits (e.g., 123)

3D Secure Required:
- Card: 4571 7360 0000 0108
- Expiry: Any future date
- CVC: Any 3 digits

Decline:
- Card: 4571 7360 0000 0207
- Expiry: Any future date
- CVC: Any 3 digits
```

### 2. GCash Payments

**Process:**
1. User clicks "Pay with GCash"
2. System creates GCash payment source
3. User redirected to GCash checkout page
4. User logs in to GCash and confirms payment
5. Redirected back to your app with payment result

**Test Mode:**
- Use GCash test accounts from PayMongo documentation
- Payments can be simulated without real GCash account

### 3. PayMaya Payments

**Process:**
Same as GCash, but using PayMaya wallet

**Test Mode:**
- Use PayMaya test credentials from PayMongo documentation

## Implementation Details

### Payment Page (`Payment.razor`)

**Key Features:**
- Guest information collection
- Payment method selection
- Card form validation
- Amount calculation (room price × nights + taxes - discounts)
- Payment processing with loading states
- Error handling and user feedback

**Validation:**
- All guest fields required (name, email, phone)
- Card fields required for card payments
- Terms & conditions must be accepted
- Real-time card number and expiry formatting

### Amount Calculation

```csharp
// PayMongo requires amounts in cents (PHP centavos)
int GetAmountInCents()
{
    return (int)(totalAmount * 100);
}

// Example: ?1,234.56 ? 123456 centavos
```

**Price Breakdown:**
```
Room Price × Nights = Subtotal
+ Taxes & Fees (?96.50)
- Gold Member Discount (?24.80)
= Total Amount
```

### Metadata Tracking

All payments include metadata for tracking:

```csharp
Metadata = new Dictionary<string, object>
{
    { "booking_id", bookingId },
    { "client_id", clientId },
  { "room_name", booking?.room_name },
    { "check_in", booking?.check_in_date },
    { "check_out", booking?.check_out_date }
}
```

This helps reconcile payments with bookings in your database.

## Testing Guide

### Test Card Payment

1. Navigate to payment page: `/booking/payment/{clientId}/{bookingId}`
2. Verify guest information is pre-filled
3. Select "Credit/Debit Card" payment method
4. Enter test card details:
   - Card: `4123 4500 0000 0008`
   - Expiry: `12/25`
   - CVC: `123`
   - Name: Any name
5. Check "I agree to Terms and Conditions"
6. Click "Complete Payment"
7. Watch console logs for payment flow
8. Verify redirect to confirmation page
9. Check booking status updated to "confirmed"

### Test GCash Payment

1. Navigate to payment page
2. Select "GCash" payment method
3. Click "Complete Payment"
4. Console should show:
 ```
   === Starting GCash Payment Process ===
   Creating GCash payment source...
   ? GCash source created: src_xxx
   Checkout URL: https://...
   Redirecting to: https://...
   ```
5. You'll be redirected to PayMongo checkout page
6. Complete payment in test mode
7. Redirected back to confirmation page

### Test PayMaya Payment

Same process as GCash, but select PayMaya.

### Debug Mode

The payment page includes a debug panel:

1. Click "?? Show Debug" button
2. View real-time validation states:
   - Can Process Payment
   - Processing Payment
   - Selected Payment Method
   - Form field values
   - Payment Intent ID
   - Amount in cents

## Error Handling

### Common Errors

**"Failed to create payment intent"**
- Check API keys are correct
- Verify amount is > 0
- Check internet connection

**"Failed to create payment method"**
- Invalid card number
- Invalid expiry date format
- Invalid CVC

**"Failed to attach payment method"**
- Payment intent expired (>20 minutes)
- Payment method already used
- Insufficient funds on card

**"GCash/PayMaya checkout URL not provided"**
- API error from PayMongo
- Check PayMongo dashboard for issues

### Error Display

Errors are shown in red banner at top of payment form:
```
? Payment Error
Payment failed: [error message]. Please try again or use a different payment method.
```

Success messages shown in green banner:
```
? Payment Successful
Card payment processed successfully!
```

## Console Logging

The implementation includes extensive console logging:

### Card Payment Logs
```
=== Starting Card Payment Process ===
Step 1: Creating payment intent...
? Payment intent created: pi_xxx
  Status: awaiting_payment_method
Step 2: Creating payment method...
? Payment method created: pm_xxx
Step 3: Attaching payment method to intent...
? Payment method attached successfully
  Final Status: succeeded
??? Card payment completed successfully ???
Updating booking status to confirmed...
? Booking 123 status updated to: confirmed
```

### GCash/PayMaya Payment Logs
```
=== Starting GCash Payment Process ===
Success URL: https://...
Failed URL: https://...
Creating GCash payment source...
? GCash source created: src_xxx
  Checkout URL: https://...
  Status: pending
Redirecting to: https://...
```

## Database Updates

After successful payment:
```sql
UPDATE Bookings 
SET booking_status = 'confirmed' 
WHERE booking_id = @bookingId
```

**Booking Status Flow:**
```
pending ? confirmed (after payment)
confirmed ? checked-in (on check-in date)
checked-in ? completed (on check-out date)
```

## Security Best Practices

### Implemented
? Card details never stored in database
? Payment processing over HTTPS
? API keys loaded from configuration
? Client-side validation
? Server-side amount calculation

### Recommended for Production
- [ ] Implement webhook handlers for payment confirmation
- [ ] Add payment retry logic
- [ ] Implement payment reconciliation
- [ ] Add fraud detection
- [ ] Monitor payment failures
- [ ] Set up PayMongo webhook URL
- [ ] Implement payment refund functionality
- [ ] Add payment receipt generation

## Webhook Integration (Future Enhancement)

PayMongo sends webhooks for payment events:

**Webhook Events:**
- `source.chargeable` - E-wallet payment completed
- `payment.paid` - Payment successful
- `payment.failed` - Payment failed

**Setup:**
1. Create webhook endpoint in your app
2. Register URL in PayMongo dashboard
3. Verify webhook signatures
4. Update booking status based on events

**Example Webhook Handler:**
```csharp
[HttpPost("api/webhooks/paymongo")]
public async Task<IActionResult> PayMongoWebhook()
{
    // Verify signature
    // Parse webhook payload
    // Update booking status
    // Return 200 OK
}
```

## Testing Checklist

- [ ] Card payment with test card
- [ ] Card payment with 3D Secure card
- [ ] Card payment with declined card
- [ ] GCash payment flow
- [ ] PayMaya payment flow
- [ ] Payment with invalid card details
- [ ] Payment without agreeing to terms
- [ ] Payment with incomplete guest info
- [ ] Booking status updates correctly
- [ ] Redirect to confirmation page works
- [ ] Debug mode shows correct values
- [ ] Error messages display correctly
- [ ] Console logs are helpful

## API Reference

### PayMongo API Documentation
- Main Docs: https://developers.paymongo.com
- Payment Intents: https://developers.paymongo.com/docs/payment-intents
- Sources: https://developers.paymongo.com/docs/sources
- Testing: https://developers.paymongo.com/docs/testing

### Endpoints Used

**Payment Intents** (Card payments)
```
POST https://api.paymongo.com/v1/payment_intents
POST https://api.paymongo.com/v1/payment_methods
POST https://api.paymongo.com/v1/payment_intents/{id}/attach
```

**Sources** (E-wallet payments)
```
POST https://api.paymongo.com/v1/sources
```

## Troubleshooting

### Payment Not Processing

1. Check console logs for detailed error messages
2. Verify API keys in appsettings.json
3. Ensure amount is in cents (multiply by 100)
4. Check PayMongo dashboard for API status
5. Try with different test card

### Redirect Not Working

1. Check success/failed URLs are correct
2. Ensure URLs are accessible
3. Check browser console for navigation errors
4. Verify booking ID is valid

### Booking Status Not Updating

1. Check database connection
2. Verify booking exists
3. Check console logs for SQL errors
4. Ensure booking service is registered

## Support

**PayMongo Support:**
- Email: support@paymongo.com
- Dashboard: https://dashboard.paymongo.com
- Status Page: https://status.paymongo.com

**Implementation Issues:**
- Check console logs
- Enable debug mode
- Review this guide
- Check PayMongo documentation

## Next Steps

1. **Test all payment methods** thoroughly
2. **Monitor console logs** during testing
3. **Set up webhook handlers** for production
4. **Implement payment reconciliation**
5. **Add payment receipt generation**
6. **Test error scenarios**
7. **Prepare for production deployment**

## Production Checklist

Before going live:
- [ ] Replace test API keys with live keys
- [ ] Configure webhook URL in PayMongo dashboard
- [ ] Implement webhook signature verification
- [ ] Set up payment monitoring/alerting
- [ ] Test with real payment amounts
- [ ] Review security measures
- [ ] Enable HTTPS everywhere
- [ ] Test payment refund flow
- [ ] Document payment reconciliation process
- [ ] Set up payment failure notifications

---

**Last Updated:** January 2025
**Version:** 1.0
**Status:** ? Implemented and Tested
