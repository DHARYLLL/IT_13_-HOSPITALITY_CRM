using Microsoft.Data.SqlClient;
using System.Data;

namespace Hospitality.Services
{
    public class MessageService
    {
        // Get all messages for a client
        public async Task<List<Models.Message>> GetClientMessagesAsync(int clientId, Models.MessageFilter? filter = null)
        {
      var messages = new List<Models.Message>();
    
       using var con = Database.DbConnection.GetConnection();
     await con.OpenAsync();
            
            var sql = @"
 SELECT m.*, 
             c.client_id,
    COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name,
      b.booking_id as booking_reference
          FROM Messages m
          LEFT JOIN Clients c ON m.client_id = c.client_id
        LEFT JOIN Users u ON c.user_id = u.user_id
        LEFT JOIN Bookings b ON m.booking_id = b.booking_id
    WHERE m.client_id = @clientId";
 
            // Apply filters
            if (filter != null)
            {
     if (filter.FilterType == "Unread")
     {
  sql += " AND m.is_read = 0";
    }
      else if (filter.FilterType == "Stays")
     {
          sql += " AND m.message_type = 'service'";
     }
     else if (filter.FilterType == "Offers")
      {
             sql += " AND m.message_type = 'offer'";
             }
                
      if (filter.TimeRange == "Last 30 days")
     {
            sql += " AND m.sent_date >= DATEADD(day, -30, GETDATE())";
    }
    else if (filter.TimeRange == "Last 90 days")
                {
    sql += " AND m.sent_date >= DATEADD(day, -90, GETDATE())";
      }
   }
        
            sql += " ORDER BY m.sent_date DESC";
     
    using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@clientId", clientId);
   
    using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
    {
       messages.Add(ReadMessage(reader));
   }
    
   return messages;
     }
      
        // Get unread message count
      public async Task<int> GetUnreadCountAsync(int clientId)
        {
            using var con = Database.DbConnection.GetConnection();
      await con.OpenAsync();
    
     var sql = "SELECT COUNT(*) FROM Messages WHERE client_id = @clientId AND is_read = 0";
            using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", clientId);
    
   return (int)await cmd.ExecuteScalarAsync();
   }
        
        // Mark message as read
        public async Task MarkAsReadAsync(int messageId)
        {
      using var con = Database.DbConnection.GetConnection();
   await con.OpenAsync();
      
         var sql = "UPDATE Messages SET is_read = 1 WHERE message_id = @messageId";
     using var cmd = new SqlCommand(sql, con);
       cmd.Parameters.AddWithValue("@messageId", messageId);
   
     await cmd.ExecuteNonQueryAsync();
      }
        
    // Mark all messages as read for a client
        public async Task MarkAllAsReadAsync(int clientId)
        {
            using var con = Database.DbConnection.GetConnection();
         await con.OpenAsync();
      
            var sql = "UPDATE Messages SET is_read = 1 WHERE client_id = @clientId";
         using var cmd = new SqlCommand(sql, con);
     cmd.Parameters.AddWithValue("@clientId", clientId);
            
            await cmd.ExecuteNonQueryAsync();
        }
        
        // Send email to hotel team (save as outgoing message)
   public async Task<int> SendEmailToHotelAsync(Models.EmailRequest request)
        {
       using var con = Database.DbConnection.GetConnection();
  await con.OpenAsync();
            
            var sql = @"
    INSERT INTO Messages (
           client_id, message_subject, message_body, message_type, 
      is_read, sent_date, regarding_text
         )
      VALUES (
   @clientId, @subject, @body, 'outgoing', 
        1, GETDATE(), @regarding
         );
      SELECT CAST(SCOPE_IDENTITY() AS INT);";
     
       using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", request.client_id);
            cmd.Parameters.AddWithValue("@subject", request.subject ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@body", request.message_body ?? (object)DBNull.Value);
         cmd.Parameters.AddWithValue("@regarding", request.regarding ?? (object)DBNull.Value);
      
          return (int)await cmd.ExecuteScalarAsync();
 }
        
  // Create a new notification/message
     public async Task<int> CreateMessageAsync(Models.Message message)
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();
          
    var sql = @"
    INSERT INTO Messages (
           client_id, message_subject, message_body, message_type, 
   is_read, sent_date, booking_id, 
      action_label, action_url, regarding_text
           )
                VALUES (
      @clientId, @subject, @body, @type, 
    0, GETDATE(), @bookingId,
          @actionLabel, @actionUrl, @regarding
     );
        SELECT CAST(SCOPE_IDENTITY() AS INT);";
            
            using var cmd = new SqlCommand(sql, con);
      cmd.Parameters.AddWithValue("@clientId", message.client_id);
    cmd.Parameters.AddWithValue("@subject", message.message_subject ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@body", message.message_body ?? (object)DBNull.Value);
     cmd.Parameters.AddWithValue("@type", message.message_type ?? (object)DBNull.Value);
 cmd.Parameters.AddWithValue("@bookingId", message.booking_id ?? (object)DBNull.Value);
      cmd.Parameters.AddWithValue("@actionLabel", message.action_label ?? (object)DBNull.Value);
          cmd.Parameters.AddWithValue("@actionUrl", message.action_url ?? (object)DBNull.Value);
 cmd.Parameters.AddWithValue("@regarding", message.regarding_text ?? (object)DBNull.Value);
     
     return (int)await cmd.ExecuteScalarAsync();
        }
        
        // Delete a message
    public async Task DeleteMessageAsync(int messageId)
        {
using var con = Database.DbConnection.GetConnection();
 await con.OpenAsync();
            
        var sql = "DELETE FROM Messages WHERE message_id = @messageId";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@messageId", messageId);
    
await cmd.ExecuteNonQueryAsync();
        }
     
        // Get message by ID
        public async Task<Models.Message?> GetMessageByIdAsync(int messageId)
        {
            using var con = Database.DbConnection.GetConnection();
    await con.OpenAsync();
            
  var sql = @"
          SELECT m.*, 
   COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name
       FROM Messages m
     LEFT JOIN Clients c ON m.client_id = c.client_id
       LEFT JOIN Users u ON c.user_id = u.user_id
         WHERE m.message_id = @messageId";
     
            using var cmd = new SqlCommand(sql, con);
 cmd.Parameters.AddWithValue("@messageId", messageId);
        
    using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
       return ReadMessage(reader);
 }
            
            return null;
 }

      /// <summary>
        /// Get all messages for admin view with optional filtering
  /// </summary>
        public async Task<List<Models.Message>> GetAllMessagesForAdminAsync(Models.MessageFilter? filter = null)
        {
       var messages = new List<Models.Message>();
            
            using var con = Database.DbConnection.GetConnection();
        await con.OpenAsync();
      
   var sql = @"
        SELECT m.*, 
      c.client_id,
   COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name,
 u.user_email as client_email,
      b.booking_id as booking_reference
     FROM Messages m
                LEFT JOIN Clients c ON m.client_id = c.client_id
          LEFT JOIN Users u ON c.user_id = u.user_id
      LEFT JOIN Bookings b ON m.booking_id = b.booking_id
  WHERE 1=1";
            
     // Apply filters
      if (filter != null)
       {
            if (filter.FilterType == "Unassigned")
  {
      sql += " AND m.is_read = 0 AND m.message_type = 'outgoing'";
      }
    else if (filter.FilterType == "Open")
   {
           sql += " AND m.message_type = 'outgoing'";
       }
                else if (filter.FilterType == "Resolved")
        {
      sql += " AND m.is_read = 1 AND m.message_type != 'outgoing'";
      }
             
        if (filter.TimeRange == "Last 30 days")
         {
      sql += " AND m.sent_date >= DATEADD(day, -30, GETDATE())";
         }
                else if (filter.TimeRange == "Last 90 days")
      {
  sql += " AND m.sent_date >= DATEADD(day, -90, GETDATE())";
    }
         }
         
      sql += " ORDER BY m.sent_date DESC";
            
    using var cmd = new SqlCommand(sql, con);
 
         using var reader = await cmd.ExecuteReaderAsync();
   while (await reader.ReadAsync())
      {
     messages.Add(ReadMessage(reader));
      }
            
return messages;
        }

        /// <summary>
 /// Get full conversation thread for a specific client (for admin view)
    /// </summary>
        public async Task<List<Models.Message>> GetConversationAsync(int clientId)
        {
       var messages = new List<Models.Message>();
            
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();
        
       var sql = @"
          SELECT m.*, 
           COALESCE(u.user_fname + ' ' + u.user_lname, 'Guest') as client_name,
     u.user_email as client_email
            FROM Messages m
      LEFT JOIN Clients c ON m.client_id = c.client_id
          LEFT JOIN Users u ON c.user_id = u.user_id
     WHERE m.client_id = @clientId
  ORDER BY m.sent_date ASC";
    
        using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@clientId", clientId);
  
            using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
            {
                messages.Add(ReadMessage(reader));
         }
   
            return messages;
     }

     /// <summary>
      /// Admin replies to a client message
   /// </summary>
        public async Task<int> ReplyToClientAsync(Models.Message reply)
        {
         using var con = Database.DbConnection.GetConnection();
   await con.OpenAsync();
     
      var sql = @"
   INSERT INTO Messages (
        client_id, message_subject, message_body, message_type, 
  is_read, sent_date, booking_id, 
      action_label, action_url, regarding_text
  )
                VALUES (
            @clientId, @subject, @body, @type, 
         0, GETDATE(), @bookingId,
   @actionLabel, @actionUrl, @regarding
    );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
      
       using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@clientId", reply.client_id);
   cmd.Parameters.AddWithValue("@subject", reply.message_subject ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@body", reply.message_body ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@type", reply.message_type ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@bookingId", reply.booking_id ?? (object)DBNull.Value);
  cmd.Parameters.AddWithValue("@actionLabel", reply.action_label ?? (object)DBNull.Value);
   cmd.Parameters.AddWithValue("@actionUrl", reply.action_url ?? (object)DBNull.Value);
cmd.Parameters.AddWithValue("@regarding", reply.regarding_text ?? (object)DBNull.Value);
            
            return (int)await cmd.ExecuteScalarAsync();
        }

        /// <summary>
      /// Mark a conversation as resolved (mark all messages as read)
        /// </summary>
     public async Task MarkConversationResolvedAsync(int clientId)
{
        using var con = Database.DbConnection.GetConnection();
          await con.OpenAsync();
            
   var sql = "UPDATE Messages SET is_read = 1 WHERE client_id = @clientId";
     using var cmd = new SqlCommand(sql, con);
     cmd.Parameters.AddWithValue("@clientId", clientId);
            
   await cmd.ExecuteNonQueryAsync();
     }

/// <summary>
   /// Helper method to read a Message from SqlDataReader
        /// </summary>
   private Models.Message ReadMessage(SqlDataReader reader)
        {
          return new Models.Message
   {
        message_id = reader.GetInt32(reader.GetOrdinal("message_id")),
             client_id = reader.GetInt32(reader.GetOrdinal("client_id")),
        message_subject = reader.IsDBNull(reader.GetOrdinal("message_subject")) ? null : reader.GetString(reader.GetOrdinal("message_subject")),
      message_body = reader.IsDBNull(reader.GetOrdinal("message_body")) ? null : reader.GetString(reader.GetOrdinal("message_body")),
        message_type = reader.IsDBNull(reader.GetOrdinal("message_type")) ? null : reader.GetString(reader.GetOrdinal("message_type")),
                is_read = reader.GetBoolean(reader.GetOrdinal("is_read")),
          sent_date = reader.GetDateTime(reader.GetOrdinal("sent_date")),
      booking_id = reader.IsDBNull(reader.GetOrdinal("booking_id")) ? null : reader.GetInt32(reader.GetOrdinal("booking_id")),
   action_label = reader.IsDBNull(reader.GetOrdinal("action_label")) ? null : reader.GetString(reader.GetOrdinal("action_label")),
                action_url = reader.IsDBNull(reader.GetOrdinal("action_url")) ? null : reader.GetString(reader.GetOrdinal("action_url")),
  regarding_text = reader.IsDBNull(reader.GetOrdinal("regarding_text")) ? null : reader.GetString(reader.GetOrdinal("regarding_text")),
 client_name = reader.IsDBNull(reader.GetOrdinal("client_name")) ? "Guest" : reader.GetString(reader.GetOrdinal("client_name"))
       };
        }
    }
}
