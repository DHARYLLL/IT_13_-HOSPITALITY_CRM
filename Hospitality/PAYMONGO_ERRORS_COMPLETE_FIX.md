# ?? PayMongo Payment Errors - Complete Fix Guide

## Issues Identified

### 1. ? Card Payment: Nested Metadata Error
**Error Message:**
```
metadata attributes cannot be nested
```

**Root Cause:** 
PayMongo API requires ALL metadata values to be strings, but we're passing integers and objects.

**Current Code (WRONG):**
```csharp
Metadata = new Dictionary<string, object>
{
    { "booking_id", bookingId },  // int ?
    { "client_id", clientId },           // int ?
    { "room_name", booking?.room_name ?? "" }, // object ?
    { "check_in", booking?.check_in_date },    // DateTime ?
    { "check_out", booking?.check_out_date }   // DateTime ?
}
```

**Fixed Code (CORRECT):**
```csharp
Metadata = new Dictionary<string, object>
{
    { "booking_id", bookingId.ToString() },  // string ?
    { "client_id", clientId.ToString() },       // string ?
    { "room_name", booking?.room_name ?? "" },          // string ?
    { "check_in", booking?.check_in_date.ToString("yyyy-MM-dd") ?? "" }, // string ?
    { "check_out", booking?.check_out_date.ToString("yyyy-MM-dd") ?? "" }, // string ?
    { "guest_name", $"{firstName} {lastName}" },                     // string ?
    { "total_amount", totalAmount.ToString("F2") }        // string ?
}
```

### 2. ? GCash Payment: Missing Checkout URL
**Error Message:**
```
GCash checkout URL not provided by PayMongo
```

**Root Cause:** 
The API response structure is correct, but we need to log the full response to see what we're getting.

**Solution:**
- Add more detailed logging in `PayMongoService.cs`
- Check if the response is actually successful
- Verify the JSON deserialization is working correctly

### 3. ? PayMaya Payment: Invalid Source Type
**Error Message:**
```
The source_type passed paymaya is invalid
```

**Root Cause:**
According to PayMongo docs, `paymaya` is a valid source type, but there might be a formatting issue or the type might need to be different.

**Possible Solutions:**
1. Check if it should be `paymaya` or `pay_maya`
2. Verify the API version we're using supports PayMaya
3. Check if there are additional required fields for PayMaya

## Quick Fix Steps

### Step 1: Fix Card Payment Metadata

**File:** `Hospitality/Components/Pages/Payment.razor`
**Method:** `ProcessCardPayment()`

Replace the metadata section:

```csharp
Metadata = new Dictionary<string, object>
{
    { "booking_id", bookingId.ToString() },
    { "client_id", clientId.ToString() },
    { "room_name", booking?.room_name ?? "" },
    { "check_in", booking?.check_in_date.ToString("yyyy-MM-dd") ?? "" },
    { "check_out", booking?.check_out_date.ToString("yyyy-MM-dd") ?? "" },
    { "guest_name", $"{firstName} {lastName}" },
    { "total_amount", totalAmount.ToString("F2") }
}
```

### Step 2: Add Debug Logging for Sources

**File:** `Hospitality/Services/PayMongoService.cs`
**Method:** `CreateSourceAsync()`

Add logging after the API call:

```csharp
var response = await _httpClient.PostAsync("sources", content);

// ADD THIS LOGGING:
Console.WriteLine($"Source API Response Status: {response.StatusCode}");
var responseContent = await response.Content.ReadAsStringAsync();
Console.WriteLine($"Source API Response Body: {responseContent}");

if (response.IsSuccessStatusCode)
{
    var result = JsonSerializer.Deserialize<SourceApiResponse>(responseContent, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    // ADD THIS LOGGING:
    Console.WriteLine($"Parsed Source ID: {result?.Data?.Id}");
    Console.WriteLine($"Parsed Checkout URL: {result?.Data?.Attributes?.Redirect?.CheckoutUrl}");
    Console.WriteLine($"Parsed Status: {result?.Data?.Attributes?.Status}");

  return new SourceResult
    {
   Success = true,
        SourceId = result?.Data?.Id ?? "",
        CheckoutUrl = result?.Data?.Attributes?.Redirect?.CheckoutUrl ?? "",
        Status = result?.Data?.Attributes?.Status ?? ""
    };
}
```

### Step 3: Test PayMaya Source Type

Try these alternatives in order:

**Option 1:** Keep as "paymaya"
```csharp
Type = "paymaya"
```

**Option 2:** Try with underscore
```csharp
Type = "pay_maya"
```

**Option 3:** Check PayMongo docs for correct type
Visit: https://developers.paymongo.com/docs/sources

## Testing Procedure

### Test 1: Card Payment
```bash
1. Open app
2. Go to payment page
3. Fill guest info
4. Select "Credit/Debit Card"
5. Enter: 4123 4500 0000 0008
6. Expiry: 12/25, CVC: 123
7. Name: Test User
8. Check terms
9. Click "Complete Payment"

Expected: ? Payment succeeds
```

### Test 2: GCash Payment
```bash
1. Select "GCash"
2. Click "Complete Payment"
3. Check console for:
   - Source API Response Status
   - Source API Response Body
   - Parsed Checkout URL

Expected: Should see checkout URL in logs
```

### Test 3: PayMaya Payment
```bash
1. Select "PayMaya"
2. Click "Complete Payment"
3. Check console for error details

If fails: Try changing type to "pay_maya"
```

## Expected Console Output

### Card Payment Success:
```
ProcessPayment method called
=== Starting Card Payment Process ===
Step 1: Creating payment intent...
? Payment intent created: pi_xxxxx
Step 2: Creating payment method...
? Payment method created: pm_xxxxx
Step 3: Attaching payment method to intent...
? Payment method attached successfully
??? Card payment completed successfully ???
```

### GCash Payment Success:
```
=== Starting GCash Payment Process ===
Creating GCash payment source...
Source API Response Status: OK
Source API Response Body: { "data": { "id": "src_xxx", ... } }
Parsed Source ID: src_xxxxx
Parsed Checkout URL: https://pm.link/xxxxx
? GCash source created: src_xxxxx
Redirecting to: https://pm.link/xxxxx
```

### PayMaya Payment Success:
```
=== Starting PayMaya Payment Process ===
Creating PayMaya payment source...
Source API Response Status: OK
? PayMaya source created: src_xxxxx
Redirecting to: https://pm.link/xxxxx
```

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Still getting metadata error | Double-check ALL values are `.ToString()` |
| GCash URL still empty | Check JSON response structure |
| PayMaya type invalid | Try "pay_maya" or check docs |
| API returns 400 | Check request payload format |
| API returns 401 | Verify API keys are correct |

## API Documentation References

**PayMongo Sources API:**
https://developers.paymongo.com/docs/sources

**Supported Source Types:**
- `gcash` - GCash wallet
- `paymaya` - PayMaya wallet  
- `grab_pay` - GrabPay wallet

**Metadata Requirements:**
- Must be flat key-value pairs
- Values must be strings
- Cannot contain nested objects/arrays

## Quick Test Commands

```bash
# Test card payment
curl -X POST https://api.paymongo.com/v1/payment_intents \
  -u sk_test_yourkey: \
  -H "Content-Type: application/json" \
  -d '{
 "data": {
   "attributes": {
    "amount": 10000,
     "currency": "PHP",
    "metadata": {
        "booking_id": "123",
   "client_id": "1"
        }
      }
    }
  }'

# Test GCash source
curl -X POST https://api.paymongo.com/v1/sources \
  -u sk_test_yourkey: \
  -H "Content-Type: application/json" \
  -d '{
    "data": {
      "attributes": {
 "type": "gcash",
        "amount": 10000,
  "currency": "PHP",
        "redirect": {
          "success": "https://example.com/success",
          "failed": "https://example.com/failed"
        }
      }
    }
  }'
```

## Files to Modify

1. **Payment.razor** - Fix metadata in ProcessCardPayment()
2. **PayMongoService.cs** - Add debug logging in CreateSourceAsync()
3. **Test and verify** each payment method

## Next Steps

1. ? Apply metadata fix for card payments
2. ?? Add logging to source creation
3. ?? Test all three payment methods
4. ?? Document actual API responses
5. ?? Adjust based on test results

---

**Status:** Ready to fix
**Priority:** ?? Critical
**Estimated Time:** 15-30 minutes
