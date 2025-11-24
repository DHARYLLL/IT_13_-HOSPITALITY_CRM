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
    b.booking_id, b.client_id, b.room_id, b.booking_date, 
 b.check_in_date, b.check_out_date, b.guest_count, b.bed_type, b.booking_status,
       r.room_name, r.room_number, r.room_floor, r.room_price
        FROM bookings b
            JOIN rooms r ON b.room_id = r.room_id
          WHERE b.client_id = @clientId
         ORDER BY b.booking_date DESC";

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

        // Get current/active booking (check-in date is today or in the future, and status is confirmed/active)
        string sql = @"
         SELECT TOP 1
  b.booking_id, b.client_id, b.room_id, b.booking_date, 
       b.check_in_date, b.check_out_date, b.guest_count, b.bed_type, b.booking_status,
     r.room_name, r.room_number, r.room_floor, r.room_price
 FROM bookings b
  JOIN rooms r ON b.room_id = r.room_id
     WHERE b.client_id = @clientId
      AND b.check_out_date >= CAST(GETDATE() AS DATE)
       AND b.booking_status IN ('Confirmed', 'Active', 'Checked-In')
     ORDER BY b.check_in_date ASC";

        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", clientId);

    using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
     return MapBooking(reader);
        }

 return null;
    }

    private static Booking MapBooking(SqlDataReader reader)
    {
        return new Booking
  {
            booking_id = reader.GetInt32(reader.GetOrdinal("booking_id")),
        client_id = reader.GetInt32(reader.GetOrdinal("client_id")),
      room_id = reader.GetInt32(reader.GetOrdinal("room_id")),
    booking_date = reader.GetDateTime(reader.GetOrdinal("booking_date")),
  check_in_date = reader.GetDateTime(reader.GetOrdinal("check_in_date")),
            check_out_date = reader.GetDateTime(reader.GetOrdinal("check_out_date")),
    guest_count = reader.GetInt32(reader.GetOrdinal("guest_count")),
      bed_type = reader["bed_type"] as string ?? string.Empty,
 booking_status = reader["booking_status"] as string ?? "Confirmed",
room_name = reader["room_name"] as string,
        room_number = reader["room_number"] as int?,
      room_floor = reader["room_floor"] as int?,
            room_price = reader["room_price"] as decimal?
        };
    }
}
