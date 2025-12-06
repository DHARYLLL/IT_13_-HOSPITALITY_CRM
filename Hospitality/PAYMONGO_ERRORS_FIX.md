# PayMongo API Errors - Fixes Required

## Issues Found

### 1. **Card Payment Error**: Nested Metadata ?
**Error:**
```
metadata attributes cannot be nested
```

**Problem:** PayMongo API doesn't accept nested objects in metadata. All values must be strings.

**Current Code (WRONG):**
```csharp
Metadata = new Dictionary<string, object>
{
    { "booking_id", bookingId },              // int - WRONG
    { "client_id", clientId },        // int - WRONG
    { "check_in", booking?.check_in_date } // DateTime - WRONG
}
```

**Fix:** Convert all values to strings:
```csharp
Metadata = new Dictionary<string, object>
{
    { "booking_id", bookingId.ToString() },
    { "client_id", clientId.ToString() },
    { "check_in", booking?.check_in_date.ToString("yyyy-MM-dd") ?? "" }
}
```

### 2. **GCash Error**: Missing Checkout URL ?
**Error:**
```
GCash checkout URL not provided by PayMongo
```

**Problem:** The API response structure might not include the checkout URL where we expect it.

**Solution:** Check the actual API response structure and handle it properly.

### 3. **PayMaya Error**: Invalid Source Type ?
**Error:**
```
The source_type passed paymaya is invalid
```

**Problem:** The correct type for PayMaya in PayMongo API sources is `paymaya` (which we're using), but there might be an issue with how we're sending it.

**Solution:** Verify the API request structure matches PayMongo documentation.

## Quick Fix Steps

1. Update Payment.razor - convert all metadata values to strings
2. Update PayMongoService.cs - improve response parsing for sources
3. Test each payment method

## Files to Modify

1. `Hospitality/Components/Pages/Payment.razor`
2. `Hospitality/Services/PayMongoService.cs` (optional - for better error messages)

---

**Status:** Fixing now...
