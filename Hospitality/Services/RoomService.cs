using Hospitality.Database;
using Hospitality.Models;
using Microsoft.Data.SqlClient;

namespace Hospitality.Services;

public class RoomService
{
    public async Task<List<Room>> GetRoomsAsync()
    {
        var rooms = new List<Room>();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        string sql = @"SELECT room_id, room_name, room_number, room_floor, room_price, room_available, room_status FROM rooms";
        using var cmd = new SqlCommand(sql, con);
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
                room_available = reader.GetString(reader.GetOrdinal("room_available")),
                room_status = reader.GetString(reader.GetOrdinal("room_status"))
            });
        }
        return rooms;
    }

    public async Task<int> AddRoomAsync(Room room)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        string sql = @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_available, room_status)
 VALUES (@name,@number,@floor,@price,@available,@status); SELECT SCOPE_IDENTITY();";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@name", room.room_name);
        cmd.Parameters.AddWithValue("@number", room.room_number);
        cmd.Parameters.AddWithValue("@floor", room.room_floor);
        cmd.Parameters.AddWithValue("@price", room.room_price);
        cmd.Parameters.AddWithValue("@available", room.room_available);
        cmd.Parameters.AddWithValue("@status", room.room_status);
        var idObj = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(idObj);
    }

    public async Task UpdateRoomAsync(Room room)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        string sql = @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_available=@available, room_status=@status WHERE room_id=@id";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@name", room.room_name);
        cmd.Parameters.AddWithValue("@number", room.room_number);
        cmd.Parameters.AddWithValue("@floor", room.room_floor);
        cmd.Parameters.AddWithValue("@price", room.room_price);
        cmd.Parameters.AddWithValue("@available", room.room_available);
        cmd.Parameters.AddWithValue("@status", room.room_status);
        cmd.Parameters.AddWithValue("@id", room.room_id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRoomAsync(int id)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        string sql = "DELETE FROM rooms WHERE room_id=@id";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
