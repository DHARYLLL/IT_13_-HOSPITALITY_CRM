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

        // Get bookings with room details
        string sql = @"
        SELECT 
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time],
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

        // Get current/active booking (check-in date is today or in the future)
        string sql = @"
        SELECT TOP 1
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time],
        r.room_name, r.room_number, r.room_floor, r.room_price
        FROM Bookings b
        LEFT JOIN Booking_rooms br ON b.booking_id = br.booking_id
        LEFT JOIN rooms r ON br.room_id = r.room_id
        WHERE b.client_id = @clientId
        AND b.[check-out_date] >= CAST(GETDATE() AS DATE)
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

        string sql = @"INSERT INTO Booking_rooms (booking_id, room_id)
        VALUES (@bookingId, @roomId); 
        SELECT CAST(SCOPE_IDENTITY() AS int);";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@bookingId", bookingId);
        cmd.Parameters.AddWithValue("@roomId", roomId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
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
            booking_status = "Confirmed", // Default status since not in DB
        };

        // Set guest_count as alias for person_count
        booking.guest_count = booking.person_count;

        return booking;
    }

    public async Task<Booking?> GetBookingByIdAsync(int bookingId)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Get booking with room details by booking ID - get first room for basic info
        string sql = @"
        SELECT 
        b.booking_id, b.client_id, b.[check-in_date], b.[check-out_date], b.person_count, b.client_request,
        b.[check-in_time], b.[check-out_time],
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

            // Get booking details including total amount
            string getBookingSql = @"
           SELECT b.client_id, b.booking_id, SUM(r.room_price) as total_amount, DATEDIFF(day, b.[check-in_date], b.[check-out_date]) as nights
 FROM Bookings b
          INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
      INNER JOIN rooms r ON br.room_id = r.room_id
    WHERE b.booking_id = @bookingId
      GROUP BY b.client_id, b.booking_id, b.[check-in_date], b.[check-out_date]";

            using var getCmd = new SqlCommand(getBookingSql, con);
            getCmd.Parameters.AddWithValue("@bookingId", bookingId);

            using var reader = await getCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
     {
    return false; // Booking not found
   }

  int clientId = reader.GetInt32(0);
    decimal roomPricePerNight = reader.GetDecimal(2);
       int nights = reader.GetInt32(3);
         decimal totalAmount = roomPricePerNight * nights;

      await reader.CloseAsync();

            // Update booking status to completed
     string updateSql = @"
                UPDATE Bookings 
      SET booking_status = 'completed' 
         WHERE booking_id = @bookingId";

   using var updateCmd = new SqlCommand(updateSql, con);
            updateCmd.Parameters.AddWithValue("@bookingId", bookingId);
            await updateCmd.ExecuteNonQueryAsync();

 // Award loyalty points
      var loyaltyService = new LoyaltyService();
  await loyaltyService.AddPointsForBookingAsync(clientId, bookingId, totalAmount);

  Console.WriteLine($"? Booking {bookingId} completed. Total: ${totalAmount}, Points awarded to client {clientId}");
     return true;
        }
 catch (Exception ex)
        {
            Console.WriteLine($"? Error completing booking: {ex.Message}");
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
}
