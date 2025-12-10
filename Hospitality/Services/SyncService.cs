using Microsoft.Data.SqlClient;
using Hospitality.Database;
using System.Diagnostics;

namespace Hospitality.Services;

/// <summary>
/// Handles synchronization between local and online databases
/// Uses table-level sync_status tracking (no SyncQueue)
/// Includes automatic background sync
/// </summary>
public class SyncService : IDisposable
{
    private readonly ConnectivityService _connectivity;
    private bool _isSyncing = false;
    private readonly object _syncLock = new();
    private Timer? _backgroundSyncTimer;
    private bool _isDisposed = false;

    // Configurable sync interval (default: 30 seconds)
    private readonly TimeSpan _autoSyncInterval = TimeSpan.FromSeconds(30);

    // Track last sync time to avoid too frequent syncs
    private DateTime _lastSyncTime = DateTime.MinValue;
    private readonly TimeSpan _minSyncInterval = TimeSpan.FromSeconds(10);

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
        
        Log("? SyncService initialized (automatic background sync enabled)");
        
        // Start background sync timer
        StartBackgroundSync();
      
        // Perform initial sync on startup (after a short delay)
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000); // Wait 3 seconds for app to fully initialize
            await PerformAutoSyncAsync("Startup");
      });
    }

    /// <summary>
    /// Starts the background sync timer
    /// </summary>
    private void StartBackgroundSync()
    {
 Log($"?? Starting background sync timer (interval: {_autoSyncInterval.TotalSeconds}s)");

        _backgroundSyncTimer = new Timer(
            async _ => await PerformAutoSyncAsync("Background"),
 null,
            _autoSyncInterval, // Initial delay
      _autoSyncInterval  // Periodic interval
        );
    }

    /// <summary>
    /// Performs automatic sync (called by timer and connectivity changes)
    /// </summary>
    private async Task PerformAutoSyncAsync(string trigger)
    {
   if (_isDisposed) return;
     
        // Check if enough time has passed since last sync
        if (DateTime.Now - _lastSyncTime < _minSyncInterval)
  {
            Log($"?? Skipping {trigger} sync (too soon since last sync)");
            return;
        }
      
        // Check if there are pending records
        var pendingCount = await GetPendingChangesCountAsync();
   if (pendingCount == 0)
        {
      // No pending records, skip sync
  return;
        }
        
   // Check if online
        var isOnline = await _connectivity.CheckOnlineDatabaseAsync();
        if (!isOnline)
 {
       Log($"?? {trigger} sync skipped (offline) - {pendingCount} records pending");
     return;
        }
        
      Log($"?? {trigger} sync triggered - {pendingCount} records pending");
   
        try
        {
          var result = await SyncAllPendingAsync();
            _lastSyncTime = DateTime.Now;
            
 if (result.Success && result.PushedCount > 0)
     {
         Log($"? {trigger} sync completed: {result.PushedCount} records synced");
            }
        }
        catch (Exception ex)
        {
     Log($"? {trigger} sync error: {ex.Message}");
     }
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
             await PerformAutoSyncAsync("Connectivity Change");
            }
     catch (Exception ex)
          {
     Log($"? Connectivity change sync error: {ex.Message}");
    }
        });
    }

  /// <summary>
    /// Returns true if currently syncing
    /// </summary>
    public bool IsSyncing => _isSyncing;

    /// <summary>
    /// Gets the count of pending changes across all tables
    /// </summary>
    public async Task<int> GetPendingChangesCountAsync()
    {
     try
        {
            using var con = DbConnection.GetLocalConnection();
  await con.OpenAsync();

            int total = 0;

     // Count pending records in each table (including Users and Clients for complete count)
            string[] tables = { "Users", "Clients", "rooms", "Bookings", "Payments", "Messages", "LoyaltyPrograms", "LoyaltyTransactions" };
       
   foreach (var table in tables)
          {
                try
        {
          string sql = $"SELECT COUNT(*) FROM {table} WHERE sync_status = 'pending'";
         using var cmd = new SqlCommand(sql, con);
    var result = await cmd.ExecuteScalarAsync();
           total += Convert.ToInt32(result);
      }
              catch
     {
          // Table might not have sync_status column, skip
    }
            }

       return total;
      }
    catch
        {
      return 0;
  }
    }

    /// <summary>
    /// Manually trigger sync (for UI buttons, etc.)
    /// </summary>
    public async Task<SyncResult> SyncAllAsync()
    {
  Log("?? Manual sync triggered");
    return await SyncAllPendingAsync();
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
     Log($"? Unknown change type: {change.ChangeType}");
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
 Log($"? Unknown entity type: {change.EntityType}");
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
            "User" => await SyncUserAsync(data, onlineCon),
            "Client" => await SyncClientAsync(data, onlineCon),
            "LoyaltyProgram" => await SyncLoyaltyProgramAsync(data, onlineCon),
            "LoyaltyTransaction" => await SyncLoyaltyTransactionAsync(data, onlineCon),
            _ => false
        };
}

    private async Task<bool> SyncBookingAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
      try
      {
   // First, ensure the client exists in the online database
           int clientId = Convert.ToInt32(data["client_id"]);
   if (!await EnsureClientExistsOnlineAsync(clientId, onlineCon))
   {
     Log($"?? Cannot sync booking - client #{clientId} could not be synced to online database");
        return false;
       }

     // Enable IDENTITY_INSERT
   string enableIdentitySql = "SET IDENTITY_INSERT Bookings ON;";
      using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
     await enableCmd.ExecuteNonQueryAsync();

      try
  {
  // Get check-in and check-out dates - handle both hyphen and underscore column names
    object? checkInDate = null;
    object? checkOutDate = null;
        object? checkInTime = null;
   object? checkOutTime = null;
      
      // Try different column name variations for check-in date
         if (data.ContainsKey("check-in_date"))
 checkInDate = data["check-in_date"];
           else if (data.ContainsKey("check_in_date"))
   checkInDate = data["check_in_date"];
      else if (data.ContainsKey("checkin_date"))
checkInDate = data["checkin_date"];
   
      // Try different column name variations for check-out date
  if (data.ContainsKey("check-out_date"))
       checkOutDate = data["check-out_date"];
  else if (data.ContainsKey("check_out_date"))
  checkOutDate = data["check_out_date"];
        else if (data.ContainsKey("checkout_date"))
         checkOutDate = data["checkout_date"];

      // Get check-in/out times - use default if not present
   if (data.ContainsKey("check-in_time"))
    checkInTime = data["check-in_time"];
   else if (data.ContainsKey("check_in_time"))
   checkInTime = data["check_in_time"];

   if (data.ContainsKey("check-out_time"))
       checkOutTime = data["check-out_time"];
   else if (data.ContainsKey("check_out_time"))
       checkOutTime = data["check_out_time"];
       
 // Default times if null: check-in 15:00, check-out 11:00
     if (checkInTime == null)
         checkInTime = TimeSpan.FromHours(15); // 3:00 PM
    if (checkOutTime == null)
       checkOutTime = TimeSpan.FromHours(11); // 11:00 AM

       // Log what we found for debugging
      Log($"?? Booking data: id={data["booking_id"]}, client={clientId}, check-in={checkInDate}, check-out={checkOutDate}");

       // Use simple INSERT with DELETE first (more compatible than MERGE)
          string deleteSql = "DELETE FROM Bookings WHERE booking_id = @booking_id;";
  using var deleteCmd = new SqlCommand(deleteSql, onlineCon);
 deleteCmd.Parameters.AddWithValue("@booking_id", data["booking_id"] ?? DBNull.Value);
   await deleteCmd.ExecuteNonQueryAsync();

 string insertSql = @"
INSERT INTO Bookings (booking_id, client_id, [check-in_date], [check-out_date], person_count, booking_status, client_request, [check-in_time], [check-out_time])
VALUES (@booking_id, @client_id, @check_in_date, @check_out_date, @person_count, @booking_status, @client_request, @check_in_time, @check_out_time);";

      using var cmd = new SqlCommand(insertSql, onlineCon);
       cmd.Parameters.AddWithValue("@booking_id", data["booking_id"] ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@client_id", clientId);
  cmd.Parameters.AddWithValue("@check_in_date", checkInDate ?? DBNull.Value);
       cmd.Parameters.AddWithValue("@check_out_date", checkOutDate ?? DBNull.Value);
     cmd.Parameters.AddWithValue("@person_count", data["person_count"] ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@booking_status", data.ContainsKey("booking_status") ? data["booking_status"] ?? "confirmed" : "confirmed");
  cmd.Parameters.AddWithValue("@client_request", data.ContainsKey("client_request") ? data["client_request"] ?? DBNull.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@check_in_time", checkInTime);
   cmd.Parameters.AddWithValue("@check_out_time", checkOutTime);

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
    Log($"? Exception details: {ex}");
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
          Log($"? Exception details: {ex}");
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
     Log($"? Exception details: {ex}");
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
     Log($"? Exception details: {ex}");
   return false;
        }
    }

    private async Task<bool> SyncUserAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
 try
   {
 // Enable IDENTITY_INSERT
    string enableIdentitySql = "SET IDENTITY_INSERT Users ON;";
  using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
await enableCmd.ExecuteNonQueryAsync();

 try
 {
      // Use DELETE + INSERT for simplicity
             string deleteSql = "DELETE FROM Users WHERE user_id = @user_id;";
  using var deleteCmd = new SqlCommand(deleteSql, onlineCon);
    deleteCmd.Parameters.AddWithValue("@user_id", data["user_id"] ?? DBNull.Value);
 await deleteCmd.ExecuteNonQueryAsync();

   // Get values with defaults for NOT NULL columns
 var roleId = data.ContainsKey("role_id") && data["role_id"] != null ? data["role_id"] : 3; // Default role
       var userFname = data.ContainsKey("user_fname") && data["user_fname"] != null ? data["user_fname"] : "Guest";
       var userLname = data.ContainsKey("user_lname") && data["user_lname"] != null ? data["user_lname"] : "";
 var userBirthDate = data.ContainsKey("user_brith_date") && data["user_brith_date"] != null ? data["user_brith_date"] : DateTime.Today;
   var userEmail = data.ContainsKey("user_email") && data["user_email"] != null ? data["user_email"] : $"user{data["user_id"]}@temp.com";
     var userContact = data.ContainsKey("user_contact_number") && data["user_contact_number"] != null ? data["user_contact_number"] : "0000000000";
   var userPassword = data.ContainsKey("user_password") && data["user_password"] != null ? data["user_password"] : "temp123";

    string insertSql = @"
     INSERT INTO Users (user_id, role_id, user_fname, user_mname, user_lname, user_brith_date, user_email, user_contact_number, user_password)
VALUES (@user_id, @role_id, @user_fname, @user_mname, @user_lname, @user_brith_date, @user_email, @user_contact_number, @user_password);";

   using var cmd = new SqlCommand(insertSql, onlineCon);
      cmd.Parameters.AddWithValue("@user_id", data["user_id"]);
   cmd.Parameters.AddWithValue("@role_id", roleId);
    cmd.Parameters.AddWithValue("@user_fname", userFname);
    cmd.Parameters.AddWithValue("@user_mname", data.ContainsKey("user_mname") ? data["user_mname"] ?? DBNull.Value : DBNull.Value);
   cmd.Parameters.AddWithValue("@user_lname", userLname);
      cmd.Parameters.AddWithValue("@user_brith_date", userBirthDate);
   cmd.Parameters.AddWithValue("@user_email", userEmail);
  cmd.Parameters.AddWithValue("@user_contact_number", userContact);
 cmd.Parameters.AddWithValue("@user_password", userPassword);

   await cmd.ExecuteNonQueryAsync();
 Log($"? Synced user #{data["user_id"]}");
   }
        finally
  {
   string disableIdentitySql = "SET IDENTITY_INSERT Users OFF;";
  using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
 await disableCmd.ExecuteNonQueryAsync();
       }

     return true;
   }
        catch (Exception ex)
        {
 Log($"? Error syncing user: {ex.Message}");
          Log($"? Exception details: {ex}");
   return false;
    }
  }

    private async Task<bool> SyncClientAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
        {
            // Enable IDENTITY_INSERT
         string enableIdentitySql = "SET IDENTITY_INSERT Clients ON;";
            using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
   await enableCmd.ExecuteNonQueryAsync();

          try
            {
      // Use DELETE + INSERT for simplicity
    string deleteSql = "DELETE FROM Clients WHERE client_id = @client_id;";
       using var deleteCmd = new SqlCommand(deleteSql, onlineCon);
                deleteCmd.Parameters.AddWithValue("@client_id", data["client_id"] ?? DBNull.Value);
       await deleteCmd.ExecuteNonQueryAsync();

           string insertSql = @"
        INSERT INTO Clients (client_id, user_id)
               VALUES (@client_id, @user_id);";

 using var cmd = new SqlCommand(insertSql, onlineCon);
         cmd.Parameters.AddWithValue("@client_id", data["client_id"] ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@user_id", data.ContainsKey("user_id") ? data["user_id"] ?? DBNull.Value : DBNull.Value);

      await cmd.ExecuteNonQueryAsync();
        Log($"? Synced client #{data["client_id"]}");
        }
       finally
    {
         string disableIdentitySql = "SET IDENTITY_INSERT Clients OFF;";
   using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
         await disableCmd.ExecuteNonQueryAsync();
    }

    return true;
  }
        catch (Exception ex)
  {
       Log($"? Error syncing client: {ex.Message}");
            Log($"? Exception details: {ex}");
            return false;
        }
    }

    private async Task<bool> SyncLoyaltyProgramAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
        {
            // Enable IDENTITY_INSERT
            string enableIdentitySql = "SET IDENTITY_INSERT LoyaltyPrograms ON;";
            using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
            await enableCmd.ExecuteNonQueryAsync();

            try
            {
                // Use DELETE + INSERT for simplicity
                string deleteSql = "DELETE FROM LoyaltyPrograms WHERE loyalty_id = @loyalty_id;";
                using var deleteCmd = new SqlCommand(deleteSql, onlineCon);
                deleteCmd.Parameters.AddWithValue("@loyalty_id", data["loyalty_id"] ?? DBNull.Value);
                await deleteCmd.ExecuteNonQueryAsync();

                string insertSql = @"
                    INSERT INTO LoyaltyPrograms (loyalty_id, client_id, current_points, lifetime_points, current_tier, tier_start_date, tier_end_date)
                    VALUES (@loyalty_id, @client_id, @current_points, @lifetime_points, @current_tier, @tier_start_date, @tier_end_date);";

                using var cmd = new SqlCommand(insertSql, onlineCon);
                cmd.Parameters.AddWithValue("@loyalty_id", data["loyalty_id"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@client_id", data["client_id"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@current_points", data.ContainsKey("current_points") ? data["current_points"] ?? 0 : 0);
                cmd.Parameters.AddWithValue("@lifetime_points", data.ContainsKey("lifetime_points") ? data["lifetime_points"] ?? 0 : 0);
                cmd.Parameters.AddWithValue("@current_tier", data.ContainsKey("current_tier") ? data["current_tier"] ?? "Bronze" : "Bronze");
                cmd.Parameters.AddWithValue("@tier_start_date", data.ContainsKey("tier_start_date") ? data["tier_start_date"] ?? DBNull.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@tier_end_date", data.ContainsKey("tier_end_date") ? data["tier_end_date"] ?? DBNull.Value : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                Log($"✅ Synced loyalty program #{data["loyalty_id"]}");
            }
            finally
            {
                string disableIdentitySql = "SET IDENTITY_INSERT LoyaltyPrograms OFF;";
                using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
                await disableCmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            Log($"❌ Error syncing loyalty program: {ex.Message}");
            Log($"❌ Exception details: {ex}");
            return false;
        }
    }

    private async Task<bool> SyncLoyaltyTransactionAsync(Dictionary<string, object?> data, SqlConnection onlineCon)
    {
        try
        {
            // Enable IDENTITY_INSERT
            string enableIdentitySql = "SET IDENTITY_INSERT LoyaltyTransactions ON;";
            using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon);
            await enableCmd.ExecuteNonQueryAsync();

            try
            {
                // Use DELETE + INSERT for simplicity
                string deleteSql = "DELETE FROM LoyaltyTransactions WHERE transaction_id = @transaction_id;";
                using var deleteCmd = new SqlCommand(deleteSql, onlineCon);
                deleteCmd.Parameters.AddWithValue("@transaction_id", data["transaction_id"] ?? DBNull.Value);
                await deleteCmd.ExecuteNonQueryAsync();

                string insertSql = @"
                    INSERT INTO LoyaltyTransactions (transaction_id, loyalty_id, points_amount, transaction_type, description, reference_id, transaction_date)
                    VALUES (@transaction_id, @loyalty_id, @points_amount, @transaction_type, @description, @reference_id, @transaction_date);";

                using var cmd = new SqlCommand(insertSql, onlineCon);
                cmd.Parameters.AddWithValue("@transaction_id", data["transaction_id"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@loyalty_id", data["loyalty_id"] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@points_amount", data.ContainsKey("points_amount") ? data["points_amount"] ?? 0 : 0);
                cmd.Parameters.AddWithValue("@transaction_type", data.ContainsKey("transaction_type") ? data["transaction_type"] ?? "earn" : "earn");
                cmd.Parameters.AddWithValue("@description", data.ContainsKey("description") ? data["description"] ?? DBNull.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@reference_id", data.ContainsKey("reference_id") ? data["reference_id"] ?? DBNull.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@transaction_date", data.ContainsKey("transaction_date") ? data["transaction_date"] ?? DateTime.Now : DateTime.Now);

                await cmd.ExecuteNonQueryAsync();
                Log($"✅ Synced loyalty transaction #{data["transaction_id"]}");
            }
            finally
            {
                string disableIdentitySql = "SET IDENTITY_INSERT LoyaltyTransactions OFF;";
                using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon);
                await disableCmd.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            Log($"❌ Error syncing loyalty transaction: {ex.Message}");
            Log($"❌ Exception details: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Ensures a client exists in the online database before syncing a booking
    /// </summary>
    private async Task<bool> EnsureClientExistsOnlineAsync(int clientId, SqlConnection onlineCon)
    {
        try
 {
  // Check if client already exists online
       string checkSql = "SELECT COUNT(*) FROM Clients WHERE client_id = @clientId";
  using var checkCmd = new SqlCommand(checkSql, onlineCon);
        checkCmd.Parameters.AddWithValue("@clientId", clientId);
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
    
            if (count > 0)
            {
     return true; // Client already exists
     }

          Log($"?? Client #{clientId} not found online, syncing...");

            // Get client data from local database
 using var localCon = DbConnection.GetLocalConnection();
        await localCon.OpenAsync();

            // First, get the user_id for this client
        string getClientSql = "SELECT client_id, user_id FROM Clients WHERE client_id = @clientId";
            using var getClientCmd = new SqlCommand(getClientSql, localCon);
      getClientCmd.Parameters.AddWithValue("@clientId", clientId);
      using var clientReader = await getClientCmd.ExecuteReaderAsync();

    if (!await clientReader.ReadAsync())
   {
                Log($"?? Client #{clientId} not found in local database");
    return false;
            }

            int userId = clientReader.GetInt32(clientReader.GetOrdinal("user_id"));
  await clientReader.CloseAsync();

       // Check if user exists online, if not sync user first
         string checkUserSql = "SELECT COUNT(*) FROM Users WHERE user_id = @userId";
   using var checkUserCmd = new SqlCommand(checkUserSql, onlineCon);
            checkUserCmd.Parameters.AddWithValue("@userId", userId);
     var userCount = Convert.ToInt32(await checkUserCmd.ExecuteScalarAsync());

            if (userCount == 0)
     {
           // Sync user first
            string getUserSql = "SELECT * FROM Users WHERE user_id = @userId";
       using var getUserCmd = new SqlCommand(getUserSql, localCon);
         getUserCmd.Parameters.AddWithValue("@userId", userId);
         using var userReader = await getUserCmd.ExecuteReaderAsync();

        if (await userReader.ReadAsync())
       {
        var userData = new Dictionary<string, object?>();
           for (int i = 0; i < userReader.FieldCount; i++)
     {
        userData[userReader.GetName(i)] = userReader.IsDBNull(i) ? null : userReader.GetValue(i);
            }
   await userReader.CloseAsync();
        await SyncUserAsync(userData, onlineCon);
         }
    else
           {
           await userReader.CloseAsync();
           Log($"?? User #{userId} not found in local database");
              return false;
    }
      }

  // Now sync the client
            var clientData = new Dictionary<string, object?>
{
     ["client_id"] = clientId,
     ["user_id"] = userId
            };
       return await SyncClientAsync(clientData, onlineCon);
        }
        catch (Exception ex)
        {
 Log($"? Error ensuring client exists: {ex.Message}");
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
            
    Log($"?? Marked {tableName} #{recordId} as pending sync");
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
    /// Sync all records that have sync_status = 'pending' in their tables
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

       // IMPORTANT: Sync in dependency order!
            // 1. Users first (no dependencies)
    var usersResult = await SyncPendingFromTableAsync("Users", "user_id", "User");
      syncedCount += usersResult.Synced;
   errorCount += usersResult.Errors;

      // 2. Clients (depends on Users)
     var clientsResult = await SyncPendingFromTableAsync("Clients", "client_id", "Client");
        syncedCount += clientsResult.Synced;
     errorCount += clientsResult.Errors;

     // 3. Rooms
 var roomsResult = await SyncPendingFromTableAsync("rooms", "room_id", "Room");
      syncedCount += roomsResult.Synced;
   errorCount += roomsResult.Errors;

  // 4. Bookings (depends on Clients)
var bookingsResult = await SyncPendingFromTableAsync("Bookings", "booking_id", "Booking");
 syncedCount += bookingsResult.Synced;
   errorCount += bookingsResult.Errors;

    // 5. Payments (depends on Bookings)
     var paymentsResult = await SyncPendingFromTableAsync("Payments", "payment_id", "Payment");
  syncedCount += paymentsResult.Synced;
          errorCount += paymentsResult.Errors;

 // 6. Messages (depends on Clients)
   var messagesResult = await SyncPendingFromTableAsync("Messages", "message_id", "Message");
   syncedCount += messagesResult.Synced;
 errorCount += messagesResult.Errors;

            // 7. Loyalty Programs (depends on Clients)
            var loyaltyProgramsResult = await SyncPendingFromTableAsync("LoyaltyPrograms", "loyalty_id", "LoyaltyProgram");
            syncedCount += loyaltyProgramsResult.Synced;
            errorCount += loyaltyProgramsResult.Errors;

            // 8. Loyalty Transactions (depends on Loyalty Programs)
            var loyaltyTransactionsResult = await SyncPendingFromTableAsync("LoyaltyTransactions", "transaction_id", "LoyaltyTransaction");
            syncedCount += loyaltyTransactionsResult.Synced;
            errorCount += loyaltyTransactionsResult.Errors;

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
        Log($"? Successfully synced {entityType} #{entityId}");
          }
              else
    {
    errors++;
     Log($"? Failed to sync {entityType} #{entityId} - sync method returned false");
    }
  }
   catch (Exception ex)
   {
       Log($"? Error syncing {entityType} #{entityId}: {ex.Message}");
         Log($"? Full exception: {ex}");
    errors++;
     }
  }
}
        catch (Exception ex)
     {
  Log($"? Error getting pending {tableName}: {ex.Message}");
            Log($"? Full exception: {ex}");
   errors++;
        }

        return (synced, errors);
    }

    /// <summary>
 /// Sync everything - pending records from all tables
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

         // IMPORTANT: Sync in dependency order!
    // 1. Users first (no dependencies)
  // 2. Clients (depends on Users)
           // 3. Rooms (no dependencies on above)
    // 4. Bookings (depends on Clients)
     // 5. Payments (depends on Bookings)
           // 6. Messages (depends on Clients)

    // Sync pending users first
   var usersResult = await SyncPendingFromTableAsync("Users", "user_id", "User");
     totalSynced += usersResult.Synced;
  totalErrors += usersResult.Errors;

  // Sync pending clients (depends on users)
  var clientsResult = await SyncPendingFromTableAsync("Clients", "client_id", "Client");
       totalSynced += clientsResult.Synced;
  totalErrors += clientsResult.Errors;

  // Sync pending rooms
   var roomsResult = await SyncPendingFromTableAsync("rooms", "room_id", "Room");
  totalSynced += roomsResult.Synced;
 totalErrors += roomsResult.Errors;

       // Sync pending bookings (depends on clients)
        var bookingsResult = await SyncPendingFromTableAsync("Bookings", "booking_id", "Booking");
       totalSynced += bookingsResult.Synced;
    totalErrors += bookingsResult.Errors;

   // Sync pending payments (depends on bookings)
     var paymentsResult = await SyncPendingFromTableAsync("Payments", "payment_id", "Payment");
          totalSynced += paymentsResult.Synced;
    totalErrors += paymentsResult.Errors;

 // Sync pending messages (depends on clients)
    var messagesResult = await SyncPendingFromTableAsync("Messages", "message_id", "Message");
         totalSynced += messagesResult.Synced;
    totalErrors += messagesResult.Errors;

            // Sync pending loyalty programs (depends on clients)
            var loyaltyProgramsResult = await SyncPendingFromTableAsync("LoyaltyPrograms", "loyalty_id", "LoyaltyProgram");
            totalSynced += loyaltyProgramsResult.Synced;
            totalErrors += loyaltyProgramsResult.Errors;

            // Sync pending loyalty transactions (depends on loyalty programs)
            var loyaltyTransactionsResult = await SyncPendingFromTableAsync("LoyaltyTransactions", "transaction_id", "LoyaltyTransaction");
            totalSynced += loyaltyTransactionsResult.Synced;
            totalErrors += loyaltyTransactionsResult.Errors;

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
  
    /// <summary>
 /// Dispose resources
 /// </summary>
 public void Dispose()
    {
        _isDisposed = true;
        _backgroundSyncTimer?.Dispose();
        _connectivity.OnlineDbAvailable -= OnOnlineDbAvailable;
        Log("? SyncService disposed");
    }
}

/// <summary>
/// Represents an item to sync (internal use only, no SyncQueue table)
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
