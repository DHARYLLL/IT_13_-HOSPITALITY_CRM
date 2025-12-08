using Hospitality.Database;
using Hospitality.Models;
using Microsoft.Data.SqlClient;

namespace Hospitality.Services;

public class BookingService
{
    public async Task<List<Booking>> GetBookingsByUserIdAsync(int userId)
    {
        var bookings = new List<Booking>();

        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Get client_id from user_id
        string clientIdSql = "SELECT client_id FROM clients WHERE user_id = @userId";
        using var clientCmd = new SqlCommand(clientIdSql, con);
        clientCmd.Parameters.AddWithValue("@userId", userId);
        var clientIdResult = await clientCmd.ExecuteScalarAsync();

        if (clientIdResult == null)
        {
            return bookings; // User is not a client or client record doesn't exist
        }

        int clientId = Convert.ToInt32(clientIdResult);

        // Get bookings with room details - INCLUDE booking_status
        string sql = @"
        SELECT 
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time], b.booking_status,
        r.room_name, r.room_number, r.room_floor, r.room_price
        FROM Bookings b
        LEFT JOIN Booking_rooms br ON b.booking_id = br.booking_id
        LEFT JOIN rooms r ON br.room_id = r.room_id 
        WHERE b.client_id = @clientId
        ORDER BY b.[check-in_date] DESC";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", clientId);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            bookings.Add(MapBooking(reader));
        }

        return bookings;
    }

    public async Task<Booking?> GetCurrentBookingByUserIdAsync(int userId)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Get client_id from user_id
        string clientIdSql = "SELECT client_id FROM clients WHERE user_id = @userId";
        using var clientCmd = new SqlCommand(clientIdSql, con);
        clientCmd.Parameters.AddWithValue("@userId", userId);
        var clientIdResult = await clientCmd.ExecuteScalarAsync();

        if (clientIdResult == null)
        {
            return null;
        }

        int clientId = Convert.ToInt32(clientIdResult);

        // Get current/active booking (check-in date is today or in the future) - INCLUDE booking_status
        string sql = @"
        SELECT TOP 1
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time], b.booking_status,
        r.room_name, r.room_number, r.room_floor, r.room_price
        FROM Bookings b
        LEFT JOIN Booking_rooms br ON b.booking_id = br.booking_id
        LEFT JOIN rooms r ON br.room_id = r.room_id
        WHERE b.client_id = @clientId
        AND b.[check-out_date] >= CAST(GETDATE() AS DATE)
        AND (b.booking_status IS NULL OR b.booking_status NOT IN ('cancelled', 'completed'))
        ORDER BY b.[check-in_date] ASC";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", clientId);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapBooking(reader);
        }

        return null;
    }

    public async Task<int> CreateBookingAsync(int clientId, DateTime checkIn, DateTime checkOut, int personCount, string? clientRequest = null)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        using var tx = await con.BeginTransactionAsync();

        try
        {
            // Insert into Bookings table including time fields
            string sql = @"INSERT INTO Bookings (client_id, [check-in_date], [check-out_date], person_count, client_request, [check-in_time], [check-out_time])
            VALUES (@clientId, @checkIn, @checkOut, @personCount, @clientRequest, @checkInTime, @checkOutTime); 
            SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var cmd = new SqlCommand(sql, con, (SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@clientId", clientId);
            cmd.Parameters.AddWithValue("@checkIn", checkIn);
            cmd.Parameters.AddWithValue("@checkOut", checkOut);
            cmd.Parameters.AddWithValue("@personCount", personCount);
            cmd.Parameters.AddWithValue("@clientRequest", (object?)clientRequest ?? DBNull.Value);

            // Add default check-in time (3:00 PM) and check-out time (11:00 AM)
            cmd.Parameters.AddWithValue("@checkInTime", TimeOnly.FromTimeSpan(new TimeSpan(15, 0, 0))); // 3:00 PM
            cmd.Parameters.AddWithValue("@checkOutTime", TimeOnly.FromTimeSpan(new TimeSpan(11, 0, 0))); // 11:00 AM

            var result = await cmd.ExecuteScalarAsync();
            int bookingId = Convert.ToInt32(result);

            await ((SqlTransaction)tx).CommitAsync();
            return bookingId;
        }
        catch
        {
            await ((SqlTransaction)tx).RollbackAsync();
            throw;
        }
    }

    public async Task<int> AddRoomToBookingAsync(int bookingId, int roomId)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        using var tx = await con.BeginTransactionAsync();

        try
        {
            // Insert into Booking_rooms table
            string insertSql = @"INSERT INTO Booking_rooms (booking_id, room_id)
            VALUES (@bookingId, @roomId); 
            SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var insertCmd = new SqlCommand(insertSql, con, (SqlTransaction)tx);
            insertCmd.Parameters.AddWithValue("@bookingId", bookingId);
            insertCmd.Parameters.AddWithValue("@roomId", roomId);

            var result = await insertCmd.ExecuteScalarAsync();
            int bookingRoomId = Convert.ToInt32(result);

            // Update room status to Reserved
            string updateStatusSql = @"UPDATE rooms SET room_status = 'Reserved' WHERE room_id = @roomId";
            using var updateCmd = new SqlCommand(updateStatusSql, con, (SqlTransaction)tx);
            updateCmd.Parameters.AddWithValue("@roomId", roomId);
            await updateCmd.ExecuteNonQueryAsync();

            await ((SqlTransaction)tx).CommitAsync();

            Console.WriteLine($"? Room {roomId} added to booking {bookingId} and marked as Reserved");
            return bookingRoomId;
        }
        catch
        {
            await ((SqlTransaction)tx).RollbackAsync();
            throw;
        }
    }

    public async Task<List<Room>> GetAvailableRoomsAsync(DateTime checkIn, DateTime checkOut, int personCount)
    {
        var rooms = new List<Room>();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Get rooms that are not booked for the specified dates
        string sql = @"
        SELECT r.room_id, r.room_name, r.room_number, r.room_floor, r.room_price, 
        r.room_status, r.room_picture, r.room_amenities
        FROM rooms r
        WHERE r.room_status = 'Available'
        AND r.room_id NOT IN (
        SELECT DISTINCT br.room_id 
        FROM Booking_rooms br
        INNER JOIN Bookings b ON br.booking_id = b.booking_id
        WHERE (b.[check-in_date] <= @checkOut AND b.[check-out_date] >= @checkIn)
        )
        ORDER BY r.room_floor, r.room_number";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@checkIn", checkIn);
        cmd.Parameters.AddWithValue("@checkOut", checkOut);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rooms.Add(new Room
            {
                room_id = reader.GetInt32(reader.GetOrdinal("room_id")),
                room_name = reader.GetString(reader.GetOrdinal("room_name")),
                room_number = reader.GetInt32(reader.GetOrdinal("room_number")),
                room_floor = reader.GetInt32(reader.GetOrdinal("room_floor")),
                room_price = reader.GetDecimal(reader.GetOrdinal("room_price")),
                //room_available = reader.GetString(reader.GetOrdinal("room_available")),
                room_status = reader.GetString(reader.GetOrdinal("room_status")),
                room_picture = reader.IsDBNull(reader.GetOrdinal("room_picture")) ? null : (byte[])reader["room_picture"],
                room_amenities = reader.IsDBNull(reader.GetOrdinal("room_amenities")) ? null : reader.GetString(reader.GetOrdinal("room_amenities"))
            });
        }

        return rooms;
    }

    private static Booking MapBooking(SqlDataReader reader)
    {
        // Check if booking_status column exists in the result set
        string bookingStatus = "Confirmed"; // Default
        try
        {
            int statusOrdinal = reader.GetOrdinal("booking_status");
            if (!reader.IsDBNull(statusOrdinal))
            {
                bookingStatus = reader.GetString(statusOrdinal);
            }
        }
        catch
        {
            // Column doesn't exist, use default
        }

        var booking = new Booking
        {
            booking_id = reader.GetInt32(reader.GetOrdinal("booking_id")),
            client_id = reader.GetInt32(reader.GetOrdinal("client_id")),
            check_in_date = reader.GetDateTime(reader.GetOrdinal("check-in_date")),
            check_out_date = reader.GetDateTime(reader.GetOrdinal("check-out_date")),
            person_count = reader.GetInt32(reader.GetOrdinal("person_count")),
            client_request = reader.IsDBNull(reader.GetOrdinal("client_request")) ? null : reader.GetString(reader.GetOrdinal("client_request")),
            check_in_time = reader.IsDBNull(reader.GetOrdinal("check-in_time")) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(reader.GetOrdinal("check-in_time"))),
            check_out_time = reader.IsDBNull(reader.GetOrdinal("check-out_time")) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(reader.GetOrdinal("check-out_time"))),
            room_name = reader.IsDBNull(reader.GetOrdinal("room_name")) ? null : reader.GetString(reader.GetOrdinal("room_name")),
            room_number = reader.IsDBNull(reader.GetOrdinal("room_number")) ? null : reader.GetInt32(reader.GetOrdinal("room_number")),
            room_floor = reader.IsDBNull(reader.GetOrdinal("room_floor")) ? null : reader.GetInt32(reader.GetOrdinal("room_floor")),
            room_price = reader.IsDBNull(reader.GetOrdinal("room_price")) ? null : reader.GetDecimal(reader.GetOrdinal("room_price")),

            // Set derived fields
            booking_status = bookingStatus,
        };

        // Set guest_count as alias for person_count
        booking.guest_count = booking.person_count;

        return booking;
    }

    public async Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Get booking with room details by booking ID - INCLUDE booking_status
        string sql = @"
        SELECT 
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time], b.booking_status,
        r.room_name, r.room_number, r.room_floor, r.room_price
        FROM Bookings b
        LEFT JOIN Booking_rooms br ON b.booking_id = br.booking_id
        LEFT JOIN rooms r ON br.room_id = r.room_id
        WHERE b.booking_id = @bookingId";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@bookingId", bookingId);

        using var reader = await cmd.ExecuteReaderAsync();

        Booking? booking = null;
        var roomNames = new List<string>();
        decimal totalRoomPrice = 0;

        while (await reader.ReadAsync())
        {
            if (booking == null)
            {
                booking = MapBooking(reader);
            }

            // Collect room information
            if (!reader.IsDBNull(reader.GetOrdinal("room_name")))
            {
                var roomName = reader.GetString(reader.GetOrdinal("room_name"));
                var roomNumber = reader.GetInt32(reader.GetOrdinal("room_number"));
                var roomPrice = reader.GetDecimal(reader.GetOrdinal("room_price"));

                roomNames.Add($"{roomName} (Room {roomNumber})");
                totalRoomPrice += roomPrice;
            }
        }

        // Update booking with aggregated room information
        if (booking != null && roomNames.Any())
        {
            booking.room_name = string.Join(", ", roomNames);
            booking.room_price = totalRoomPrice;
        }

        return booking;
    }

    public async Task<int> CreateBookingAsync(int clientId, DateTime checkIn, DateTime checkOut, int personCount, string? clientRequest, TimeOnly checkInTime, TimeOnly checkOutTime)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        using var tx = await con.BeginTransactionAsync();

        try
        {
            // Insert into Bookings table including time fields
            string sql = @"INSERT INTO Bookings (client_id, [check-in_date], [check-out_date], person_count, client_request, [check-in_time], [check-out_time])
            VALUES (@clientId, @checkIn, @checkOut, @personCount, @clientRequest, @checkInTime, @checkOutTime); 
            SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var cmd = new SqlCommand(sql, con, (SqlTransaction)tx);
            cmd.Parameters.AddWithValue("@clientId", clientId);
            cmd.Parameters.AddWithValue("@checkIn", checkIn);
            cmd.Parameters.AddWithValue("@checkOut", checkOut);
            cmd.Parameters.AddWithValue("@personCount", personCount);
            cmd.Parameters.AddWithValue("@clientRequest", (object?)clientRequest ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@checkInTime", checkInTime);
            cmd.Parameters.AddWithValue("@checkOutTime", checkOutTime);

            var result = await cmd.ExecuteScalarAsync();
            int bookingId = Convert.ToInt32(result);

            await ((SqlTransaction)tx).CommitAsync();
            return bookingId;
        }
        catch
        {
            await ((SqlTransaction)tx).RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Completes a booking and awards loyalty points
    /// </summary>
    public async Task<bool> CompleteBookingAsync(int bookingId)
    {
        try
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            using var tx = await con.BeginTransactionAsync();

            try
            {
                // Get booking details including total amount and room IDs
                string getBookingSql = @"
                SELECT b.client_id, b.booking_id, SUM(r.room_price) as total_amount, 
                DATEDIFF(day, b.[check-in_date], b.[check-out_date]) as nights,
                br.room_id
                FROM Bookings b
                INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
                INNER JOIN rooms r ON br.room_id = r.room_id
                WHERE b.booking_id = @bookingId
                GROUP BY b.client_id, b.booking_id, b.[check-in_date], b.[check-out_date], br.room_id";

                using var getCmd = new SqlCommand(getBookingSql, con, (SqlTransaction)tx);
                getCmd.Parameters.AddWithValue("@bookingId", bookingId);

                var roomIds = new List<int>();
                int clientId = 0;
                decimal totalAmount = 0;

                using var reader = await getCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return false; // Booking not found
                }

                clientId = reader.GetInt32(0);
                decimal roomPricePerNight = reader.GetDecimal(2);
                int nights = reader.GetInt32(3);
                roomIds.Add(reader.GetInt32(4));
                totalAmount = roomPricePerNight * nights;

                await reader.CloseAsync();

                // Update booking status to completed
                string updateBookingSql = @"
                UPDATE Bookings 
                SET booking_status = 'completed' 
                WHERE booking_id = @bookingId";

                using var updateBookingCmd = new SqlCommand(updateBookingSql, con, (SqlTransaction)tx);
                updateBookingCmd.Parameters.AddWithValue("@bookingId", bookingId);
                await updateBookingCmd.ExecuteNonQueryAsync();

                // Update all rooms in this booking back to Available
                foreach (var roomId in roomIds)
                {
                    string updateRoomSql = @"UPDATE rooms SET room_status = 'Available' WHERE room_id = @roomId";
                    using var updateRoomCmd = new SqlCommand(updateRoomSql, con, (SqlTransaction)tx);
                    updateRoomCmd.Parameters.AddWithValue("@roomId", roomId);
                    await updateRoomCmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"? Room {roomId} set back to Available");
                }

                // Award loyalty points
                var loyaltyService = new LoyaltyService();
                await loyaltyService.AddPointsForBookingAsync(clientId, bookingId, totalAmount);

                await ((SqlTransaction)tx).CommitAsync();

                Console.WriteLine($"? Booking {bookingId} completed. Total: ${totalAmount}, Points awarded to client {clientId}");
                return true;
            }
            catch
            {
                await ((SqlTransaction)tx).RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error completing booking: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks in a booking and sets room status to Occupied
    /// </summary>
    public async Task<bool> CheckInBookingAsync(int bookingId)
    {
        try
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            using var tx = await con.BeginTransactionAsync();

            try
            {
                // Get room IDs for this booking
                string getRoomsSql = @"
                SELECT br.room_id
                FROM Booking_rooms br
                WHERE br.booking_id = @bookingId";

                using var getRoomsCmd = new SqlCommand(getRoomsSql, con, (SqlTransaction)tx);
                getRoomsCmd.Parameters.AddWithValue("@bookingId", bookingId);

                var roomIds = new List<int>();
                using var reader = await getRoomsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    roomIds.Add(reader.GetInt32(0));
                }
                await reader.CloseAsync();

                if (roomIds.Count == 0)
                {
                    return false; // No rooms found for this booking
                }

                // Update all rooms to Occupied status
                foreach (var roomId in roomIds)
                {
                    string updateRoomSql = @"UPDATE rooms SET room_status = 'Occupied' WHERE room_id = @roomId";
                    using var updateRoomCmd = new SqlCommand(updateRoomSql, con, (SqlTransaction)tx);
                    updateRoomCmd.Parameters.AddWithValue("@roomId", roomId);
                    await updateRoomCmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"? Room {roomId} set to Occupied for booking {bookingId}");
                }

                // Update booking status to 'checked-in' if you have this status
                string updateBookingSql = @"
                UPDATE Bookings 
                SET booking_status = 'checked-in' 
                WHERE booking_id = @bookingId";

                using var updateBookingCmd = new SqlCommand(updateBookingSql, con, (SqlTransaction)tx);
                updateBookingCmd.Parameters.AddWithValue("@bookingId", bookingId);
                await updateBookingCmd.ExecuteNonQueryAsync();

                await ((SqlTransaction)tx).CommitAsync();

                Console.WriteLine($"? Booking {bookingId} checked in successfully");
                return true;
            }
            catch
            {
                await ((SqlTransaction)tx).RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error checking in booking: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Cancels a booking, returns rooms to Available status, and sends confirmation message
    /// </summary>
    public async Task<bool> CancelBookingAsync(int bookingId, string? cancellationReason = null)
    {
        try
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            using var tx = await con.BeginTransactionAsync();

            try
            {
                // Get booking and client info for the confirmation message
                string getBookingInfoSql = @"
                SELECT b.client_id, b.[check-in_date], b.[check-out_date], r.room_name,
                SUM(r.room_price * DATEDIFF(day, b.[check-in_date], b.[check-out_date])) as total_amount
                FROM Bookings b
                LEFT JOIN Booking_rooms br ON b.booking_id = br.booking_id
                LEFT JOIN rooms r ON br.room_id = r.room_id
                WHERE b.booking_id = @bookingId
                GROUP BY b.client_id, b.[check-in_date], b.[check-out_date], r.room_name";

                using var getInfoCmd = new SqlCommand(getBookingInfoSql, con, (SqlTransaction)tx);
                getInfoCmd.Parameters.AddWithValue("@bookingId", bookingId);

                int clientId = 0;
                DateTime checkInDate = DateTime.Today;
                DateTime checkOutDate = DateTime.Today;
                string roomName = "Room";
                decimal totalAmount = 0;

                using var infoReader = await getInfoCmd.ExecuteReaderAsync();
                if (await infoReader.ReadAsync())
                {
                    clientId = infoReader.GetInt32(0);
                    checkInDate = infoReader.GetDateTime(1);
                    checkOutDate = infoReader.GetDateTime(2);
                    roomName = infoReader.IsDBNull(3) ? "Room" : infoReader.GetString(3);
                    totalAmount = infoReader.IsDBNull(4) ? 0 : infoReader.GetDecimal(4);
                }
                await infoReader.CloseAsync();

                // Get room IDs for this booking
                string getRoomsSql = @"
                SELECT br.room_id
                FROM Booking_rooms br
                WHERE br.booking_id = @bookingId";

                using var getRoomsCmd = new SqlCommand(getRoomsSql, con, (SqlTransaction)tx);
                getRoomsCmd.Parameters.AddWithValue("@bookingId", bookingId);

                var roomIds = new List<int>();
                using var reader = await getRoomsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    roomIds.Add(reader.GetInt32(0));
                }
                await reader.CloseAsync();

                // Update all rooms back to Available status
                foreach (var roomId in roomIds)
                {
                    string updateRoomSql = @"UPDATE rooms SET room_status = 'Available' WHERE room_id = @roomId";
                    using var updateRoomCmd = new SqlCommand(updateRoomSql, con, (SqlTransaction)tx);
                    updateRoomCmd.Parameters.AddWithValue("@roomId", roomId);
                    await updateRoomCmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"? Room {roomId} set back to Available (booking cancelled)");
                }

                // Update booking status to cancelled
                string updateBookingSql = @"
                UPDATE Bookings 
                SET booking_status = 'cancelled' 
                WHERE booking_id = @bookingId";

                using var updateBookingCmd = new SqlCommand(updateBookingSql, con, (SqlTransaction)tx);
                updateBookingCmd.Parameters.AddWithValue("@bookingId", bookingId);
                await updateBookingCmd.ExecuteNonQueryAsync();

                // Send cancellation confirmation message to client
                if (clientId > 0)
                {
                    string messageBody = $@"Dear Guest,

Your booking has been successfully cancelled.

?? Cancellation Details:
????????????????????????
• Booking ID: #{bookingId:D7}
• Room: {roomName}
• Check-in: {checkInDate:dddd, MMMM dd, yyyy}
• Check-out: {checkOutDate:dddd, MMMM dd, yyyy}
• Original Amount: ?{totalAmount:N2}
{(string.IsNullOrEmpty(cancellationReason) ? "" : $"• Reason: {cancellationReason}")}

?? Refund Information:
????????????????????????
Your refund will be processed according to our cancellation policy. Please allow 5-7 business days for the refund to appear in your account.

If you have any questions about your cancellation or refund, please don't hesitate to contact us.

We hope to welcome you at InnSight Hotels in the future!

Best regards,
InnSight Hotels Team";

                    string insertMessageSql = @"
                    INSERT INTO Messages (client_id, booking_id, message_subject, message_body, message_type, message_category, regarding_text, sent_date, is_read)
                    VALUES (@clientId, @bookingId, @subject, @body, 'service', 'Booking Cancellation', 'Booking Cancellation Confirmation', GETDATE(), 0)";

                    using var msgCmd = new SqlCommand(insertMessageSql, con, (SqlTransaction)tx);
                    msgCmd.Parameters.AddWithValue("@clientId", clientId);
                    msgCmd.Parameters.AddWithValue("@bookingId", bookingId);
                    msgCmd.Parameters.AddWithValue("@subject", $"Booking #{bookingId:D7} Cancellation Confirmed");
                    msgCmd.Parameters.AddWithValue("@body", messageBody);
                    await msgCmd.ExecuteNonQueryAsync();

                    Console.WriteLine($"?? Cancellation confirmation message sent to client {clientId}");
                }

                await ((SqlTransaction)tx).CommitAsync();

                Console.WriteLine($"? Booking {bookingId} cancelled successfully");
                return true;
            }
            catch
            {
                await ((SqlTransaction)tx).RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error cancelling booking: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Updates booking status and records payment information
    /// </summary>
    public async Task<bool> UpdateBookingStatusAsync(int bookingId, string status, string? paymentIntentId = null, string? paymentMethod = null)
    {
        try
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
            UPDATE Bookings 
            SET booking_status = @status
            WHERE booking_id = @bookingId";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@bookingId", bookingId);
            cmd.Parameters.AddWithValue("@status", status);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected > 0)
            {
                Console.WriteLine($"? Booking {bookingId} status updated to: {status}");
                
                if (!string.IsNullOrEmpty(paymentIntentId))
                {
                   Console.WriteLine($"  Payment Intent ID: {paymentIntentId}");
                 }
                
             if (!string.IsNullOrEmpty(paymentMethod))
          {
      Console.WriteLine($"  Payment Method: {paymentMethod}");
     }
    
       return true;
    }

 return false;
        }
        catch (Exception ex)
        {
 Console.WriteLine($"? Error updating booking status: {ex.Message}");
            return false;
      }
    }

    /// <summary>
    /// Gets total amount for a booking
    /// </summary>
    public async Task<decimal> GetBookingTotalAsync(int bookingId)
    {
        try
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
            SELECT SUM(r.room_price) * DATEDIFF(day, b.[check-in_date], b.[check-out_date]) as total_amount
            FROM Bookings b
            INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
            INNER JOIN rooms r ON br.room_id = r.room_id
            WHERE b.booking_id = @bookingId
            GROUP BY b.[check-in_date], b.[check-out_date]";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@bookingId", bookingId);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToDecimal(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets all bookings with client information for admin reports
    /// </summary>
    public async Task<List<Booking>> GetAllBookingsWithClientsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var bookings = new List<Booking>();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        string sql = @"
        SELECT 
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time], b.booking_status,
        r.room_name, r.room_number, r.room_floor, r.room_price,
        c.first_name, c.last_name, c.email,
        DATEDIFF(day, b.[check-in_date], b.[check-out_date]) as nights
        FROM Bookings b
        INNER JOIN clients c ON b.client_id = c.client_id
        LEFT JOIN Booking_rooms br ON b.booking_id = br.booking_id
        LEFT JOIN rooms r ON br.room_id = r.room_id
        WHERE (@startDate IS NULL OR b.[check-in_date] >= @startDate)
        AND (@endDate IS NULL OR b.[check-in_date] <= @endDate)
        ORDER BY b.[check-in_date] DESC";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@startDate", (object?)startDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@endDate", (object?)endDate ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync();

        var bookingDict = new Dictionary<int, Booking>();

        while (await reader.ReadAsync())
        {
            int bookingId = reader.GetInt32(reader.GetOrdinal("booking_id"));

            if (!bookingDict.ContainsKey(bookingId))
            {
                var booking = MapBooking(reader);
                booking.client_first_name = reader.GetString(reader.GetOrdinal("first_name"));
                booking.client_last_name = reader.GetString(reader.GetOrdinal("last_name"));
                booking.client_email = reader.GetString(reader.GetOrdinal("email"));

                int nights = reader.GetInt32(reader.GetOrdinal("nights"));
                decimal roomPrice = reader.IsDBNull(reader.GetOrdinal("room_price")) ? 0 : reader.GetDecimal(reader.GetOrdinal("room_price"));
                booking.total_amount = roomPrice * nights;

                bookingDict[bookingId] = booking;
            }
            else
            {
                // Multiple rooms - add to total
                decimal roomPrice = reader.IsDBNull(reader.GetOrdinal("room_price")) ? 0 : reader.GetDecimal(reader.GetOrdinal("room_price"));
                int nights = reader.GetInt32(reader.GetOrdinal("nights"));
                bookingDict[bookingId].total_amount += roomPrice * nights;
            }
        }

        return bookingDict.Values.ToList();
    }

    /// <summary>
    /// Gets booking metrics for a date range
    /// </summary>
    public async Task<BookingMetrics> GetBookingMetricsAsync(int dateRangeDays)
    {
        var metrics = new BookingMetrics();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        DateTime startDate = DateTime.Today.AddDays(-dateRangeDays);
        DateTime endDate = DateTime.Today.AddDays(dateRangeDays); // Extended to include FUTURE bookings

        // Get total bookings and revenue
        string sql = @"
        SELECT 
        COUNT(DISTINCT b.booking_id) as total_bookings,
        SUM(r.room_price * DATEDIFF(day, b.[check-in_date], b.[check-out_date])) as total_revenue,
        COUNT(DISTINCT b.client_id) as total_guests,
        AVG(CAST(DATEDIFF(day, b.[check-in_date], b.[check-out_date]) AS DECIMAL(10,2))) as avg_stay_duration,
        AVG(r.room_price) as avg_daily_rate
        FROM Bookings b
        INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
        INNER JOIN rooms r ON br.room_id = r.room_id
        WHERE b.[check-in_date] >= @startDate AND b.[check-in_date] <= @endDate";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@startDate", startDate);
        cmd.Parameters.AddWithValue("@endDate", endDate);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            metrics.TotalBookings = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            metrics.TotalRevenue = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
            metrics.TotalGuests = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            metrics.AvgStayDuration = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
            metrics.AverageDailyRate = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
        }

        return metrics;
    }

    /// <summary>
    /// Gets daily revenue breakdown for charts
    /// </summary>
    public async Task<List<DailyRevenue>> GetDailyRevenueAsync(int dateRangeDays)
    {
        var dailyRevenue = new List<DailyRevenue>();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        DateTime startDate = DateTime.Today.AddDays(-dateRangeDays);

        string sql = @"
        SELECT 
        b.[check-in_date] as date,
        SUM(r.room_price * DATEDIFF(day, b.[check-in_date], b.[check-out_date])) as revenue
        FROM Bookings b
        INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
        INNER JOIN rooms r ON br.room_id = r.room_id
        WHERE b.[check-in_date] >= @startDate
        GROUP BY b.[check-in_date]
        ORDER BY b.[check-in_date]";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@startDate", startDate);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            dailyRevenue.Add(new DailyRevenue
            {
                Date = reader.GetDateTime(0),
                Revenue = reader.GetDecimal(1)
            });
        }

        return dailyRevenue;
    }

    /// <summary>
    /// Updates room statuses based on booking dates
    /// Should be called periodically or when viewing room list
    /// </summary>
    public async Task UpdateRoomStatusesBasedOnDatesAsync()
    {
        try
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            var today = DateTime.Today;

            // First, fix any NULL booking_status values - set them to 'confirmed'
            string fixNullStatusSql = @"
            UPDATE Bookings 
            SET booking_status = 'confirmed' 
            WHERE booking_status IS NULL";

            using var fixNullCmd = new SqlCommand(fixNullStatusSql, con);
            var fixedCount = await fixNullCmd.ExecuteNonQueryAsync();

            if (fixedCount > 0)
            {
                Console.WriteLine($"? Fixed {fixedCount} bookings with NULL status");
            }

            // Set rooms to Reserved for future bookings (check-in date is in future)
            string reservedSql = @"
            UPDATE r
            SET r.room_status = 'Reserved'
            FROM rooms r
            INNER JOIN Booking_rooms br ON r.room_id = br.room_id
            INNER JOIN Bookings b ON br.booking_id = b.booking_id
            WHERE b.[check-in_date] > @today 
            AND b.booking_status NOT IN ('cancelled', 'completed')
            AND r.room_status = 'Available'";

            using var reservedCmd = new SqlCommand(reservedSql, con);
            reservedCmd.Parameters.AddWithValue("@today", today);
            var reservedCount = await reservedCmd.ExecuteNonQueryAsync();

            if (reservedCount > 0)
            {
                Console.WriteLine($"? Updated {reservedCount} rooms to Reserved status (future bookings)");
            }

            // Set rooms to Occupied for bookings that have started (check-in date is today or past, check-out date is future)
            string occupiedSql = @"
            UPDATE r
            SET r.room_status = 'Occupied'
            FROM rooms r
            INNER JOIN Booking_rooms br ON r.room_id = br.room_id
            INNER JOIN Bookings b ON br.booking_id = b.booking_id
            WHERE b.[check-in_date] <= @today 
            AND b.[check-out_date] > @today
            AND b.booking_status NOT IN ('cancelled', 'completed')
            AND r.room_status != 'Occupied'";

            using var occupiedCmd = new SqlCommand(occupiedSql, con);
            occupiedCmd.Parameters.AddWithValue("@today", today);
            var occupiedCount = await occupiedCmd.ExecuteNonQueryAsync();

            if (occupiedCount > 0)
            {
                Console.WriteLine($"? Updated {occupiedCount} rooms to Occupied status");
            }

            // Set rooms back to Available for completed bookings (check-out date is past)
            string availableSql = @"
            UPDATE r
            SET r.room_status = 'Available'
            FROM rooms r
            WHERE r.room_id IN (
            SELECT DISTINCT br.room_id
            FROM Booking_rooms br
            INNER JOIN Bookings b ON br.booking_id = b.booking_id
            WHERE b.[check-out_date] <= @today
            AND b.booking_status NOT IN ('cancelled')
            )
            AND r.room_status IN ('Occupied', 'Reserved')
            AND r.room_id NOT IN (
            SELECT DISTINCT br2.room_id
            FROM Booking_rooms br2
            INNER JOIN Bookings b2 ON br2.booking_id = b2.booking_id
            WHERE b2.[check-in_date] <= @today 
            AND b2.[check-out_date] > @today
            AND b2.booking_status NOT IN ('cancelled', 'completed')
            )";

            using var availableCmd = new SqlCommand(availableSql, con);
            availableCmd.Parameters.AddWithValue("@today", today);
            var availableCount = await availableCmd.ExecuteNonQueryAsync();

            if (availableCount > 0)
            {
                Console.WriteLine($"? Updated {availableCount} rooms to Available status");
            }

            // Auto-complete bookings that have passed their check-out date
            string completeBookingsSql = @"
            UPDATE Bookings
            SET booking_status = 'completed'
            WHERE [check-out_date] < @today
            AND booking_status NOT IN ('cancelled', 'completed')";

            using var completeCmd = new SqlCommand(completeBookingsSql, con);
            completeCmd.Parameters.AddWithValue("@today", today);
            var completedCount = await completeCmd.ExecuteNonQueryAsync();

            if (completedCount > 0)
            {
                Console.WriteLine($"? Auto-completed {completedCount} past bookings");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error updating room statuses: {ex.Message}");
        }
    }
}

public class BookingMetrics
{
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalGuests { get; set; }
    public decimal AvgStayDuration { get; set; }
    public decimal AverageDailyRate { get; set; }
}

public class DailyRevenue
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
}
