# Automatic Room Status Management

## Overview
The system now automatically updates room statuses based on booking lifecycle events and dates.

## Features Implemented

### 1. **Automatic Status on Booking Creation**
When a room is added to a booking (`AddRoomToBookingAsync`):
- Room status is automatically set to **"Reserved"**
- Transaction ensures data consistency
- Console logging for tracking

### 2. **Automatic Status on Check-In**
When a guest checks in (`CheckInBookingAsync`):
- Room status is automatically updated to **"Occupied"**
- Booking status is updated to **"checked-in"**
- All rooms in the booking are updated

### 3. **Automatic Status on Check-Out/Completion**
When a booking is completed (`CompleteBookingAsync`):
- Room status is automatically set back to **"Available"**
- Loyalty points are awarded
- Booking status is set to **"completed"**

### 4. **Automatic Status on Cancellation**
When a booking is cancelled (`CancelBookingAsync`):
- Room status is immediately returned to **"Available"**
- Booking status is set to **"cancelled"**
- Makes rooms available for new bookings

### 5. **Date-Based Auto-Update**
The system includes `UpdateRoomStatusesBasedOnDatesAsync()` which:
- **Automatically sets rooms to "Occupied"** for bookings where check-in date has passed and check-out is in the future
- **Automatically sets rooms to "Available"** when check-out date has passed
- **Auto-completes bookings** that are past their check-out date
- Runs every time the Rooms page is loaded

## Room Status Flow

```
Available
   ?
[Client books room]
   ?
Reserved
   ?
[Check-in date arrives OR manual check-in]
   ?
Occupied
   ?
[Check-out date passes OR manual completion]
   ?
Available
```

## New Methods in BookingService

### `AddRoomToBookingAsync(int bookingId, int roomId)`
- Sets room to "Reserved" when added to booking
- Uses transaction for data consistency

### `CheckInBookingAsync(int bookingId)`
- Sets all rooms in booking to "Occupied"
- Updates booking status to "checked-in"

### `CompleteBookingAsync(int bookingId)`
- Sets all rooms back to "Available"
- Awards loyalty points
- Updates booking status to "completed"

### `CancelBookingAsync(int bookingId)`
- Returns all rooms to "Available" immediately
- Updates booking status to "cancelled"

### `UpdateRoomStatusesBasedOnDatesAsync()`
- Automatically updates room statuses based on current date
- Runs when Rooms page loads
- Handles:
  - Setting rooms to Occupied on check-in date
  - Setting rooms to Available after check-out date
  - Auto-completing past bookings

## Integration Points

### Rooms.razor
- Calls `UpdateRoomStatusesBasedOnDatesAsync()` on page load
- Ensures room list always shows current statuses

### Payment.razor / BookingConfirmation.razor
- Should call `AddRoomToBookingAsync()` when booking is confirmed
- Room automatically becomes "Reserved"

### Admin Pages
- Can call `CheckInBookingAsync()` for manual check-in
- Can call `CompleteBookingAsync()` for manual completion
- Can call `CancelBookingAsync()` for cancellations

## Database Requirements

Ensure your database has these columns:
- `rooms.room_status` (varchar) - Possible values: "Available", "Reserved", "Occupied", "Cleaning", "Maintenance"
- `Bookings.booking_status` (varchar) - Possible values: "Confirmed", "checked-in", "completed", "cancelled"

## Console Logging

All status changes are logged to console:
- ? Room X added to booking Y and marked as Reserved
- ? Room X set to Occupied for booking Y
- ? Room X set back to Available
- ? Updated X rooms to Occupied status
- ? Auto-completed X past bookings
- ? Error messages when operations fail

## Usage Example

```csharp
// When client makes a booking
var bookingId = await BookingService.CreateBookingAsync(clientId, checkIn, checkOut, personCount);
await BookingService.AddRoomToBookingAsync(bookingId, roomId); // Room becomes "Reserved"

// On check-in (manual or automatic)
await BookingService.CheckInBookingAsync(bookingId); // Room becomes "Occupied"

// On completion (manual or automatic)
await BookingService.CompleteBookingAsync(bookingId); // Room becomes "Available"

// Or cancel
await BookingService.CancelBookingAsync(bookingId); // Room returns to "Available"
```

## Benefits

1. **No Manual Updates Required** - Room statuses update automatically
2. **Data Consistency** - Transaction-based updates ensure reliability
3. **Real-Time Accuracy** - Date-based updates keep status current
4. **Audit Trail** - Console logging tracks all status changes
5. **Error Handling** - Proper exception handling prevents partial updates

## Testing Checklist

- [ ] Book a room ? Verify status changes to "Reserved"
- [ ] Check-in date arrives ? Verify status changes to "Occupied"
- [ ] Check-out date passes ? Verify status changes to "Available"
- [ ] Cancel a booking ? Verify status immediately returns to "Available"
- [ ] Manual check-in ? Verify status changes to "Occupied"
- [ ] Manual completion ? Verify status changes to "Available"
- [ ] Load Rooms page ? Verify statuses are up-to-date

## Notes

- The auto-update runs on Rooms page load - consider adding a background task for production
- All database operations use transactions to ensure data consistency
- Room status changes are logged for debugging and audit purposes
- The system handles multiple rooms per booking correctly
