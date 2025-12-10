using Hospitality.Database;
using Hospitality.Models;
using Microsoft.Data.SqlClient;

namespace Hospitality.Services;

public class PaymentService
{
    private readonly LoyaltyService _loyaltyService = new();
    private readonly DualWriteService? _dualWriteService;
    private readonly SyncService? _syncService;

    public PaymentService()
    {
        // Default constructor for backward compatibility
    }

    public PaymentService(DualWriteService dualWriteService, SyncService syncService)
    {
        _dualWriteService = dualWriteService;
        _syncService = syncService;
    }

    /// <summary>
    /// Gets discount percentage based on loyalty tier
    /// </summary>
    public decimal GetLoyaltyDiscount(string tier)
    {
        return tier.ToLower() switch
        {
            "bronze" => 0m,      // No discount
            "silver" => 5m,      // 5% discount
            "gold" => 10m,       // 10% discount
            "platinum" => 15m,   // 15% discount
            _ => 0m
        };
    }

    /// <summary>
    /// Calculates pricing with loyalty discount
    /// </summary>
    public async Task<PendingBooking> CalculatePricingAsync(
        int clientId,
        List<Room> selectedRooms,
        DateTime checkIn,
        DateTime checkOut,
        int personCount,
        string? specialRequests = null)
    {
        // Get client's loyalty tier
        var loyalty = await _loyaltyService.GetLoyaltyProgramAsync(clientId);
        string tier = loyalty?.current_tier ?? "Bronze";
        decimal discountPercentage = GetLoyaltyDiscount(tier);

        int nights = (checkOut - checkIn).Days;
        decimal subtotal = selectedRooms.Sum(r => r.room_price * nights);
        decimal discountAmount = subtotal * (discountPercentage / 100m);
        decimal taxesAndFees = 0m; // No taxes and fees
        decimal totalAmount = subtotal - discountAmount;

        return new PendingBooking
        {
            client_id = clientId,
            check_in_date = checkIn,
            check_out_date = checkOut,
            person_count = personCount,
            check_in_time = new TimeOnly(15, 0),
            check_out_time = new TimeOnly(11, 0),
            room_ids = selectedRooms.Select(r => r.room_id).ToList(),
            special_requests = specialRequests,
            loyalty_tier = tier,
            discount_percentage = discountPercentage,
            subtotal = subtotal,
            discount_amount = discountAmount,
            taxes_and_fees = taxesAndFees,
            total_amount = totalAmount
        };
    }

    /// <summary>
    /// Records a payment for a booking
    /// </summary>
    public async Task<int> RecordPaymentAsync(
    int bookingId,
    decimal amount,
    string paymentMethod,
    string paymentType,
    string? paymentIntentId = null,
    string? checkoutSessionId = null,
    string? notes = null)
    {
      // If DualWriteService is available, use it for dual-write
      if (_dualWriteService != null)
      {
          return await _dualWriteService.ExecuteWriteAsync(
          "Payment",
          "Payments",
          "INSERT",
            async (con, tx) =>
            {
                string sql = @"
            INSERT INTO Payments (booking_id, amount, payment_method, payment_status, 
            payment_intent_id, checkout_session_id, payment_date, payment_type, notes, sync_status)
            VALUES (@bookingId, @amount, @paymentMethod, 'completed', 
            @paymentIntentId, @checkoutSessionId, @paymentDate, @paymentType, @notes, 'pending');
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using var cmd = new SqlCommand(sql, con, tx);
                cmd.Parameters.AddWithValue("@bookingId", bookingId);
                cmd.Parameters.AddWithValue("@amount", amount);
                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                cmd.Parameters.AddWithValue("@paymentIntentId", (object?)paymentIntentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@checkoutSessionId", (object?)checkoutSessionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@paymentDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@paymentType", paymentType);
                cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            });
        }

        // Fallback to original implementation
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        string sql = @"
          INSERT INTO Payments (booking_id, amount, payment_method, payment_status, 
          payment_intent_id, checkout_session_id, payment_date, payment_type, notes)
         VALUES (@bookingId, @amount, @paymentMethod, 'completed', 
                @paymentIntentId, @checkoutSessionId, @paymentDate, @paymentType, @notes);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@bookingId", bookingId);
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
        cmd.Parameters.AddWithValue("@paymentIntentId", (object?)paymentIntentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@checkoutSessionId", (object?)checkoutSessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@paymentDate", DateTime.Now);
        cmd.Parameters.AddWithValue("@paymentType", paymentType);
        cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        int paymentId = Convert.ToInt32(result);

        // Queue for sync
        if (_syncService != null)
        {
            await _syncService.MarkForSyncAsync("Payments", paymentId, "INSERT");
        }

        return paymentId;
    }

    /// <summary>
    /// Gets all payments for a booking
    /// </summary>
    public async Task<List<Payment>> GetPaymentsForBookingAsync(int bookingId)
    {
        var payments = new List<Payment>();

        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        string sql = @"
            SELECT payment_id, booking_id, amount, payment_method, payment_status,
           payment_intent_id, checkout_session_id, payment_date, payment_type, notes
        FROM Payments
            WHERE booking_id = @bookingId
    ORDER BY payment_date DESC";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@bookingId", bookingId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            payments.Add(new Payment
            {
                payment_id = reader.GetInt32(0),
                booking_id = reader.GetInt32(1),
                amount = reader.GetDecimal(2),
                payment_method = reader.GetString(3),
                payment_status = reader.GetString(4),
                payment_intent_id = reader.IsDBNull(5) ? null : reader.GetString(5),
                checkout_session_id = reader.IsDBNull(6) ? null : reader.GetString(6),
                payment_date = reader.GetDateTime(7),
                payment_type = reader.GetString(8),
                notes = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }

        return payments;
    }

    /// <summary>
    /// Gets payment summary for a booking (total paid, remaining balance)
    /// </summary>
    public async Task<BookingPaymentSummary> GetPaymentSummaryAsync(int bookingId, decimal totalAmount)
    {
        var payments = await GetPaymentsForBookingAsync(bookingId);
        var totalPaid = payments.Where(p => p.payment_status == "completed").Sum(p => p.amount);

        return new BookingPaymentSummary
        {
            booking_id = bookingId,
            total_amount = totalAmount,
            total_paid = totalPaid,
            remaining_balance = totalAmount - totalPaid,
            is_fully_paid = totalPaid >= totalAmount,
            payments = payments
        };
    }

    /// <summary>
    /// Creates a booking only after successful payment
    /// This is the main method to finalize a booking with payment
    /// </summary>
    public async Task<(int bookingId, bool success, string message)> CreateBookingWithPaymentAsync(
  PendingBooking pendingBooking,
     decimal paymentAmount,
string paymentMethod,
     string paymentType,
        string? paymentIntentId = null,
        string? checkoutSessionId = null)
    {
        // If DualWriteService is available, use it for dual-write
        if (_dualWriteService != null)
        {
            int createdBookingId = 0;
            int createdPaymentId = 0;

            try
            {
                // Create booking with dual-write
                createdBookingId = await _dualWriteService.ExecuteWriteAsync(
                 "Booking",
                      "Bookings",
                    "INSERT",
                       async (con, tx) =>
                         {
                             string status = paymentType == "full" ? "confirmed" : "partially_paid";

                             string bookingSql = @"
                 INSERT INTO Bookings (client_id, [check-in_date], [check-out_date], person_count, 
       client_request, [check-in_time], [check-out_time], booking_status, sync_status)
       VALUES (@clientId, @checkIn, @checkOut, @personCount, 
        @clientRequest, @checkInTime, @checkOutTime, @status, 'pending');
             SELECT CAST(SCOPE_IDENTITY() AS INT);";

                             using var bookingCmd = new SqlCommand(bookingSql, con, tx);
                             bookingCmd.Parameters.AddWithValue("@clientId", pendingBooking.client_id);
                             bookingCmd.Parameters.AddWithValue("@checkIn", pendingBooking.check_in_date);
                             bookingCmd.Parameters.AddWithValue("@checkOut", pendingBooking.check_out_date);
                             bookingCmd.Parameters.AddWithValue("@personCount", pendingBooking.person_count);
                             bookingCmd.Parameters.AddWithValue("@clientRequest", (object?)pendingBooking.special_requests ?? DBNull.Value);
                             bookingCmd.Parameters.AddWithValue("@checkInTime", pendingBooking.check_in_time);
                             bookingCmd.Parameters.AddWithValue("@checkOutTime", pendingBooking.check_out_time);
                             bookingCmd.Parameters.AddWithValue("@status", status);

                             var result = await bookingCmd.ExecuteScalarAsync();
                             int bookingId = Convert.ToInt32(result);

                             // Add rooms to booking
                             foreach (var roomId in pendingBooking.room_ids)
                             {
                                 string roomSql = @"INSERT INTO Booking_rooms (booking_id, room_id) VALUES (@bookingId, @roomId)";
                                 using var roomCmd = new SqlCommand(roomSql, con, tx);
                                 roomCmd.Parameters.AddWithValue("@bookingId", bookingId);
                                 roomCmd.Parameters.AddWithValue("@roomId", roomId);
                                 await roomCmd.ExecuteNonQueryAsync();

                                 string updateRoomSql = @"UPDATE rooms SET room_status = 'Reserved' WHERE room_id = @roomId";
                                 using var updateRoomCmd = new SqlCommand(updateRoomSql, con, tx);
                                 updateRoomCmd.Parameters.AddWithValue("@roomId", roomId);
                                 await updateRoomCmd.ExecuteNonQueryAsync();
                             }

                             return bookingId;
                         });

                if (createdBookingId > 0)
                {
                    // Create payment with dual-write
                    createdPaymentId = await RecordPaymentAsync(
                     createdBookingId,
                    paymentAmount,
                  paymentMethod,
                paymentType,
                   paymentIntentId,
                        checkoutSessionId,
                       $"{pendingBooking.loyalty_tier} member - {pendingBooking.discount_percentage}% discount applied");

                    Console.WriteLine($"? Booking {createdBookingId} and Payment {createdPaymentId} created with dual-write");
                    return (createdBookingId, true, "Booking created successfully");
                }

                return (0, false, "Failed to create booking");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error creating booking with payment: {ex.Message}");
                return (0, false, ex.Message);
            }
        }

        // Fallback to original implementation
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        using var transaction = con.BeginTransaction();

        try
        {
            // 1. Create the booking
            string bookingSql = @"
     INSERT INTO Bookings (client_id, [check-in_date], [check-out_date], person_count, 
               client_request, [check-in_time], [check-out_time], booking_status)
       VALUES (@clientId, @checkIn, @checkOut, @personCount, 
        @clientRequest, @checkInTime, @checkOutTime, @status);
    SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var bookingCmd = new SqlCommand(bookingSql, con, transaction);
            bookingCmd.Parameters.AddWithValue("@clientId", pendingBooking.client_id);
            bookingCmd.Parameters.AddWithValue("@checkIn", pendingBooking.check_in_date);
            bookingCmd.Parameters.AddWithValue("@checkOut", pendingBooking.check_out_date);
            bookingCmd.Parameters.AddWithValue("@personCount", pendingBooking.person_count);
            bookingCmd.Parameters.AddWithValue("@clientRequest", (object?)pendingBooking.special_requests ?? DBNull.Value);
            bookingCmd.Parameters.AddWithValue("@checkInTime", pendingBooking.check_in_time);
            bookingCmd.Parameters.AddWithValue("@checkOutTime", pendingBooking.check_out_time);

            string status = paymentType == "full" ? "confirmed" : "partially_paid";
            bookingCmd.Parameters.AddWithValue("@status", status);

            var bookingIdResult = await bookingCmd.ExecuteScalarAsync();
            int bookingId = Convert.ToInt32(bookingIdResult);

            Console.WriteLine($"? Created booking {bookingId}");

            // 2. Add rooms to booking and update their status
            foreach (var roomId in pendingBooking.room_ids)
            {
                string roomSql = @"INSERT INTO Booking_rooms (booking_id, room_id) VALUES (@bookingId, @roomId)";
                using var roomCmd = new SqlCommand(roomSql, con, transaction);
                roomCmd.Parameters.AddWithValue("@bookingId", bookingId);
                roomCmd.Parameters.AddWithValue("@roomId", roomId);
                await roomCmd.ExecuteNonQueryAsync();

                string updateRoomSql = @"UPDATE rooms SET room_status = 'Reserved' WHERE room_id = @roomId";
                using var updateRoomCmd = new SqlCommand(updateRoomSql, con, transaction);
                updateRoomCmd.Parameters.AddWithValue("@roomId", roomId);
                await updateRoomCmd.ExecuteNonQueryAsync();

                Console.WriteLine($"? Room {roomId} added to booking and marked as Reserved");
            }

            // 3. Record the payment
            string paymentSql = @"
              INSERT INTO Payments (booking_id, amount, payment_method, payment_status, 
          payment_intent_id, checkout_session_id, payment_date, payment_type, notes)
           VALUES (@bookingId, @amount, @paymentMethod, 'completed', 
      @paymentIntentId, @checkoutSessionId, @paymentDate, @paymentType, @notes);
   SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var paymentCmd = new SqlCommand(paymentSql, con, transaction);
            paymentCmd.Parameters.AddWithValue("@bookingId", bookingId);
            paymentCmd.Parameters.AddWithValue("@amount", paymentAmount);
            paymentCmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
            paymentCmd.Parameters.AddWithValue("@paymentIntentId", (object?)paymentIntentId ?? DBNull.Value);
            paymentCmd.Parameters.AddWithValue("@checkoutSessionId", (object?)checkoutSessionId ?? DBNull.Value);
            paymentCmd.Parameters.AddWithValue("@paymentDate", DateTime.Now);
            paymentCmd.Parameters.AddWithValue("@paymentType", paymentType);

            string paymentNote = $"{pendingBooking.loyalty_tier} member - {pendingBooking.discount_percentage}% discount applied";
            paymentCmd.Parameters.AddWithValue("@notes", paymentNote);

            await paymentCmd.ExecuteScalarAsync();

            Console.WriteLine($"? Payment of {paymentAmount:C} recorded for booking {bookingId}");

            await transaction.CommitAsync();

            return (bookingId, true, "Booking created successfully");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"? Error creating booking with payment: {ex.Message}");
            return (0, false, ex.Message);
        }
    }

    /// <summary>
    /// Adds an additional payment to an existing booking (for partial payments)
    /// </summary>
    public async Task<(bool success, string message)> AddPaymentToBookingAsync(
    int bookingId,
 decimal paymentAmount,
        string paymentMethod,
        string paymentType,
        decimal totalBookingAmount,
    string? paymentIntentId = null,
 string? checkoutSessionId = null)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        using var transaction = con.BeginTransaction();

        try
        {
            // Record the payment
            string paymentSql = @"
         INSERT INTO Payments (booking_id, amount, payment_method, payment_status, 
       payment_intent_id, checkout_session_id, payment_date, payment_type, notes)
  VALUES (@bookingId, @amount, @paymentMethod, 'completed', 
  @paymentIntentId, @checkoutSessionId, @paymentDate, @paymentType, @notes);";

            using var paymentCmd = new SqlCommand(paymentSql, con, transaction);
            paymentCmd.Parameters.AddWithValue("@bookingId", bookingId);
            paymentCmd.Parameters.AddWithValue("@amount", paymentAmount);
            paymentCmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
            paymentCmd.Parameters.AddWithValue("@paymentIntentId", (object?)paymentIntentId ?? DBNull.Value);
            paymentCmd.Parameters.AddWithValue("@checkoutSessionId", (object?)checkoutSessionId ?? DBNull.Value);
            paymentCmd.Parameters.AddWithValue("@paymentDate", DateTime.Now);
            paymentCmd.Parameters.AddWithValue("@paymentType", paymentType);
            paymentCmd.Parameters.AddWithValue("@notes", $"Additional payment - {paymentType}");

            await paymentCmd.ExecuteNonQueryAsync();

            // Check if booking is now fully paid
            string checkSql = @"
      SELECT COALESCE(SUM(amount), 0) 
              FROM Payments 
    WHERE booking_id = @bookingId AND payment_status = 'completed'";

            using var checkCmd = new SqlCommand(checkSql, con, transaction);
            checkCmd.Parameters.AddWithValue("@bookingId", bookingId);

            var totalPaid = Convert.ToDecimal(await checkCmd.ExecuteScalarAsync());

            // Update booking status if fully paid
            if (totalPaid >= totalBookingAmount)
            {
                string updateSql = @"UPDATE Bookings SET booking_status = 'confirmed' WHERE booking_id = @bookingId";
                using var updateCmd = new SqlCommand(updateSql, con, transaction);
                updateCmd.Parameters.AddWithValue("@bookingId", bookingId);
                await updateCmd.ExecuteNonQueryAsync();

                Console.WriteLine($"? Booking {bookingId} is now fully paid and confirmed");
            }

            await transaction.CommitAsync();

            return (true, totalPaid >= totalBookingAmount ? "Booking fully paid" : "Payment recorded");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"? Error adding payment: {ex.Message}");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Gets the minimum downpayment required (50% of total)
    /// </summary>
    public decimal GetMinimumDownpayment(decimal totalAmount)
    {
        return totalAmount * 0.5m; // 50% minimum downpayment
    }

    /// <summary>
    /// Validates if a payment amount is valid
    /// </summary>
    public (bool isValid, string message) ValidatePaymentAmount(decimal paymentAmount, decimal totalAmount, decimal alreadyPaid)
    {
        var remaining = totalAmount - alreadyPaid;
        var minDownpayment = GetMinimumDownpayment(totalAmount);

        if (paymentAmount <= 0)
            return (false, "Payment amount must be greater than 0");

        if (paymentAmount > remaining)
            return (false, $"Payment amount cannot exceed remaining balance of {remaining:C}");

        if (alreadyPaid == 0 && paymentAmount < minDownpayment)
            return (false, $"Minimum downpayment required is {minDownpayment:C} (50% of total)");

        return (true, "Valid payment amount");
    }

    /// <summary>
    /// Gets bookings that have remaining balance to pay
    /// </summary>
    public async Task<List<BookingWithBalance>> GetBookingsWithBalanceAsync(int clientId)
    {
        var bookingsWithBalance = new List<BookingWithBalance>();

        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Get all active bookings for this client with payment info
        string sql = @"
      SELECT 
      b.booking_id,
                b.[check-in_date] as check_in_date,
           b.[check-out_date] as check_out_date,
 b.person_count,
  b.booking_status,
         r.room_name,
    r.room_number,
      r.room_price,
  DATEDIFF(day, b.[check-in_date], b.[check-out_date]) as nights,
    COALESCE((SELECT SUM(p.amount) FROM Payments p WHERE p.booking_id = b.booking_id AND p.payment_status = 'completed'), 0) as total_paid
     FROM Bookings b
     INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
  INNER JOIN rooms r ON br.room_id = r.room_id
   WHERE b.client_id = @clientId
            AND b.booking_status IN ('partially_paid', 'confirmed')
      AND b.[check-in_date] >= CAST(GETDATE() AS DATE)
        ORDER BY b.[check-in_date] ASC";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", clientId);

        var bookingDict = new Dictionary<int, BookingWithBalance>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int bookingId = reader.GetInt32(reader.GetOrdinal("booking_id"));
            decimal roomPrice = reader.GetDecimal(reader.GetOrdinal("room_price"));
            int nights = reader.GetInt32(reader.GetOrdinal("nights"));
            decimal totalPaid = reader.GetDecimal(reader.GetOrdinal("total_paid"));

            if (!bookingDict.ContainsKey(bookingId))
            {
                bookingDict[bookingId] = new BookingWithBalance
                {
                    booking_id = bookingId,
                    check_in_date = reader.GetDateTime(reader.GetOrdinal("check_in_date")),
                    check_out_date = reader.GetDateTime(reader.GetOrdinal("check_out_date")),
                    person_count = reader.GetInt32(reader.GetOrdinal("person_count")),
                    booking_status = reader.GetString(reader.GetOrdinal("booking_status")),
                    room_name = reader.GetString(reader.GetOrdinal("room_name")),
                    room_number = reader.GetInt32(reader.GetOrdinal("room_number")),
                    nights = nights,
                    subtotal = roomPrice * nights,
                    total_paid = totalPaid
                };
            }
            else
            {
                // Multiple rooms - add to subtotal
                bookingDict[bookingId].subtotal += roomPrice * nights;
                bookingDict[bookingId].room_name += $", {reader.GetString(reader.GetOrdinal("room_name"))}";
            }
        }

        // Calculate totals (no taxes) and filter for those with remaining balance
        foreach (var booking in bookingDict.Values)
        {
            booking.taxes_and_fees = 0m; // No taxes and fees
            booking.total_amount = booking.subtotal;
            booking.remaining_balance = booking.total_amount - booking.total_paid;

            // Only include if there's a remaining balance
            if (booking.remaining_balance > 0)
            {
                bookingsWithBalance.Add(booking);
            }
        }

        return bookingsWithBalance;
    }
}

/// <summary>
/// Represents a booking with payment balance information
/// </summary>
public class BookingWithBalance
{
    public int booking_id { get; set; }
    public DateTime check_in_date { get; set; }
    public DateTime check_out_date { get; set; }
    public int person_count { get; set; }
    public string booking_status { get; set; } = "";
    public string room_name { get; set; } = "";
    public int room_number { get; set; }
    public int nights { get; set; }
    public decimal subtotal { get; set; }
    public decimal taxes_and_fees { get; set; }
    public decimal total_amount { get; set; }
    public decimal total_paid { get; set; }
    public decimal remaining_balance { get; set; }
}
