# PayMongo Quick Test Reference

## ?? API Keys (Already Configured)
```
Public Key: pk_test_YOUR_PUBLIC_KEY_HERE
Secret Key: sk_test_YOUR_SECRET_KEY_HERE
```

## ?? Test Card Numbers

### ? Successful Payment
```
Card Number: 4123 4500 0000 0008
Expiry: 12/25
CVC: 123
Name: John Doe
```

### ?? 3D Secure Required
```
Card Number: 4571 7360 0000 0108
Expiry: 12/25
CVC: 123
Name: Jane Smith
```

### ? Payment Declined
```
Card Number: 4571 7360 0000 0207
Expiry: 12/25
CVC: 123
Name: Test User
```

### ?? Invalid Card
```
Card Number: 4000 0000 0000 0002
Expiry: 12/25
CVC: 123
```

## ?? Quick Test Steps

### Test 1: Card Payment (2 minutes)
1. Go to: `/booking/payment/{clientId}/{bookingId}`
2. Verify info pre-filled
3. Keep "Credit/Debit Card" selected
4. Enter test card: `4123 4500 0000 0008`
5. Expiry: `12/25`, CVC: `123`
6. Name: `Test User`
7. Check terms checkbox
8. Click "Complete Payment"
9. ? Should redirect to confirmation

### Test 2: GCash Payment (1 minute)
1. On payment page
2. Click "GCash" button
3. Check terms checkbox
4. Click "Complete Payment"
5. ? Should redirect to PayMongo checkout
6. (In test mode, you can simulate payment)

### Test 3: PayMaya Payment (1 minute)
1. On payment page
2. Click "PayMaya" button
3. Check terms checkbox
4. Click "Complete Payment"
5. ? Should redirect to PayMongo checkout

### Test 4: Validation (1 minute)
1. On payment page
2. Clear name fields
3. Try to pay
4. ? Button should be disabled
5. Fill fields
6. ? Button should enable

### Test 5: Debug Mode (30 seconds)
1. Click "?? Show Debug" button
2. ? See all field values
3. ? See validation states
4. ? See amount in cents

## ?? Expected Console Output

### Card Payment Success
```
ProcessPayment method called
Starting payment processing for booking 123
Payment method: card
Total amount: ?1,234.56 (123456 cents)
=== Starting Card Payment Process ===
Step 1: Creating payment intent...
? Payment intent created: pi_xxxxx
  Status: awaiting_payment_method
Step 2: Creating payment method...
? Payment method created: pm_xxxxx
Step 3: Attaching payment method to intent...
? Payment method attached successfully
  Final Status: succeeded
??? Card payment completed successfully ???
Updating booking status to confirmed...
? Booking 123 status updated to: confirmed
  Payment Intent ID: pi_xxxxx
  Payment Method: card
Payment processing completed successfully
Navigating to confirmation: /booking/confirmation/1/123
```

### GCash Payment
```
ProcessPayment method called
Starting payment processing for booking 123
Payment method: gcash
=== Starting GCash Payment Process ===
Success URL: https://localhost:7000/booking/confirmation/1/123
Failed URL: https://localhost:7000/booking/payment/1/123
Creating GCash payment source...
? GCash source created: src_xxxxx
  Checkout URL: https://pm.link/xxxxx
  Status: pending
Redirecting to: https://pm.link/xxxxx
```

## ?? Common Issues & Fixes

| Issue | Fix |
|-------|-----|
| Button disabled | Check terms checkbox |
| "Fill required fields" | Complete all guest info |
| "Invalid card" | Use test card numbers |
| Payment fails | Check console logs |
| No redirect | Check network tab |

## ?? Success Criteria

? Card payment completes
? Booking status changes to "confirmed"
? Redirect to confirmation page
? Console shows no errors
? E-wallet redirects work
? Validation prevents bad input
? Debug mode shows data

## ?? Payment Flow Diagram

```
[Guest Info] ? [Select Method] ? [Enter Details] ? [Terms] ? [Pay Button]
    ?          ?        ?     ?
         Card        Fill Card        Accept      Process
     GCash      Details    Terms Payment
           PayMaya    ?   ?            ?
     ?      Success
          Validate            ?
          ? Update DB
      Enable  ?
            ButtonRedirect
```

## ?? Debug Checklist

- [ ] Console shows payment method
- [ ] Amount in cents is correct (multiply by 100)
- [ ] Payment intent ID generated
- [ ] Payment method ID generated
- [ ] Status is "succeeded"
- [ ] Booking status updated
- [ ] Navigation triggered

## ?? Need Help?

1. Check console logs (F12 ? Console)
2. Enable debug mode on payment page
3. Review `PAYMONGO_IMPLEMENTATION_GUIDE.md`
4. Check PayMongo status: https://status.paymongo.com
5. Contact PayMongo support: support@paymongo.com

## ?? Ready to Test?

```bash
# Run your application
dotnet run

# Navigate to payment page
# URL format: /booking/payment/{clientId}/{bookingId}
# Example: /booking/payment/1/1
```

---

**Quick Test:** Use card `4123 4500 0000 0008` ? Should work immediately! ?
