using Hospitality.Database;
using Hospitality.Models;
using Microsoft.Data.SqlClient;

namespace Hospitality.Services;

public class RoomService
{
    private readonly SyncService? _syncService;
    private readonly DualWriteService? _dualWriteService;

    public RoomService()
    {
        // Default constructor for backward compatibility
    }

    public RoomService(SyncService syncService)
    {
        _syncService = syncService;
    }

    public RoomService(SyncService syncService, DualWriteService dualWriteService)
    {
        _syncService = syncService;
        _dualWriteService = dualWriteService;
    }

    public async Task<List<Room>> GetRoomsAsync()
    {
        var rooms = new List<Room>();
      using var con = DbConnection.GetConnection();
        await con.OpenAsync();
  
        // Check if room_occupancy column exists
      bool hasOccupancyColumn = await HasColumnAsync(con, "rooms", "room_occupancy");
    
        string sql = hasOccupancyColumn
      ? @"SELECT room_id, room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities, room_occupancy FROM rooms"
            : @"SELECT room_id, room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities FROM rooms";
        
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
      room_status = reader.GetString(reader.GetOrdinal("room_status")),
        room_picture = reader.IsDBNull(reader.GetOrdinal("room_picture")) ? null : (byte[])reader["room_picture"],
      room_amenities = reader.IsDBNull(reader.GetOrdinal("room_amenities")) ? null : reader.GetString(reader.GetOrdinal("room_amenities")),
       room_occupancy = hasOccupancyColumn && !reader.IsDBNull(reader.GetOrdinal("room_occupancy")) 
                ? reader.GetInt32(reader.GetOrdinal("room_occupancy")) 
       : 2 // Default occupancy
            });
        }
    return rooms;
    }

    public async Task<int> AddRoomAsync(Room room)
    {
        // If DualWriteService is available, use it for dual-write
        if (_dualWriteService != null)
   {
            return await _dualWriteService.ExecuteWriteAsync(
             "Room",
   "rooms",
       "INSERT",
    async (con, tx) =>
                {
     // Check if sync_status column exists
         bool hasSyncColumn = await HasSyncStatusColumnAsync(con, tx, "rooms");
         bool hasOccupancyColumn = await HasColumnAsync(con, "rooms", "room_occupancy");

     string sql = hasSyncColumn && hasOccupancyColumn
  ? @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities, room_occupancy, sync_status)
          VALUES (@name,@number,@floor,@price,@status,@photo,@amenities,@occupancy,'pending'); SELECT CAST(SCOPE_IDENTITY() AS int);"
      : hasSyncColumn
? @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities, sync_status)
                  VALUES (@name,@number,@floor,@price,@status,@photo,@amenities,'pending'); SELECT CAST(SCOPE_IDENTITY() AS int);"
     : hasOccupancyColumn
? @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities, room_occupancy)
             VALUES (@name,@number,@floor,@price,@status,@photo,@amenities,@occupancy); SELECT CAST(SCOPE_IDENTITY() AS int);"
 : @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities)
         VALUES (@name,@number,@floor,@price,@status,@photo,@amenities); SELECT CAST(SCOPE_IDENTITY() AS int);";

   using var cmd = new SqlCommand(sql, con, tx);
           cmd.Parameters.AddWithValue("@name", room.room_name);
               cmd.Parameters.AddWithValue("@number", room.room_number);
         cmd.Parameters.AddWithValue("@floor", room.room_floor);
      cmd.Parameters.AddWithValue("@price", room.room_price);
             cmd.Parameters.AddWithValue("@status", room.room_status);
        cmd.Parameters.AddWithValue("@photo", (object?)room.room_picture ?? DBNull.Value);
             cmd.Parameters.AddWithValue("@amenities", (object?)room.room_amenities ?? DBNull.Value);
   if (hasOccupancyColumn)
            cmd.Parameters.AddWithValue("@occupancy", room.room_occupancy);
             var idObj = await cmd.ExecuteScalarAsync();
              return (idObj is int i) ? i : Convert.ToInt32(idObj);
      });
        }

        // Fallback to original implementation
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();
        
        bool hasOccupancy = await HasColumnAsync(con, "rooms", "room_occupancy");
        
        string sqlFallback = hasOccupancy
            ? @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities, room_occupancy)
             VALUES (@name,@number,@floor,@price,@status,@photo,@amenities,@occupancy); SELECT CAST(SCOPE_IDENTITY() AS int);"
         : @"INSERT INTO rooms (room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities)
  VALUES (@name,@number,@floor,@price,@status,@photo,@amenities); SELECT CAST(SCOPE_IDENTITY() AS int);";
   
        using var cmdFallback = new SqlCommand(sqlFallback, con);
        cmdFallback.Parameters.AddWithValue("@name", room.room_name);
     cmdFallback.Parameters.AddWithValue("@number", room.room_number);
        cmdFallback.Parameters.AddWithValue("@floor", room.room_floor);
        cmdFallback.Parameters.AddWithValue("@price", room.room_price);
        cmdFallback.Parameters.AddWithValue("@status", room.room_status);
  cmdFallback.Parameters.AddWithValue("@photo", (object?)room.room_picture ?? DBNull.Value);
  cmdFallback.Parameters.AddWithValue("@amenities", (object?)room.room_amenities ?? DBNull.Value);
        if (hasOccupancy)
            cmdFallback.Parameters.AddWithValue("@occupancy", room.room_occupancy);
        var idObjFallback = await cmdFallback.ExecuteScalarAsync();
        int roomIdFallback = (idObjFallback is int i) ? i : Convert.ToInt32(idObjFallback);

        // Queue for sync to online database
        if (_syncService != null)
        {
  await _syncService.QueueChangeAsync("Room", roomIdFallback, "INSERT", "rooms");
            Console.WriteLine($"?? Room {roomIdFallback} queued for sync");
        }

        return roomIdFallback;
    }

    public async Task UpdateRoomAsync(Room room)
    {
      // If DualWriteService is available, use it for dual-write
        if (_dualWriteService != null)
    {
 await _dualWriteService.ExecuteWriteAsync(
      "Room",
                "rooms",
                "UPDATE",
      room.room_id,
          async (con, tx) =>
        {
          // Check if sync_status column exists
    bool hasSyncColumn = await HasSyncStatusColumnAsync(con, tx, "rooms");
                    bool hasOccupancyColumn = await HasColumnAsync(con, "rooms", "room_occupancy");

          string sql = hasSyncColumn && hasOccupancyColumn
     ? @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_status=@status, room_picture=@photo, room_amenities=@amenities, room_occupancy=@occupancy, sync_status='pending', last_modified=GETDATE() WHERE room_id=@id"
           : hasSyncColumn
          ? @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_status=@status, room_picture=@photo, room_amenities=@amenities, sync_status='pending', last_modified=GETDATE() WHERE room_id=@id"
          : hasOccupancyColumn
            ? @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_status=@status, room_picture=@photo, room_amenities=@amenities, room_occupancy=@occupancy WHERE room_id=@id"
        : @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_status=@status, room_picture=@photo, room_amenities=@amenities WHERE room_id=@id";

             using var cmd = new SqlCommand(sql, con, tx);
         cmd.Parameters.AddWithValue("@name", room.room_name);
  cmd.Parameters.AddWithValue("@number", room.room_number);
     cmd.Parameters.AddWithValue("@floor", room.room_floor);
          cmd.Parameters.AddWithValue("@price", room.room_price);
    cmd.Parameters.AddWithValue("@status", room.room_status);
    cmd.Parameters.AddWithValue("@photo", (object?)room.room_picture ?? DBNull.Value);
         cmd.Parameters.AddWithValue("@amenities", (object?)room.room_amenities ?? DBNull.Value);
   if (hasOccupancyColumn)
             cmd.Parameters.AddWithValue("@occupancy", room.room_occupancy);
            cmd.Parameters.AddWithValue("@id", room.room_id);
     await cmd.ExecuteNonQueryAsync();
       return true;
      });
         return;
}

        // Fallback to original implementation
        using var con = DbConnection.GetConnection();
    await con.OpenAsync();
     
        bool hasOccupancy = await HasColumnAsync(con, "rooms", "room_occupancy");
        
        string sqlFallback = hasOccupancy
            ? @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_status=@status, room_picture=@photo, room_amenities=@amenities, room_occupancy=@occupancy WHERE room_id=@id"
 : @"UPDATE rooms SET room_name=@name, room_number=@number, room_floor=@floor, room_price=@price, room_status=@status, room_picture=@photo, room_amenities=@amenities WHERE room_id=@id";

        using var cmdFallback = new SqlCommand(sqlFallback, con);
        cmdFallback.Parameters.AddWithValue("@name", room.room_name);
        cmdFallback.Parameters.AddWithValue("@number", room.room_number);
   cmdFallback.Parameters.AddWithValue("@floor", room.room_floor);
    cmdFallback.Parameters.AddWithValue("@price", room.room_price);
        cmdFallback.Parameters.AddWithValue("@status", room.room_status);
cmdFallback.Parameters.AddWithValue("@photo", (object?)room.room_picture ?? DBNull.Value);
        cmdFallback.Parameters.AddWithValue("@amenities", (object?)room.room_amenities ?? DBNull.Value);
        if (hasOccupancy)
            cmdFallback.Parameters.AddWithValue("@occupancy", room.room_occupancy);
        cmdFallback.Parameters.AddWithValue("@id", room.room_id);
        await cmdFallback.ExecuteNonQueryAsync();

        // Queue for sync to online database
        if (_syncService != null)
        {
            await _syncService.QueueChangeAsync("Room", room.room_id, "UPDATE", "rooms");
        Console.WriteLine($"?? Room {room.room_id} update queued for sync");
  }
    }

    public async Task DeleteRoomAsync(int id)
    {
        // If DualWriteService is available, use it for dual-write
      if (_dualWriteService != null)
        {
     await _dualWriteService.ExecuteWriteAsync(
                "Room",
    "rooms",
   "DELETE",
      id,
   async (con, tx) =>
      {
      string sql = "DELETE FROM rooms WHERE room_id=@id";
   using var cmd = new SqlCommand(sql, con, tx);
     cmd.Parameters.AddWithValue("@id", id);
           await cmd.ExecuteNonQueryAsync();
           return true;
       },
  async (onlineCon, onlineTx) =>
          {
           string sql = "DELETE FROM rooms WHERE room_id=@id";
              using var cmd = new SqlCommand(sql, onlineCon, onlineTx);
     cmd.Parameters.AddWithValue("@id", id);
   await cmd.ExecuteNonQueryAsync();
       return true;
 });
            return;
        }

        // Fallback to original implementation
      using var con = DbConnection.GetConnection();
        await con.OpenAsync();
    string sqlFallback = "DELETE FROM rooms WHERE room_id=@id";
        using var cmdFallback = new SqlCommand(sqlFallback, con);
        cmdFallback.Parameters.AddWithValue("@id", id);
        await cmdFallback.ExecuteNonQueryAsync();

        // Queue for sync to online database
    if (_syncService != null)
      {
   await _syncService.QueueChangeAsync("Room", id, "DELETE", "rooms");
    Console.WriteLine($"?? Room {id} deletion queued for sync");
 }
    }

    /// <summary>
    /// Checks if a specific column exists in the specified table
    /// </summary>
    private async Task<bool> HasColumnAsync(SqlConnection con, string tableName, string columnName)
  {
   try
        {
     string sql = @"SELECT COUNT(*) FROM sys.columns 
    WHERE object_id = OBJECT_ID(@tableName) AND name = @columnName";
    using var cmd = new SqlCommand(sql, con);
          cmd.Parameters.AddWithValue("@tableName", tableName);
            cmd.Parameters.AddWithValue("@columnName", columnName);
        var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the sync_status column exists in the specified table
    /// </summary>
    private async Task<bool> HasSyncStatusColumnAsync(SqlConnection con, SqlTransaction? tx, string tableName)
 {
        try
        {
       string sql = @"SELECT COUNT(*) FROM sys.columns 
    WHERE object_id = OBJECT_ID(@tableName) AND name = 'sync_status'";
        using var cmd = new SqlCommand(sql, con, tx);
cmd.Parameters.AddWithValue("@tableName", tableName);
          var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
      }
        catch
        {
   return false;
        }
    }
}
