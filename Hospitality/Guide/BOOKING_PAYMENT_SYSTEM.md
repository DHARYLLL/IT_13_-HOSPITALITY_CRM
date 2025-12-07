# Booking & Payment System Updates

## Overview
This update implements the following features:
1. **Loyalty-based discounts** during booking
2. **Booking data saved only after successful payment**
3. **Multiple payments support** (downpayment and balance)
4. **Room status changes after successful payment**
5. **Simplified booking form** (removed unnecessary preferences)

## Key Changes

### 1. Loyalty Discount System
Discounts are automatically applied based on the client's loyalty tier:
- **Bronze**: No discount (0%)
- **Silver**: 5% discount
- **Gold**: 10% discount
- **Platinum**: 15% discount

The discount is calculated on the room subtotal before taxes.

### 2. Payment Flow
**Before** (Old Flow):
1. Select rooms ? Booking created immediately
2. Go to payment
3. Payment processed

**After** (New Flow):
1. Select rooms ? Booking data stored temporarily (NOT in database)
2. Go to payment with booking details in URL parameters
3. Payment processed
4. **Only after successful payment**: Booking is created in database
5. Rooms are marked as "Reserved"
6. Loyalty points are awarded

### 3. Multiple Payments (Downpayment)
Clients can now choose:
- **Pay in Full**: Pay 100% of the total amount
- **Downpayment (50%)**: Pay minimum 50% now, balance due at check-in

The system tracks:
- Total amount due
- Amount paid
- Remaining balance
- Payment history

### 4. Room Status Flow
- **Available**: Room is free for booking
- **Reserved**: Room is booked but client hasn't checked in yet (set after successful payment)
- **Occupied**: Client has checked in

### 5. Database Changes

#### New Table: `Payments`
Run `PaymentsSetup.sql` to create the payments table:

```sql
CREATE TABLE Payments (
    payment_id INT IDENTITY(1,1) PRIMARY KEY,
    booking_id INT NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    payment_method NVARCHAR(50) NOT NULL, -- card, gcash, grab_pay
    payment_status NVARCHAR(50) NOT NULL DEFAULT 'pending',
    payment_intent_id NVARCHAR(255) NULL,
    checkout_session_id NVARCHAR(255) NULL,
    payment_date DATETIME NOT NULL DEFAULT GETDATE(),
    payment_type NVARCHAR(50) NOT NULL DEFAULT 'full', -- full, downpayment, partial, balance
    notes NVARCHAR(500) NULL,
    CONSTRAINT FK_Payments_Bookings FOREIGN KEY (booking_id) REFERENCES Bookings(booking_id)
);
```

## Files Modified

### New Files
- `Hospitality\Models\Payment.cs` - Payment, BookingPaymentSummary, PendingBooking models
- `Hospitality\Services\PaymentService.cs` - Payment processing and loyalty discount logic
- `Hospitality\Database\PaymentsSetup.sql` - SQL script to create Payments table

### Modified Files
- `Hospitality\Components\Pages\RoomSelection.razor` - Now calculates pricing with loyalty discounts, passes data to payment instead of creating booking
- `Hospitality\Components\Pages\Payment.razor` - Supports downpayment options, creates booking only after payment
- `Hospitality\Components\Pages\NewBooking.razor` - Simplified form, displays loyalty discount preview
- `Hospitality\MauiProgram.cs` - Registered PaymentService

## Usage Examples

### Making a Booking with Downpayment
1. Client selects dates and guests in NewBooking page
2. Client selects room(s) in RoomSelection page (sees loyalty discount applied)
3. Client chooses "Downpayment (50%)" in Payment page
4. Client completes payment via PayMongo
5. System creates booking with status "partially_paid"
6. Rooms are marked as "Reserved"
7. Client can pay remaining balance later

### Paying Remaining Balance
Navigate to: `/booking/payment/{clientId}/{bookingId}`

The system will:
1. Load existing booking and payment history
2. Calculate remaining balance
3. Process balance payment
4. Update booking status to "confirmed" when fully paid

## API Reference

### PaymentService Methods

```csharp
// Get loyalty discount percentage
decimal GetLoyaltyDiscount(string tier);

// Calculate pricing with loyalty discount
Task<PendingBooking> CalculatePricingAsync(...);

// Create booking with payment (atomic operation)
Task<(int bookingId, bool success, string message)> CreateBookingWithPaymentAsync(...);

// Add payment to existing booking
Task<(bool success, string message)> AddPaymentToBookingAsync(...);

// Get payment summary for booking
Task<BookingPaymentSummary> GetPaymentSummaryAsync(int bookingId, decimal totalAmount);

// Get minimum downpayment (50%)
decimal GetMinimumDownpayment(decimal totalAmount);
```

## Testing

1. Run `PaymentsSetup.sql` in your database
2. Create a new booking as a logged-in client
3. Verify loyalty discount is displayed
4. Choose downpayment option
5. Complete payment
6. Verify:
   - Booking is created in database
   - Payment is recorded in Payments table
   - Rooms are marked as Reserved
   - Loyalty points are awarded

