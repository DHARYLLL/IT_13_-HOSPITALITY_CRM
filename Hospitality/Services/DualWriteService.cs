using Hospitality.Database;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Hospitality.Services;

/// <summary>
/// Service that handles dual-write operations to both local and online databases.
/// When online: writes to both databases simultaneously.
/// When offline: writes to local database and queues for sync.
/// </summary>
public class DualWriteService
{
    private readonly ConnectivityService _connectivity;
 private readonly SyncService _syncService;

  public DualWriteService(ConnectivityService connectivity, SyncService syncService)
    {
     _connectivity = connectivity;
   _syncService = syncService;
        Console.WriteLine("?? DualWriteService initialized");
    }

    /// <summary>
    /// Executes a write operation on both local and online databases when online,
    /// or only local with sync queue when offline.
    /// </summary>
    /// <param name="entityType">Type of entity (Booking, Payment, etc.)</param>
    /// <param name="tableName">Database table name</param>
    /// <param name="changeType">INSERT, UPDATE, or DELETE</param>
    /// <param name="localAction">Action to execute on local database, returns the entity ID</param>
    /// <param name="onlineAction">Action to execute on online database (optional, uses same query if null)</param>
    /// <returns>The entity ID from the operation</returns>
    public async Task<int> ExecuteWriteAsync(
        string entityType,
        string tableName,
        string changeType,
        Func<SqlConnection, SqlTransaction?, Task<int>> localAction,
        Func<SqlConnection, SqlTransaction?, int, Task<bool>>? onlineAction = null)
    {
        int entityId = 0;
        bool isOnline = await _connectivity.CheckOnlineDatabaseAsync();

        Console.WriteLine($"?? DualWrite: {changeType} {entityType} - Mode: {(isOnline ? "Online (dual-write)" : "Offline (pending)")}");

        // Step 1: Always write to local database first
        using var localCon = DbConnection.GetLocalConnection();
 await localCon.OpenAsync();

     using var localTx = (SqlTransaction)(await localCon.BeginTransactionAsync());

   try
        {
       entityId = await localAction(localCon, localTx);

    // Mark as pending sync initially
            await UpdateSyncStatusAsync(localCon, localTx, tableName, entityId, "pending");

   await localTx.CommitAsync();
      Console.WriteLine($"? Local write successful: {entityType} #{entityId}");
     }
    catch (Exception ex)
        {
            await localTx.RollbackAsync();
            Console.WriteLine($"? Local write failed: {ex.Message}");
            throw;
     }

        // Step 2: If online, write to online database immediately
        if (isOnline && entityId > 0)
      {
      try
            {
   using var onlineCon = DbConnection.GetOnlineConnection();
 await onlineCon.OpenAsync();
                Console.WriteLine($"? Connected to online database for {entityType} #{entityId}");

                using var onlineTx = (SqlTransaction)(await onlineCon.BeginTransactionAsync());

       try
   {
bool onlineSuccess = false;

    if (onlineAction != null)
    {
// Custom online action provided
                onlineSuccess = await onlineAction(onlineCon, onlineTx, entityId);
         }
 else
 {
          // Use default sync method - need fresh local connection for reading
        using var localConForRead = DbConnection.GetLocalConnection();
         await localConForRead.OpenAsync();
      onlineSuccess = await SyncEntityToOnlineAsync(entityType, entityId, localConForRead, onlineCon, onlineTx);
                    }

      if (onlineSuccess)
          {
             await onlineTx.CommitAsync();

              // Mark as synced in local database
       await MarkAsSyncedAsync(tableName, entityId);
       Console.WriteLine($"? Online write successful: {entityType} #{entityId}");
         }
 else
    {
      await onlineTx.RollbackAsync();
  // Stays as 'pending' in local DB for later sync
    Console.WriteLine($"?? Online write failed, will retry later: {entityType} #{entityId}");
          }
            }
 catch (Exception ex)
           {
   await onlineTx.RollbackAsync();
    // Stays as 'pending' in local DB for later sync
         Console.WriteLine($"?? Online write error, will retry later: {ex.Message}");
   }
            }
          catch (Exception ex)
      {
 // Connection to online failed, record stays as 'pending' for sync
     Console.WriteLine($"?? Cannot connect to online database, will sync later: {ex.Message}");
     }
        }
   else if (entityId > 0)
 {
            // Offline - record stays as 'pending' for sync
            Console.WriteLine($"?? Offline mode: {entityType} #{entityId} marked pending, will sync when online");
        }

     return entityId;
    }

    /// <summary>
    /// Executes a write operation that doesn't return an ID (UPDATE/DELETE)
    /// </summary>
    public async Task<bool> ExecuteWriteAsync(
        string entityType,
      string tableName,
        string changeType,
        int entityId,
        Func<SqlConnection, SqlTransaction?, Task<bool>> localAction,
        Func<SqlConnection, SqlTransaction?, Task<bool>>? onlineAction = null)
    {
        bool isOnline = await _connectivity.CheckOnlineDatabaseAsync();

        Console.WriteLine($"?? DualWrite: {changeType} {entityType} #{entityId} - Mode: {(isOnline ? "Online (dual-write)" : "Offline (pending)")}");

      // Step 1: Always write to local database first
        using var localCon = DbConnection.GetLocalConnection();
        await localCon.OpenAsync();

        using var localTx = await localCon.BeginTransactionAsync() as SqlTransaction;

try
   {
      bool localSuccess = await localAction(localCon, localTx);

        if (!localSuccess)
            {
     await localTx!.RollbackAsync();
            Console.WriteLine($"? Local {changeType} failed for {entityType} #{entityId}");
    return false;
   }

    // Mark as pending sync
          await UpdateSyncStatusAsync(localCon, localTx, tableName, entityId, "pending");

   await localTx!.CommitAsync();
         Console.WriteLine($"? Local {changeType} successful: {entityType} #{entityId}");
        }
        catch (Exception ex)
        {
 await localTx!.RollbackAsync();
            Console.WriteLine($"? Local {changeType} failed: {ex.Message}");
    throw;
        }

        // Step 2: If online, write to online database immediately
        if (isOnline)
   {
        try
     {
   using var onlineCon = DbConnection.GetOnlineConnection();
await onlineCon.OpenAsync();

  using var onlineTx = await onlineCon.BeginTransactionAsync() as SqlTransaction;

            try
        {
          bool onlineSuccess = false;

          if (onlineAction != null)
         {
         onlineSuccess = await onlineAction(onlineCon, onlineTx);
   }
         else
      {
   // Use default sync method
            using var localCon2 = DbConnection.GetLocalConnection();
        await localCon2.OpenAsync();
 onlineSuccess = await SyncEntityToOnlineAsync(entityType, entityId, localCon2, onlineCon, onlineTx);
             }

               if (onlineSuccess)
{
               await onlineTx!.CommitAsync();
  await MarkAsSyncedAsync(tableName, entityId);
        Console.WriteLine($"? Online {changeType} successful: {entityType} #{entityId}");
       }
         else
         {
              await onlineTx!.RollbackAsync();
          // Stays as 'pending' in local DB for later sync
     Console.WriteLine($"?? Online {changeType} failed, will retry later");
      }
      }
         catch (Exception ex)
    {
    await onlineTx!.RollbackAsync();
        // Stays as 'pending' in local DB for later sync
       Console.WriteLine($"?? Online {changeType} error, will retry later: {ex.Message}");
        }
            }
        catch (Exception ex)
     {
 // Connection to online failed, record stays as 'pending' for sync
    Console.WriteLine($"?? Cannot connect to online database, will sync later: {ex.Message}");
            }
        }
        else
 {
          // Offline - record stays as 'pending' for sync
            Console.WriteLine($"?? Offline mode: {entityType} #{entityId} marked pending, will sync when online");
  }

        return true;
    }

    /// <summary>
    /// Syncs a single entity from local to online database
    /// </summary>
    private async Task<bool> SyncEntityToOnlineAsync(
        string entityType,
        int entityId,
        SqlConnection localCon,
        SqlConnection onlineCon,
        SqlTransaction? onlineTx)
    {
        try
        {
   switch (entityType)
          {
                case "Booking":
        return await SyncBookingToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
      case "Payment":
   return await SyncPaymentToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
              case "Message":
             return await SyncMessageToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
      case "Room":
       return await SyncRoomToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
        case "User":
      return await SyncUserToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
              case "Client":
  return await SyncClientToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
     case "LoyaltyProgram":
              return await SyncLoyaltyProgramToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
            case "LoyaltyTransaction":
               return await SyncLoyaltyTransactionToOnlineAsync(entityId, localCon, onlineCon, onlineTx);
  default:
    Console.WriteLine($"?? Unknown entity type for sync: {entityType}");
       return false;
        }
        }
        catch (Exception ex)
        {
 Console.WriteLine($"? Error syncing {entityType} #{entityId}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> SyncBookingToOnlineAsync(int bookingId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
        string selectSql = "SELECT * FROM Bookings WHERE booking_id = @id";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", bookingId);

        using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return true; // Record doesn't exist

        // Store values before closing reader
        var bookingIdValue = reader["booking_id"];
        var clientId = reader["client_id"];
        var checkInDate = reader["check-in_date"];
 var checkOutDate = reader["check-out_date"];
        var personCount = reader["person_count"];
        var bookingStatus = reader["booking_status"] ?? DBNull.Value;
  var clientRequest = reader["client_request"] ?? DBNull.Value;
   var checkInTime = reader["check-in_time"] ?? DBNull.Value;
        var checkOutTime = reader["check-out_time"] ?? DBNull.Value;

        await reader.CloseAsync();

   // Enable IDENTITY_INSERT
        string enableIdentitySql = "SET IDENTITY_INSERT Bookings ON;";
        using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
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
            client_request = @client_request,
   [check-in_time] = @check_in_time,
              [check-out_time] = @check_out_time
            WHEN NOT MATCHED THEN
  INSERT (booking_id, client_id, [check-in_date], [check-out_date], person_count, booking_status, client_request, [check-in_time], [check-out_time])
      VALUES (@booking_id, @client_id, @check_in_date, @check_out_date, @person_count, @booking_status, @client_request, @check_in_time, @check_out_time);";

    using var mergeCmd = new SqlCommand(mergeSql, onlineCon, tx);
   mergeCmd.Parameters.AddWithValue("@booking_id", bookingIdValue);
     mergeCmd.Parameters.AddWithValue("@client_id", clientId);
       mergeCmd.Parameters.AddWithValue("@check_in_date", checkInDate);
  mergeCmd.Parameters.AddWithValue("@check_out_date", checkOutDate);
          mergeCmd.Parameters.AddWithValue("@person_count", personCount);
            mergeCmd.Parameters.AddWithValue("@booking_status", bookingStatus);
         mergeCmd.Parameters.AddWithValue("@client_request", clientRequest);
            mergeCmd.Parameters.AddWithValue("@check_in_time", checkInTime);
 mergeCmd.Parameters.AddWithValue("@check_out_time", checkOutTime);

        await mergeCmd.ExecuteNonQueryAsync();
     Console.WriteLine($"? Booking #{bookingId} synced to online database");
      }
     finally
    {
            string disableIdentitySql = "SET IDENTITY_INSERT Bookings OFF;";
         using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
          await disableCmd.ExecuteNonQueryAsync();
        }

        // Also sync booking_rooms
        await SyncBookingRoomsToOnlineAsync(bookingId, localCon, onlineCon, tx);

        return true;
    }

    private async Task SyncBookingRoomsToOnlineAsync(int bookingId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
  {
        // Delete existing and re-insert
  string deleteSql = "DELETE FROM Booking_rooms WHERE booking_id = @bookingId";
        using var deleteCmd = new SqlCommand(deleteSql, onlineCon, tx);
        deleteCmd.Parameters.AddWithValue("@bookingId", bookingId);
        await deleteCmd.ExecuteNonQueryAsync();

        string selectSql = "SELECT room_id FROM Booking_rooms WHERE booking_id = @bookingId";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@bookingId", bookingId);

        using var reader = await selectCmd.ExecuteReaderAsync();
        var roomIds = new List<int>();
        while (await reader.ReadAsync())
        {
       roomIds.Add(reader.GetInt32(0));
      }
 await reader.CloseAsync();

        foreach (var roomId in roomIds)
  {
         string insertSql = "INSERT INTO Booking_rooms (booking_id, room_id) VALUES (@bookingId, @roomId)";
            using var insertCmd = new SqlCommand(insertSql, onlineCon, tx);
    insertCmd.Parameters.AddWithValue("@bookingId", bookingId);
            insertCmd.Parameters.AddWithValue("@roomId", roomId);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<bool> SyncPaymentToOnlineAsync(int paymentId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
        string selectSql = "SELECT * FROM Payments WHERE payment_id = @id";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", paymentId);

        using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return true;

        // Store values before closing reader
        var paymentIdValue = reader["payment_id"];
     var bookingId = reader["booking_id"];
     var amount = reader["amount"];
        var paymentMethod = reader["payment_method"] ?? DBNull.Value;
 var paymentStatus = reader["payment_status"] ?? DBNull.Value;
        var paymentType = reader["payment_type"] ?? DBNull.Value;
 var paymentDate = reader["payment_date"] ?? DBNull.Value;
        var paymentIntentId = reader["payment_intent_id"] ?? DBNull.Value;
var checkoutSessionId = reader["checkout_session_id"] ?? DBNull.Value;
        var notes = reader["notes"] ?? DBNull.Value;

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT
     string enableIdentitySql = "SET IDENTITY_INSERT Payments ON;";
        using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
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
        payment_date = @payment_date,
       payment_intent_id = @payment_intent_id,
  checkout_session_id = @checkout_session_id,
      notes = @notes
 WHEN NOT MATCHED THEN
       INSERT (payment_id, booking_id, amount, payment_method, payment_status, payment_type, payment_date, payment_intent_id, checkout_session_id, notes)
         VALUES (@payment_id, @booking_id, @amount, @payment_method, @payment_status, @payment_type, @payment_date, @payment_intent_id, @checkout_session_id, @notes);";

  using var mergeCmd = new SqlCommand(mergeSql, onlineCon, tx);
      mergeCmd.Parameters.AddWithValue("@payment_id", paymentIdValue);
       mergeCmd.Parameters.AddWithValue("@booking_id", bookingId);
         mergeCmd.Parameters.AddWithValue("@amount", amount);
      mergeCmd.Parameters.AddWithValue("@payment_method", paymentMethod);
            mergeCmd.Parameters.AddWithValue("@payment_status", paymentStatus);
        mergeCmd.Parameters.AddWithValue("@payment_type", paymentType);
         mergeCmd.Parameters.AddWithValue("@payment_date", paymentDate);
         mergeCmd.Parameters.AddWithValue("@payment_intent_id", paymentIntentId);
       mergeCmd.Parameters.AddWithValue("@checkout_session_id", checkoutSessionId);
       mergeCmd.Parameters.AddWithValue("@notes", notes);

          await mergeCmd.ExecuteNonQueryAsync();
 Console.WriteLine($"? Payment #{paymentId} synced to online database");
        }
    finally
    {
 string disableIdentitySql = "SET IDENTITY_INSERT Payments OFF;";
 using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
       await disableCmd.ExecuteNonQueryAsync();
        }

 return true;
    }

    private async Task<bool> SyncMessageToOnlineAsync(int messageId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
  string selectSql = "SELECT * FROM Messages WHERE message_id = @id";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", messageId);

        using var reader = await selectCmd.ExecuteReaderAsync();
   if (!await reader.ReadAsync()) return true;

        // Store values before closing reader
     var messageIdValue = reader["message_id"];
        var clientId = reader["client_id"];
        var messageSubject = reader["message_subject"] ?? DBNull.Value;
   var messageBody = reader["message_body"] ?? DBNull.Value;
        var messageType = reader["message_type"] ?? DBNull.Value;
        var isRead = reader["is_read"];
        var sentDate = reader["sent_date"];

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT
     string enableIdentitySql = "SET IDENTITY_INSERT Messages ON;";
    using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
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

       using var mergeCmd = new SqlCommand(mergeSql, onlineCon, tx);
         mergeCmd.Parameters.AddWithValue("@message_id", messageIdValue);
 mergeCmd.Parameters.AddWithValue("@client_id", clientId);
     mergeCmd.Parameters.AddWithValue("@message_subject", messageSubject);
         mergeCmd.Parameters.AddWithValue("@message_body", messageBody);
      mergeCmd.Parameters.AddWithValue("@message_type", messageType);
         mergeCmd.Parameters.AddWithValue("@is_read", isRead);
            mergeCmd.Parameters.AddWithValue("@sent_date", sentDate);

  await mergeCmd.ExecuteNonQueryAsync();
    Console.WriteLine($"? Message #{messageId} synced to online database");
    }
 finally
        {
            string disableIdentitySql = "SET IDENTITY_INSERT Messages OFF;";
            using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
   await disableCmd.ExecuteNonQueryAsync();
     }

 return true;
    }

    private async Task<bool> SyncRoomToOnlineAsync(int roomId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
        string selectSql = "SELECT * FROM rooms WHERE room_id = @id";
    using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", roomId);

     using var reader = await selectCmd.ExecuteReaderAsync();
   if (!await reader.ReadAsync()) return true;

     // Store values before closing reader
        var roomIdValue = reader["room_id"];
        var roomName = reader["room_name"];
        var roomNumber = reader["room_number"];
      var roomFloor = reader["room_floor"];
 var roomPrice = reader["room_price"];
        var roomStatus = reader["room_status"];
        var roomPicture = reader["room_picture"] ?? DBNull.Value;
    var roomAmenities = reader["room_amenities"] ?? DBNull.Value;

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT to allow explicit room_id values
     string enableIdentitySql = "SET IDENTITY_INSERT rooms ON;";
        using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
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

  using var mergeCmd = new SqlCommand(mergeSql, onlineCon, tx);
      mergeCmd.Parameters.AddWithValue("@room_id", roomIdValue);
     mergeCmd.Parameters.AddWithValue("@room_name", roomName);
            mergeCmd.Parameters.AddWithValue("@room_number", roomNumber);
   mergeCmd.Parameters.AddWithValue("@room_floor", roomFloor);
            mergeCmd.Parameters.AddWithValue("@room_price", roomPrice);
      mergeCmd.Parameters.AddWithValue("@room_status", roomStatus);
         mergeCmd.Parameters.AddWithValue("@room_picture", roomPicture);
         mergeCmd.Parameters.AddWithValue("@room_amenities", roomAmenities);

            await mergeCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"? Room #{roomId} synced to online database");
        }
        finally
        {
         // Always disable IDENTITY_INSERT
            string disableIdentitySql = "SET IDENTITY_INSERT rooms OFF;";
            using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
      await disableCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    private async Task<bool> SyncUserToOnlineAsync(int userId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
        string selectSql = "SELECT * FROM Users WHERE user_id = @id";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", userId);

        using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return true;

        // Store values before closing reader - using correct column names
        var userIdValue = reader["user_id"];
        var roleId = reader["role_id"];
        var userFname = reader["user_fname"] ?? DBNull.Value;
        var userMname = reader["user_mname"] ?? DBNull.Value;
        var userLname = reader["user_lname"] ?? DBNull.Value;
        var userBirthDate = reader["user_brith_date"] ?? DBNull.Value;
        var userEmail = reader["user_email"] ?? DBNull.Value;
        var userContact = reader["user_contact_number"] ?? DBNull.Value;
        var userPassword = reader["user_password"] ?? DBNull.Value;

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT
        string enableIdentitySql = "SET IDENTITY_INSERT Users ON;";
        using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
        await enableCmd.ExecuteNonQueryAsync();

        try
        {
            // Use DELETE + INSERT for simplicity (matches SyncService approach)
            string deleteSql = "DELETE FROM Users WHERE user_id = @user_id;";
            using var deleteCmd = new SqlCommand(deleteSql, onlineCon, tx);
            deleteCmd.Parameters.AddWithValue("@user_id", userIdValue);
            await deleteCmd.ExecuteNonQueryAsync();

            string insertSql = @"
                INSERT INTO Users (user_id, role_id, user_fname, user_mname, user_lname, user_brith_date, user_email, user_contact_number, user_password)
                VALUES (@user_id, @role_id, @user_fname, @user_mname, @user_lname, @user_brith_date, @user_email, @user_contact_number, @user_password);";

            using var insertCmd = new SqlCommand(insertSql, onlineCon, tx);
            insertCmd.Parameters.AddWithValue("@user_id", userIdValue);
            insertCmd.Parameters.AddWithValue("@role_id", roleId);
            insertCmd.Parameters.AddWithValue("@user_fname", userFname);
            insertCmd.Parameters.AddWithValue("@user_mname", userMname);
            insertCmd.Parameters.AddWithValue("@user_lname", userLname);
            insertCmd.Parameters.AddWithValue("@user_brith_date", userBirthDate);
            insertCmd.Parameters.AddWithValue("@user_email", userEmail);
            insertCmd.Parameters.AddWithValue("@user_contact_number", userContact);
            insertCmd.Parameters.AddWithValue("@user_password", userPassword);

            await insertCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"✅ User #{userId} synced to online database");
        }
        finally
        {
            string disableIdentitySql = "SET IDENTITY_INSERT Users OFF;";
            using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
            await disableCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    private async Task<bool> SyncClientToOnlineAsync(int clientId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
        string selectSql = "SELECT * FROM Clients WHERE client_id = @id";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", clientId);

        using var reader = await selectCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return true;

        // Store values before closing reader - Clients table only has client_id and user_id
        var clientIdValue = reader["client_id"];
        var userId = reader["user_id"];

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT
        string enableIdentitySql = "SET IDENTITY_INSERT Clients ON;";
        using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
        await enableCmd.ExecuteNonQueryAsync();

        try
        {
            // Use DELETE + INSERT for simplicity (matches SyncService approach)
            string deleteSql = "DELETE FROM Clients WHERE client_id = @client_id;";
            using var deleteCmd = new SqlCommand(deleteSql, onlineCon, tx);
            deleteCmd.Parameters.AddWithValue("@client_id", clientIdValue);
            await deleteCmd.ExecuteNonQueryAsync();

            string insertSql = @"
                INSERT INTO Clients (client_id, user_id)
                VALUES (@client_id, @user_id);";

            using var insertCmd = new SqlCommand(insertSql, onlineCon, tx);
            insertCmd.Parameters.AddWithValue("@client_id", clientIdValue);
            insertCmd.Parameters.AddWithValue("@user_id", userId);

            await insertCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"✅ Client #{clientId} synced to online database");
        }
        finally
        {
            string disableIdentitySql = "SET IDENTITY_INSERT Clients OFF;";
            using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
            await disableCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    private async Task<bool> SyncLoyaltyProgramToOnlineAsync(int loyaltyId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
 string selectSql = "SELECT * FROM LoyaltyPrograms WHERE loyalty_id = @id";
 using var selectCmd = new SqlCommand(selectSql, localCon);
      selectCmd.Parameters.AddWithValue("@id", loyaltyId);

     using var reader = await selectCmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return true;

        // Store values before closing reader
        var loyaltyIdValue = reader["loyalty_id"];
        var clientId = reader["client_id"];
        var currentPoints = reader["current_points"];
        var lifetimePoints = reader["lifetime_points"];
        var currentTier = reader["current_tier"];
        var tierStartDate = reader["tier_start_date"] ?? DBNull.Value;
        var tierEndDate = reader["tier_end_date"] ?? DBNull.Value;

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT
string enableIdentitySql = "SET IDENTITY_INSERT LoyaltyPrograms ON;";
    using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
  await enableCmd.ExecuteNonQueryAsync();

        try
        {
            string mergeSql = @"
    MERGE INTO LoyaltyPrograms AS target
USING (SELECT @loyalty_id AS loyalty_id) AS source
        ON target.loyalty_id = source.loyalty_id
     WHEN MATCHED THEN
  UPDATE SET 
       client_id = @client_id,
          current_points = @current_points,
      lifetime_points = @lifetime_points,
         current_tier = @current_tier,
      tier_start_date = @tier_start_date,
      tier_end_date = @tier_end_date
         WHEN NOT MATCHED THEN
INSERT (loyalty_id, client_id, current_points, lifetime_points, current_tier, tier_start_date, tier_end_date)
     VALUES (@loyalty_id, @client_id, @current_points, @lifetime_points, @current_tier, @tier_start_date, @tier_end_date);";

            using var mergeCmd = new SqlCommand(mergeSql, onlineCon, tx);
            mergeCmd.Parameters.AddWithValue("@loyalty_id", loyaltyIdValue);
    mergeCmd.Parameters.AddWithValue("@client_id", clientId);
            mergeCmd.Parameters.AddWithValue("@current_points", currentPoints);
            mergeCmd.Parameters.AddWithValue("@lifetime_points", lifetimePoints);
            mergeCmd.Parameters.AddWithValue("@current_tier", currentTier);
        mergeCmd.Parameters.AddWithValue("@tier_start_date", tierStartDate);
            mergeCmd.Parameters.AddWithValue("@tier_end_date", tierEndDate);

         await mergeCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"? LoyaltyProgram #{loyaltyId} synced to online database");
      }
        finally
   {
    string disableIdentitySql = "SET IDENTITY_INSERT LoyaltyPrograms OFF;";
            using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
            await disableCmd.ExecuteNonQueryAsync();
        }

        return true;
    }

    private async Task<bool> SyncLoyaltyTransactionToOnlineAsync(int transactionId, SqlConnection localCon, SqlConnection onlineCon, SqlTransaction? tx)
    {
        string selectSql = "SELECT * FROM LoyaltyTransactions WHERE transaction_id = @id";
        using var selectCmd = new SqlCommand(selectSql, localCon);
        selectCmd.Parameters.AddWithValue("@id", transactionId);

     using var reader = await selectCmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return true;

        // Store values before closing reader
        var transactionIdValue = reader["transaction_id"];
        var loyaltyId = reader["loyalty_id"];
        var pointsAmount = reader["points_amount"];
        var transactionType = reader["transaction_type"];
        var description = reader["description"] ?? DBNull.Value;
      var referenceId = reader["reference_id"] ?? DBNull.Value;
  var transactionDate = reader["transaction_date"];

        await reader.CloseAsync();

        // Enable IDENTITY_INSERT
        string enableIdentitySql = "SET IDENTITY_INSERT LoyaltyTransactions ON;";
        using var enableCmd = new SqlCommand(enableIdentitySql, onlineCon, tx);
  await enableCmd.ExecuteNonQueryAsync();

        try
        {
            string mergeSql = @"
            MERGE INTO LoyaltyTransactions AS target
  USING (SELECT @transaction_id AS transaction_id) AS source
 ON target.transaction_id = source.transaction_id
          WHEN MATCHED THEN
  UPDATE SET 
     loyalty_id = @loyalty_id,
           points_amount = @points_amount,
      transaction_type = @transaction_type,
             description = @description,
     reference_id = @reference_id,
    transaction_date = @transaction_date
            WHEN NOT MATCHED THEN
       INSERT (transaction_id, loyalty_id, points_amount, transaction_type, description, reference_id, transaction_date)
   VALUES (@transaction_id, @loyalty_id, @points_amount, @transaction_type, @description, @reference_id, @transaction_date);";

 using var mergeCmd = new SqlCommand(mergeSql, onlineCon, tx);
       mergeCmd.Parameters.AddWithValue("@transaction_id", transactionIdValue);
   mergeCmd.Parameters.AddWithValue("@loyalty_id", loyaltyId);
    mergeCmd.Parameters.AddWithValue("@points_amount", pointsAmount);
       mergeCmd.Parameters.AddWithValue("@transaction_type", transactionType);
            mergeCmd.Parameters.AddWithValue("@description", description);
  mergeCmd.Parameters.AddWithValue("@reference_id", referenceId);
 mergeCmd.Parameters.AddWithValue("@transaction_date", transactionDate);

            await mergeCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"? LoyaltyTransaction #{transactionId} synced to online database");
        }
     finally
        {
        string disableIdentitySql = "SET IDENTITY_INSERT LoyaltyTransactions OFF;";
    using var disableCmd = new SqlCommand(disableIdentitySql, onlineCon, tx);
   await disableCmd.ExecuteNonQueryAsync();
        }

  return true;
    }

    /// <summary>
    /// Updates sync_status column in the local database
    /// </summary>
    private async Task UpdateSyncStatusAsync(SqlConnection con, SqlTransaction? tx, string tableName, int entityId, string status)
    {
  try
        {
         string pkColumn = GetPrimaryKeyColumn(tableName);
            string sql = $"UPDATE {tableName} SET sync_status = @status, last_modified = GETDATE() WHERE {pkColumn} = @id";

            using var cmd = new SqlCommand(sql, con, tx);
    cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@id", entityId);
    await cmd.ExecuteNonQueryAsync();
        }
catch
 {
     // Table might not have sync_status column, ignore
  }
    }

    /// <summary>
  /// Marks a record as synced after successful online write
    /// </summary>
    private async Task MarkAsSyncedAsync(string tableName, int entityId)
    {
        try
        {
            using var con = DbConnection.GetLocalConnection();
            await con.OpenAsync();

            string pkColumn = GetPrimaryKeyColumn(tableName);
          string sql = $"UPDATE {tableName} SET sync_status = 'synced', last_modified = GETDATE() WHERE {pkColumn} = @id";

            using var cmd = new SqlCommand(sql, con);
     cmd.Parameters.AddWithValue("@id", entityId);
 await cmd.ExecuteNonQueryAsync();
        }
  catch
        {
            // Table might not have sync_status column, ignore
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
}
