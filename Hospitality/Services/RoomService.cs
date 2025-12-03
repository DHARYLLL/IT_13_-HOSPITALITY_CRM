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
        // Use a single, consistent SELECT matching DB schema
        string sql = @"SELECT room_id, room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities FROM rooms";
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
                //room_available = reader.GetString(reader.GetOrdinal("room_available")),
                room_status = reader.GetString(reader.GetOrdinal("room_status")),
                room_picture = reader.IsDBNull(reader.GetOrdinal("room_picture")) ? null : (byte[])reader["room_picture"],
                room_amenities = reader.IsDBNull(reader.GetOrdinal("room_amenities")) ? null : reader.GetString(reader.GetOrdinal("room_amenities"))
            });
        }
        return rooms;
    }

    public async Task<int> AddRoomAsync(Room room)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        string sql = @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities)
        VALUES (@name,@number,@floor,@price,@status,@photo,@amenities); SELECT CAST(SCOPE_IDENTITY() AS int);";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@name", room.room_name);
        cmd.Parameters.AddWithValue("@number", room.room_number);
        cmd.Parameters.AddWithValue("@floor", room.room_floor);
        cmd.Parameters.AddWithValue("@price", room.room_price);
        //cmd.Parameters.AddWithValue("@available", room.room_available);
        cmd.Parameters.AddWithValue("@status", room.room_status);
        cmd.Parameters.AddWithValue("@photo", (object?)room.room_picture ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@amenities", (object?)room.room_amenities ?? DBNull.Value);
        var idObj = await cmd.ExecuteScalarAsync();
        return (idObj is int i) ? i : Convert.ToInt32(idObj);
    }

    public async Task UpdateRoomAsync(Room room)
    {
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        string sql = @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_available=@available, room_status=@status, room_picture=@photo, room_amenities=@amenities WHERE room_id=@id";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@name", room.room_name);
        cmd.Parameters.AddWithValue("@number", room.room_number);
        cmd.Parameters.AddWithValue("@floor", room.room_floor);
        cmd.Parameters.AddWithValue("@price", room.room_price);
        //cmd.Parameters.AddWithValue("@available", room.room_available);
        cmd.Parameters.AddWithValue("@status", room.room_status);
        cmd.Parameters.AddWithValue("@photo", (object?)room.room_picture ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@amenities", (object?)room.room_amenities ?? DBNull.Value);
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
