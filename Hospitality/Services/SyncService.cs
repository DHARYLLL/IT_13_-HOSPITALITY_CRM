using System.Text.Json;
using Microsoft.Data.SqlClient;
using Hospitality.Database;
using System.Diagnostics;

namespace Hospitality.Services;

/// <summary>
/// Handles synchronization between local and online databases
/// </summary>
public class SyncService
{
    private readonly ConnectivityService _connectivity;
    private bool _isSyncing = false;
    private readonly object _syncLock = new();

    /// <summary>
    /// Event fired when sync status changes
    /// </summary>
    public event Action<string>? SyncStatusChanged;

    /// <summary>
    /// Event fired when sync completes
    /// </summary>
    public event Action<SyncResult>? SyncCompleted;

    public SyncService(ConnectivityService connectivity)
    {
      _connectivity = connectivity;

        // Auto-sync when online database becomes available
     _connectivity.OnlineDbAvailable += OnOnlineDbAvailable;
        
  Log("?? SyncService initialized and listening for connectivity changes");
    }

    /// <summary>
 /// Helper method for logging - outputs to both Console and Debug
    /// </summary>
    private static void Log(string message)
    {
        string logMessage = $"[SYNC] {message}";
        Console.WriteLine(logMessage);
      Debug.WriteLine(logMessage);
    }

    private void OnOnlineDbAvailable()
    {
        Log("?? OnlineDbAvailable event received!");
        // Fire and forget - run sync in background
        _ = Task.Run(async () =>
  {
      try
       {
     Log("?? Starting automatic sync...");
       
     // Sync both SyncQueue and pending records from tables in one operation
         var result = await SyncAllPendingAsync();
 Log($"? Automatic sync completed: {result.Message}");
      }
        catch (Exception ex)
        {
         Log($"? Background sync error: {ex.Message}");
  }
        });
    }

    /// <summary>
    /// Returns true if currently syncing
    /// </summary>
  public bool IsSyncing => _isSyncing;

    /// <summary>
    /// Gets the count of pending changes
    /// </summary>
    public async Task<int> GetPendingChangesCountAsync()
    {
        try
    {
            using var con = DbConnection.GetLocalConnection();
      await con.OpenAsync();

            string sql = "SELECT COUNT(*) FROM SyncQueue WHERE sync_status = 'pending'";
  using var cmd = new SqlCommand(sql, con);
         var result = await cmd.ExecuteScalarAsync();
     return Convert.ToInt32(result);
        }
      catch
        {
     return 0;
        }
    }

    /// <summary>
    /// Queue a change to be synced later
    /// </summary>
    public async Task QueueChangeAsync(string entityType, int entityId, string changeType, string tableName, object? data = null)
    {
        try
        {
    using var con = DbConnection.GetLocalConnection();
            await con.OpenAsync();

          string sql = @"
    INSERT INTO SyncQueue (entity_type, entity_id, change_type, table_name, change_data, sync_status)
    VALUES (@entityType, @entityId, @changeType, @tableName, @changeData, 'pending')";

 using var cmd = new SqlCommand(sql, con);
 cmd.Parameters.AddWithValue("@entityType", entityType);
  cmd.Parameters.AddWithValue("@entityId", entityId);
       cmd.Parameters.AddWithValue("@changeType", changeType);
          cmd.Parameters.AddWithValue("@tableName", tableName);
            cmd.Parameters.AddWithValue("@changeData", data != null ? JsonSerializer.Serialize(data) : DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
  Log($"?? Queued {changeType} for {entityType} #{entityId}");

        // Try to sync immediately if online
  if (await _connectivity.CheckOnlineDatabaseAsync())
      {
 _ = Task.Run(async () => await SyncAllAsync());
      }
      }
        catch (Exception ex)
        {
            Log($"? Error queuing change: {ex.Message}");
  }
    }

 /// <summary>
    /// Synchronize all pending changes
    /// </summary>
    public async Task<SyncResult> SyncAllAsync()
{
        lock (_syncLock)
   {
     if (_isSyncing) return new SyncResult { Success = false, Message = "Sync already in progress" };
 _isSyncing = true;
        }

        var result = new SyncResult();
      SyncStatusChanged?.Invoke("Syncing...");

        try
        {
     // Check if we can reach online database
    if (!await _connectivity.CheckOnlineDatabaseAsync())
            {
 result.Success = false;
  result.Message = "Online database not reachable";
       return result;
            }

    Log("?? Starting sync...");

            // 1. Push pending changes to online database
       var pushResult = await PushChangesToOnlineAsync();
            result.PushedCount = pushResult.Count;
  result.PushErrors = pushResult.Errors;

    // 2. Pull latest data from online database (optional - for two-way sync)
          // var pullResult = await PullChangesFromOnlineAsync();
            // result.PulledCount = pullResult.Count;

            result.Success = pushResult.Errors == 0;
  result.Message = $"Pushed {pushResult.Count} changes, {pushResult.Errors} errors";

            Log($"? Sync completed: {result.Message}");
       SyncStatusChanged?.Invoke(result.Success ? "Synced" : "Sync errors");
            SyncCompleted?.Invoke(result);
      }
        catch (Exception ex)
     {
            Log($"? Sync failed: {ex.Message}");
            result.Success = false;
          result.Message = $"Sync failed: {ex.Message}";
         SyncStatusChanged?.Invoke("Sync failed");
        }
        finally
        {
   _isSyncing = false;
 }

   return result;
    }

    /// <summary>
    /// Push pending changes from local to online database
    /// </summary>
    private async Task<(int Count, int Errors)> PushChangesToOnlineAsync()
    {
   int successCount = 0;
        int errorCount = 0;

     try
        {
    using var localCon = DbConnection.GetLocalConnection();
   await localCon.OpenAsync();

            // Get pending changes
          string sql = @"
        SELECT sync_id, entity_type, entity_id, change_type, table_name, change_data, retry_count
            FROM SyncQueue 
           WHERE sync_status = 'pending'
         ORDER BY created_at ASC";

   var pendingChanges = new List<SyncQueueItem>();

    using (var cmd = new SqlCommand(sql, localCon))
            using (var reader = await cmd.ExecuteReaderAsync())
     {
 while (await reader.ReadAsync())
      {
               pendingChanges.Add(new SyncQueueItem
              {
 SyncId = reader.GetInt32(0),
             EntityType = reader.GetString(1),
   EntityId = reader.GetInt32(2),
         ChangeType = reader.GetString(3),
  TableName = reader.GetString(4),
  ChangeData = reader.IsDBNull(5) ? null : reader.GetString(5),
            RetryCount = reader.GetInt32(6)
  });
          }
       }

  Log($"?? Found {pendingChanges.Count} pending changes to sync");

            foreach (var change in pendingChanges)
    {
           try
      {
                 bool success = await SyncSingleChangeAsync(change, localCon);

  if (success)
            {
        // Mark as completed in SyncQueue
               await UpdateSyncStatusAsync(localCon, change.SyncId, "completed", null);
     
        // Update the entity's sync_status to 'synced' in the source table
     await MarkEntityAsSyncedAsync(localCon, change.TableName, change.EntityId);
     
        successCount++;
    }
else
        {
     // Mark as failed
    await UpdateSyncStatusAsync(localCon, change.SyncId, "failed", "Sync operation failed");
         errorCount++;
              }
    }
 catch (Exception ex)
  {
           Log($"? Error syncing {change.EntityType} #{change.EntityId}: {ex.Message}");

        change.RetryCount++;
  if (change.RetryCount >= 5)
      {
    await UpdateSyncStatusAsync(localCon, change.SyncId, "failed", ex.Message);
  }
         else
       {
      await UpdateRetryCountAsync(localCon, change.SyncId, change.RetryCount);
        }
         errorCount++;
    }
            }
        }
  catch (Exception ex)
        {
          Log($"? Push error: {ex.Message}");
    errorCount++;
        }

        return (successCount, errorCount);
    }

    /// <summary>
    /// Update the sync_status column in the source table to 'synced' after successful sync
    /// </summary>
    private async Task MarkEntityAsSyncedAsync(SqlConnection con, string tableName, int entityId)
    {
        try
  {
     string pkColumn = GetPrimaryKeyColumn(tableName);
            string sql = $"UPDATE {tableName} SET sync_status = 'synced', last_modified = GETDATE() WHERE {pkColumn} = @id";
          
            using var cmd = new SqlCommand(sql, con);
       cmd.Parameters.AddWithValue("@id", entityId);
       int rowsAffected = await cmd.ExecuteNonQueryAsync();
            
     if (rowsAffected > 0)
            {
    Log($"? Marked {tableName} #{entityId} as synced");
            }
        }
   catch (Exception ex)
        {
    // Table might not have sync_status column, log but don't fail
   Log($"?? Could not mark {tableName} #{entityId} as synced: {ex.Message}");
    }
  }

    /// <summary>
    /// Sync a single change to the online database
    /// </summary>
    private async Task<bool> SyncSingleChangeAsync(SyncQueueItem change, SqlConnection localCon)
    {
        Log($"?? Syncing {change.ChangeType} for {change.EntityType} #{change.EntityId}...");

  using var onlineCon = DbConnection.GetOnlineConnection();
    await onlineCon.OpenAsync();

    switch (change.ChangeType.ToUpper())
        {
case "INSERT":
    case "UPDATE":
      return await SyncRecordToOnlineAsync(change, localCon, onlineCon);

 case "DELETE":
          return await DeleteFromOnlineAsync(change, onlineCon);

            default:
                Log($"?? Unknown change type: {change.ChangeType}");
     return false;
        }
    }

    /// <summary>
    /// Sync a record from local to online database
    /// </summary>
    private async Task<bool> SyncRecordToOnlineAsync(SyncQueueItem change, SqlConnection localCon, SqlConnection onlineCon)
    {
        // Get the record from local database
        string selectSql = change.EntityType switch
        {
     "Booking" => $"SELECT * FROM Bookings WHERE booking_id = {change.EntityId}",
            "Payment" => $"SELECT * FROM Payments WHERE payment_id = {change.EntityId}",
       "Message" => $"SELECT * FROM Messages WHERE message_id = {change.EntityId}",
            "LoyaltyProgram" => $"SELECT * FROM LoyaltyPrograms WHERE loyalty_id = {change.EntityId}",
            "LoyaltyTransaction" => $"SELECT * FROM LoyaltyTransactions WHERE transaction_id = {change.EntityId}",
         "User" => $"SELECT * FROM Users WHERE user_id = {change.EntityId}",
      "Client" => $"SELECT * FROM Clients WHERE client_id = {change.EntityId}",
        "Room" => $"SELECT * FROM rooms WHERE room_id = {change.EntityId}",
        _ => null
        };

   if (selectSql == null)
        {
            Log($"?? Unknown entity type: {change.EntityType}");
            return false;
        }

        // Use a separate connection for reading to avoid reader conflicts
        using var readCon = DbConnection.GetLocalConnection();
        await readCon.OpenAsync();
        
   using var selectCmd = new SqlCommand(selectSql, readCon);
        using var reader = await selectCmd.ExecuteReaderAsync();

   if (!await reader.ReadAsync())
        {
          Log($"?? Record not found in local database: {change.EntityType} #{change.EntityId}");
     return true; // Consider it synced since it doesn't exist
        }

        // Read all data into a dictionary before closing the reader
      var data = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
    string columnName = reader.GetName(i);
            data[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
      }
   
        // Close the reader before performing sync
     await reader.CloseAsync();

   // Build INSERT/UPDATE query based on entity type
        return change.EntityType switch
        {
   "Booking" => await SyncBookingAsync(data, onlineCon),
         "Payment" => await SyncPaymentAsync(data, onlineCon),
 "Message" => await SyncMessageAsync(data, onlineCon),
            "Room" => await SyncRoomAsync(data, onlineCon),
            _ => false
        };
    }

    private async Task<bool> SyncBookingAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
   {
            // Enable IDENTITY_INSERT
         string enableIdentitySql = "SET IDENTITY_INSERT Bookings ON;";
     using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
      await enableCmd.ExecuteNonQueryAsync();

            try
      {
      string mergeSql = @"
    MERGE INTO Bookings AS target
           USING (SELECT @booking_id AS booking_id) AS source
     ON target.booking_id = source.booking_id
WHEN MATCHED THEN
UPDATE SET 
                  client_id = @client_id,
          [check-in_date] = @check_in_date,
           [check-out_date] = @check_out_date,
     person_count = @person_count,
 booking_status = @booking_status,
             client_request = @client_request
      WHEN NOT MATCHED THEN
            INSERT (booking_id, client_id, [check-in_date], [check-out_date], person_count, booking_status, client_request)
         VALUES (@booking_id, @client_id, @check_in_date, @check_out_date, @person_count, @booking_status, @client_request);";

  using var cmd = new SqlCommand(mergeSql, onlineCon);
       cmd.Parameters.AddWithValue("@booking_id", data["booking_id"] ?? DBNull.Value);
     cmd.Parameters.AddWithValue("@client_id", data["client_id"] ?? DBNull.Value);
       cmd.Parameters.AddWithValue("@check_in_date", data.ContainsKey("check-in_date") ? data["check-in_date"] ?? DBNull.Value : DBNull.Value);
      cmd.Parameters.AddWithValue("@check_out_date", data.ContainsKey("check-out_date") ? data["check-out_date"] ?? DBNull.Value : DBNull.Value);
     cmd.Parameters.AddWithValue("@person_count", data["person_count"] ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@booking_status", data["booking_status"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@client_request", data["client_request"] ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
         Log($"? Synced booking #{data["booking_id"]}");
       }
         finally
            {
 string disableIdentitySql = "SET IDENTITY_INSERT Bookings OFF;";
        using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
        await disableCmd.ExecuteNonQueryAsync();
   }
       
            return true;
        }
        catch (Exception ex)
      {
            Log($"? Error syncing booking: {ex.Message}");
     return false;
        }
    }

    private async Task<bool> SyncPaymentAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
        {
       // Enable IDENTITY_INSERT
            string enableIdentitySql = "SET IDENTITY_INSERT Payments ON;";
      using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
  await enableCmd.ExecuteNonQueryAsync();

    try
        {
    string mergeSql = @"
   MERGE INTO Payments AS target
   USING (SELECT @payment_id AS payment_id) AS source
    ON target.payment_id = source.payment_id
      WHEN MATCHED THEN
       UPDATE SET 
        booking_id = @booking_id,
       amount = @amount,
        payment_method = @payment_method,
     payment_status = @payment_status,
          payment_type = @payment_type,
           payment_date = @payment_date
              WHEN NOT MATCHED THEN
 INSERT (payment_id, booking_id, amount, payment_method, payment_status, payment_type, payment_date)
             VALUES (@payment_id, @booking_id, @amount, @payment_method, @payment_status, @payment_type, @payment_date);";

       using var cmd = new SqlCommand(mergeSql, onlineCon);
    cmd.Parameters.AddWithValue("@payment_id", data["payment_id"] ?? DBNull.Value);
      cmd.Parameters.AddWithValue("@booking_id", data["booking_id"] ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@amount", data["amount"] ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@payment_method", data["payment_method"] ?? DBNull.Value);
      cmd.Parameters.AddWithValue("@payment_status", data["payment_status"] ?? DBNull.Value);
      cmd.Parameters.AddWithValue("@payment_type", data["payment_type"] ?? DBNull.Value);
      cmd.Parameters.AddWithValue("@payment_date", data["payment_date"] ?? DBNull.Value);

    await cmd.ExecuteNonQueryAsync();
         Log($"? Synced payment #{data["payment_id"]}");
    }
    finally
     {
   string disableIdentitySql = "SET IDENTITY_INSERT Payments OFF;";
                using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
    await disableCmd.ExecuteNonQueryAsync();
       }

     return true;
   }
        catch (Exception ex)
        {
    Log($"? Error syncing payment: {ex.Message}");
    return false;
        }
    }

  private async Task<bool> SyncMessageAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
        {
 // Enable IDENTITY_INSERT
    string enableIdentitySql = "SET IDENTITY_INSERT Messages ON;";
            using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
    await enableCmd.ExecuteNonQueryAsync();

  try
            {
             string mergeSql = @"
             MERGE INTO Messages AS target
  USING (SELECT @message_id AS message_id) AS source
     ON target.message_id = source.message_id
   WHEN MATCHED THEN
            UPDATE SET 
            client_id = @client_id,
    message_subject = @message_subject,
            message_body = @message_body,
  message_type = @message_type,
        is_read = @is_read,
               sent_date = @sent_date
      WHEN NOT MATCHED THEN
      INSERT (message_id, client_id, message_subject, message_body, message_type, is_read, sent_date)
   VALUES (@message_id, @client_id, @message_subject, @message_body, @message_type, @is_read, @sent_date);";

  using var cmd = new SqlCommand(mergeSql, onlineCon);
 cmd.Parameters.AddWithValue("@message_id", data["message_id"] ?? DBNull.Value);
 cmd.Parameters.AddWithValue("@client_id", data["client_id"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@message_subject", data["message_subject"] ?? DBNull.Value);
cmd.Parameters.AddWithValue("@message_body", data["message_body"] ?? DBNull.Value);
  cmd.Parameters.AddWithValue("@message_type", data["message_type"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is_read", data["is_read"] ?? DBNull.Value);
           cmd.Parameters.AddWithValue("@sent_date", data["sent_date"] ?? DBNull.Value);

      await cmd.ExecuteNonQueryAsync();
    Log($"? Synced message #{data["message_id"]}");
  }
      finally
            {
      string disableIdentitySql = "SET IDENTITY_INSERT Messages OFF;";
      using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
        await disableCmd.ExecuteNonQueryAsync();
 }
      
            return true;
  }
        catch (Exception ex)
        {
         Log($"? Error syncing message: {ex.Message}");
       return false;
      }
    }

    private async Task<bool> SyncRoomAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
        {
            // Enable IDENTITY_INSERT
        string enableIdentitySql = "SET IDENTITY_INSERT rooms ON;";
            using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
       await enableCmd.ExecuteNonQueryAsync();

            try
     {
      string mergeSql = @"
                    MERGE INTO rooms AS target
         USING (SELECT @room_id AS room_id) AS source
  ON target.room_id = source.room_id
    WHEN MATCHED THEN
       UPDATE SET 
     room_name = @room_name,
              room_number = @room_number,
              room_floor = @room_floor,
          room_price = @room_price,
         room_status = @room_status,
       room_picture = @room_picture,
         room_amenities = @room_amenities
           WHEN NOT MATCHED THEN
   INSERT (room_id, room_name, room_number, room_floor, room_price, room_status, room_picture, room_amenities)
  VALUES (@room_id, @room_name, @room_number, @room_floor, @room_price, @room_status, @room_picture, @room_amenities);";

        using var cmd = new SqlCommand(mergeSql, onlineCon);
       cmd.Parameters.AddWithValue("@room_id", data["room_id"] ?? DBNull.Value);
  cmd.Parameters.AddWithValue("@room_name", data["room_name"] ?? DBNull.Value);
          cmd.Parameters.AddWithValue("@room_number", data["room_number"] ?? DBNull.Value);
          cmd.Parameters.AddWithValue("@room_floor", data["room_floor"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@room_price", data["room_price"] ?? DBNull.Value);
           cmd.Parameters.AddWithValue("@room_status", data["room_status"] ?? DBNull.Value);
             cmd.Parameters.AddWithValue("@room_picture", data["room_picture"] ?? DBNull.Value);
 cmd.Parameters.AddWithValue("@room_amenities", data["room_amenities"] ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
     Log($"? Synced room #{data["room_id"]}");
            }
       finally
        {
      string disableIdentitySql = "SET IDENTITY_INSERT rooms OFF;";
  using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
     await disableCmd.ExecuteNonQueryAsync();
            }
        
         return true;
        }
        catch (Exception ex)
        {
    Log($"? Error syncing room: {ex.Message}");
   return false;
        }
  }

    private async Task<bool> DeleteFromOnlineAsync(SyncQueueItem change, SqlConnection onlineCon)
    {
  try
        {
       string deleteSql = change.EntityType switch
            {
         "Booking" => "DELETE FROM Bookings WHERE booking_id = @id",
     "Payment" => "DELETE FROM Payments WHERE payment_id = @id",
       "Message" => "DELETE FROM Messages WHERE message_id = @id",
    "Room" => "DELETE FROM rooms WHERE room_id = @id",
      _ => null
    };

  if (deleteSql == null) return false;

         using var cmd = new SqlCommand(deleteSql, onlineCon);
          cmd.Parameters.AddWithValue("@id", change.EntityId);
      await cmd.ExecuteNonQueryAsync();

         Log($"? Deleted {change.EntityType} #{change.EntityId} from online");
            return true;
        }
        catch (Exception ex)
    {
            Log($"? Error deleting from online: {ex.Message}");
            return false;
        }
    }

    private async Task UpdateSyncStatusAsync(SqlConnection con, int syncId, string status, string? error)
    {
        string sql = "UPDATE SyncQueue SET sync_status = @status, error_message = @error WHERE sync_id = @id";
        using var cmd = new SqlCommand(sql, con);
      cmd.Parameters.AddWithValue("@status", status);
      cmd.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);
 cmd.Parameters.AddWithValue("@id", syncId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task UpdateRetryCountAsync(SqlConnection con, int syncId, int retryCount)
    {
        string sql = "UPDATE SyncQueue SET retry_count = @count, last_retry_at = GETDATE() WHERE sync_id = @id";
    using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@count", retryCount);
        cmd.Parameters.AddWithValue("@id", syncId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Mark a record in local database as needing sync
    /// </summary>
    public async Task MarkForSyncAsync(string tableName, int recordId, string changeType)
    {
  try
 {
            using var con = DbConnection.GetLocalConnection();
            await con.OpenAsync();

    // Update the sync_status column
   string sql = $"UPDATE {tableName} SET sync_status = 'pending', last_modified = GETDATE() WHERE {GetPrimaryKeyColumn(tableName)} = @id";
using var cmd = new SqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@id", recordId);
            await cmd.ExecuteNonQueryAsync();

            // Queue the change
            await QueueChangeAsync(GetEntityType(tableName), recordId, changeType, tableName);
 }
        catch (Exception ex)
        {
  Log($"? Error marking for sync: {ex.Message}");
  }
    }

    private string GetPrimaryKeyColumn(string tableName) => tableName.ToLower() switch
    {
        "bookings" => "booking_id",
        "payments" => "payment_id",
        "messages" => "message_id",
    "users" => "user_id",
        "clients" => "client_id",
        "rooms" => "room_id",
        "loyaltyprograms" => "loyalty_id",
        "loyaltytransactions" => "transaction_id",
        _ => "id"
    };

    private string GetEntityType(string tableName) => tableName.ToLower() switch
    {
 "bookings" => "Booking",
        "payments" => "Payment",
        "messages" => "Message",
        "users" => "User",
        "clients" => "Client",
        "rooms" => "Room",
        "loyaltyprograms" => "LoyaltyProgram",
        "loyaltytransactions" => "LoyaltyTransaction",
        _ => tableName
  };

    /// <summary>
    /// Sync all records that have sync_status = 'pending' in their tables.
    /// This is useful when records exist with pending status but aren't in SyncQueue.
    /// </summary>
    public async Task<SyncResult> SyncPendingRecordsFromTablesAsync()
    {
        lock (_syncLock)
        {
 if (_isSyncing) return new SyncResult { Success = false, Message = "Sync already in progress" };
_isSyncing = true;
     }

        var result = new SyncResult();
      SyncStatusChanged?.Invoke("Syncing pending records...");

        try
        {
// Check if we can reach online database
   if (!await _connectivity.CheckOnlineDatabaseAsync())
   {
        result.Success = false;
   result.Message = "Online database not reachable";
     return result;
    }

        Log("?? Syncing pending records from tables...");

      int syncedCount = 0;
            int errorCount = 0;

     // Sync pending rooms
     var roomsResult = await SyncPendingFromTableAsync("rooms", "room_id", "Room");
      syncedCount += roomsResult.Synced;
errorCount += roomsResult.Errors;

      // Sync pending bookings
   var bookingsResult = await SyncPendingFromTableAsync("Bookings", "booking_id", "Booking");
   syncedCount += bookingsResult.Synced;
            errorCount += bookingsResult.Errors;

    // Sync pending payments
       var paymentsResult = await SyncPendingFromTableAsync("Payments", "payment_id", "Payment");
    syncedCount += paymentsResult.Synced;
      errorCount += paymentsResult.Errors;

   // Sync pending messages
   var messagesResult = await SyncPendingFromTableAsync("Messages", "message_id", "Message");
   syncedCount += messagesResult.Synced;
   errorCount += messagesResult.Errors;

        result.Success = errorCount == 0;
     result.PushedCount = syncedCount;
         result.PushErrors = errorCount;
            result.Message = $"Synced {syncedCount} records from tables, {errorCount} errors";

  Log($"? Table sync completed: {result.Message}");
 SyncStatusChanged?.Invoke(result.Success ? "Synced" : "Sync errors");
    SyncCompleted?.Invoke(result);
        }
      catch (Exception ex)
        {
   Log($"? Table sync failed: {ex.Message}");
    result.Success = false;
          result.Message = $"Sync failed: {ex.Message}";
     SyncStatusChanged?.Invoke("Sync failed");
     }
    finally
        {
            _isSyncing = false;
   }

        return result;
}

    /// <summary>
    /// Sync all pending records from a specific table
    /// </summary>
    private async Task<(int Synced, int Errors)> SyncPendingFromTableAsync(string tableName, string pkColumn, string entityType)
    {
      int synced = 0;
 int errors = 0;

        try
        {
    // Get IDs of pending records using a separate connection
     var pendingIds = new List<int>();
    
using (var con = DbConnection.GetLocalConnection())
       {
      await con.OpenAsync();
     string sql = $"SELECT {pkColumn} FROM {tableName} WHERE sync_status = 'pending'";

   using var cmd = new SqlCommand(sql, con);
 using var reader = await cmd.ExecuteReaderAsync();
       
       while (await reader.ReadAsync())
           {
      pendingIds.Add(reader.GetInt32(0));
      }
      }

    if (pendingIds.Count == 0)
      {
         Log($"?? No pending {tableName} to sync");
     return (0, 0);
         }

 Log($"?? Found {pendingIds.Count} pending {tableName} to sync");

     foreach (var entityId in pendingIds)
    {
try
       {
              var change = new SyncQueueItem
    {
   EntityType = entityType,
     EntityId = entityId,
    ChangeType = "INSERT",
    TableName = tableName
    };

                    // Use fresh connections for each sync operation
             using var localCon = DbConnection.GetLocalConnection();
              await localCon.OpenAsync();
   
               using var onlineCon = DbConnection.GetOnlineConnection();
           await onlineCon.OpenAsync();

         bool success = await SyncRecordToOnlineAsync(change, localCon, onlineCon);

          if (success)
      {
    // Use a fresh connection to mark as synced
    using var updateCon = DbConnection.GetLocalConnection();
          await updateCon.OpenAsync();
      await MarkEntityAsSyncedAsync(updateCon, tableName, entityId);
 synced++;
               Log($"? Synced {entityType} #{entityId}");
   }
        else
  {
      errors++;
        Log($"? Failed to sync {entityType} #{entityId}");
           }
       }
        catch (Exception ex)
             {
       Log($"? Error syncing {entityType} #{entityId}: {ex.Message}");
     errors++;
       }
 }
     }
 catch (Exception ex)
{
  Log($"? Error getting pending {tableName}: {ex.Message}");
       errors++;
  }

        return (synced, errors);
    }

    /// <summary>
    /// Sync everything - both SyncQueue items and pending records from tables
    /// </summary>
    public async Task<SyncResult> SyncAllPendingAsync()
    {
   lock (_syncLock)
        {
if (_isSyncing) return new SyncResult { Success = false, Message = "Sync already in progress" };
_isSyncing = true;
        }

        var result = new SyncResult();
    SyncStatusChanged?.Invoke("Syncing...");

   try
    {
    // Check if we can reach online database
    if (!await _connectivity.CheckOnlineDatabaseAsync())
       {
     result.Success = false;
         result.Message = "Online database not reachable";
       return result;
   }

         Log("?? Starting comprehensive sync...");

          int totalSynced = 0;
           int totalErrors = 0;

      // 1. Push pending changes from SyncQueue
     var pushResult = await PushChangesToOnlineAsync();
     totalSynced += pushResult.Count;
     totalErrors += pushResult.Errors;
         Log($"?? SyncQueue: {pushResult.Count} synced, {pushResult.Errors} errors");

           // 2. Sync pending records directly from tables (catches records not in SyncQueue)
         var roomsResult = await SyncPendingFromTableAsync("rooms", "room_id", "Room");
   totalSynced += roomsResult.Synced;
 totalErrors += roomsResult.Errors;

   var bookingsResult = await SyncPendingFromTableAsync("Bookings", "booking_id", "Booking");
 totalSynced += bookingsResult.Synced;
   totalErrors += bookingsResult.Errors;

  var paymentsResult = await SyncPendingFromTableAsync("Payments", "payment_id", "Payment");
   totalSynced += paymentsResult.Synced;
     totalErrors += paymentsResult.Errors;

   var messagesResult = await SyncPendingFromTableAsync("Messages", "message_id", "Message");
 totalSynced += messagesResult.Synced;
 totalErrors += messagesResult.Errors;

  result.Success = totalErrors == 0;
 result.PushedCount = totalSynced;
       result.PushErrors = totalErrors;
   result.Message = $"Synced {totalSynced} records, {totalErrors} errors";

         Log($"? Comprehensive sync completed: {result.Message}");
         SyncStatusChanged?.Invoke(result.Success ? "Synced" : "Sync errors");
            SyncCompleted?.Invoke(result);
        }
       catch (Exception ex)
        {
      Log($"? Sync failed: {ex.Message}");
  result.Success = false;
          result.Message = $"Sync failed: {ex.Message}";
 SyncStatusChanged?.Invoke("Sync failed");
        }
     finally
        {
     _isSyncing = false;
 }

 return result;
    }
}

/// <summary>
/// Represents an item in the sync queue
/// </summary>
public class SyncQueueItem
{
    public int SyncId { get; set; }
  public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string ChangeType { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? ChangeData { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int PushedCount { get; set; }
    public int PulledCount { get; set; }
    public int PushErrors { get; set; }
    public int PullErrors { get; set; }
}
