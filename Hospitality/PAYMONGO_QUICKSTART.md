# ?? PayMongo Quick Start - Get Testing in 2 Minutes!

## ? Super Quick Test

```bash
# 1. Run your app
dotnet run

# 2. Go to payment page
# URL: /booking/payment/{clientId}/{bookingId}
# Example: http://localhost:7000/booking/payment/1/1

# 3. Use this card
Card: 4123 4500 0000 0008
Expiry: 12/25
CVC: 123
Name: Test User

# 4. Check terms ? Click "Complete Payment"
# 5. ? Success! Should redirect to confirmation
```

## ?? What Just Happened?

Your payment went through these steps:
1. Created payment intent with PayMongo
2. Created payment method with card details
3. Attached payment method to intent
4. Payment succeeded
5. Booking status updated to "confirmed"
6. Redirected to confirmation page

## ?? Check Console Output

You should see:
```
=== Starting Card Payment Process ===
? Payment intent created: pi_xxxxx
? Payment method created: pm_xxxxx
? Payment method attached successfully
??? Card payment completed successfully ???
```

## ?? Enable Debug Mode

Click "?? Show Debug" button to see:
- All form field values
- Validation states
- Payment intent ID
- Amount in cents

## ?? More Test Cards

```
? Success: 4123 4500 0000 0008
?? 3D Secure: 4571 7360 0000 0108
? Declined: 4571 7360 0000 0207
```

## ?? Test E-Wallets

### GCash
1. Select "GCash"
2. Click "Complete Payment"
3. Will redirect to PayMongo checkout
4. Complete payment there

### PayMaya
1. Select "PayMaya"
2. Click "Complete Payment"
3. Will redirect to PayMongo checkout
4. Complete payment there

## ? Success Checklist

- [ ] Card payment completes
- [ ] Console shows "??? Card payment completed successfully ???"
- [ ] Redirects to confirmation page
- [ ] Booking status is "confirmed"
- [ ] No errors in console

## ?? Something Wrong?

### Button is Disabled
- ? Fill all guest info fields
- ? Fill all card fields
- ? Check terms checkbox

### Payment Fails
- ? Check console for error message
- ? Try different test card
- ? Verify API keys in appsettings.json

### No Redirect
- ? Check console for navigation logs
- ? Verify booking ID is valid

## ?? Need More Info?

Read these files:
- **Quick Reference:** `PAYMONGO_QUICK_REFERENCE.md`
- **Full Guide:** `PAYMONGO_IMPLEMENTATION_GUIDE.md`
- **Webhooks:** `PAYMONGO_WEBHOOK_GUIDE.md`
- **Summary:** `PAYMONGO_IMPLEMENTATION_SUMMARY.md`

## ?? That's It!

Your PayMongo integration is working!

**Production ready?** Update API keys and implement webhooks.

**Questions?** Check the documentation files!

---

**Quick Test Again:**
`4123 4500 0000 0008` ? `12/25` ? `123` ? Pay ? ? Success!
