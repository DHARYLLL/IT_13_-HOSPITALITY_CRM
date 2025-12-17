using Hospitality.Database;
using Hospitality.Models;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Hospitality.Services
{
    public class UserService
    {
        private readonly DualWriteService? _dualWriteService;
        private readonly SyncService? _syncService;

        public UserService()
        {
            // Default constructor for backward compatibility
        }

        public UserService(DualWriteService dualWriteService, SyncService syncService)
        {
            _dualWriteService = dualWriteService;
            _syncService = syncService;
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            User? user = null;

            try
            {
                using (SqlConnection con = DbConnection.GetConnection())
                {
                    await con.OpenAsync();

                    // First, get the user by email (without password check in SQL)
                    string query = @"SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, 
                                    U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, R.role_name
                                     FROM users U JOIN Roles R ON U.role_id = R.role_id
                                     WHERE U.user_email = @email";

                    using var cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@email", email);

                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        var storedPassword = reader["user_password"]?.ToString();
                        
                        // Verify password - handle both hashed and plain text (for migration)
                        bool passwordValid = false;
                        if (!string.IsNullOrWhiteSpace(storedPassword))
                        {
                            if (PasswordHasher.IsHashed(storedPassword))
                            {
                                // Password is hashed, verify using BCrypt
                                passwordValid = PasswordHasher.VerifyPassword(password, storedPassword);
                            }
                            else
                            {
                                // Password is plain text (legacy), compare directly
                                // This allows existing users to login while we migrate
                                passwordValid = storedPassword == password;
                                
                                // If login successful with plain text, upgrade to hashed
                                if (passwordValid)
                                {
                                    await UpgradePasswordToHashAsync(reader.GetInt32(reader.GetOrdinal("user_id")), password);
                                }
                            }
                        }

                        if (passwordValid)
                        {
                            // Reset reader position to map user
                            // Since we already read, we need to map from current position
                            user = MapUser(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging but do not rethrow to avoid crashing the UI
                System.Diagnostics.Debug.WriteLine($"Login error: {ex}");
                return null;
            }

            return user;
        }

        private async Task UpgradePasswordToHashAsync(int userId, string plainPassword)
        {
            try
            {
                var hashedPassword = PasswordHasher.HashPassword(plainPassword);
                using var con = DbConnection.GetConnection();
                await con.OpenAsync();
                
                string updateSql = "UPDATE users SET user_password = @hashedPassword WHERE user_id = @userId";
                using var cmd = new SqlCommand(updateSql, con);
                cmd.Parameters.AddWithValue("@hashedPassword", hashedPassword);
                cmd.Parameters.AddWithValue("@userId", userId);
                
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Log but don't throw - password upgrade is not critical for login
                System.Diagnostics.Debug.WriteLine($"Password upgrade error: {ex}");
            }
        }

        public async Task<int> RegisterAsync(User user)
        {
            try
            {
                // If DualWriteService is available, use it for dual-write
                if (_dualWriteService != null)
                {
                    // First, determine role name if not supplied
                    string? userRoleName = user.roleName;
                    if (string.IsNullOrWhiteSpace(userRoleName))
                    {
                        using var roleCon = DbConnection.GetConnection();
                        await roleCon.OpenAsync();
                        using var roleCmd = new SqlCommand("SELECT role_name FROM Roles WHERE role_id=@rid", roleCon);
                        roleCmd.Parameters.AddWithValue("@rid", user.role_id);
                        var rn = await roleCmd.ExecuteScalarAsync();
                        userRoleName = rn?.ToString();
                    }

                    // Register user with DualWriteService
                    int registeredUserId = await _dualWriteService.ExecuteWriteAsync(
                        "User",
                        "Users",
                        "INSERT",
                        async (dbCon, dbTx) =>
                        {
                            // Check if sync_status column exists
                            bool hasSyncColumn = await HasSyncStatusColumnAsync(dbCon, dbTx, "Users");

                            string sql = hasSyncColumn
                                ? @"INSERT INTO Users (role_id, user_fname, user_mname, user_lname, user_brith_date, user_email, user_contact_number, user_password, sync_status)
                                   VALUES (@role_id, @fname, @mname, @lname, @birth, @email, @contact, @password, 'pending'); 
                                   SELECT CAST(SCOPE_IDENTITY() AS int);"
                                : @"INSERT INTO Users (role_id, user_fname, user_mname, user_lname, user_brith_date, user_email, user_contact_number, user_password)
                                   VALUES (@role_id, @fname, @mname, @lname, @birth, @email, @contact, @password); 
                                   SELECT CAST(SCOPE_IDENTITY() AS int);";

                            using var cmd = new SqlCommand(sql, dbCon, dbTx);
                            cmd.Parameters.AddWithValue("@role_id", user.role_id);
                            cmd.Parameters.AddWithValue("@fname", (object?)user.user_fname ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@mname", (object?)user.user_mname ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@lname", (object?)user.user_lname ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@birth", (object?)user.user_brith_date ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@email", (object?)user.user_email ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@contact", (object?)user.user_contact_number ?? DBNull.Value);
                            // Hash password before storing
                            var passwordToStore = string.IsNullOrWhiteSpace(user.user_password) 
                                ? DBNull.Value 
                                : (object)PasswordHasher.HashPassword(user.user_password);
                            cmd.Parameters.AddWithValue("@password", passwordToStore);

                            var idObj = await cmd.ExecuteScalarAsync();
                            return (idObj is int i) ? i : Convert.ToInt32(idObj);
                        });

                    // After user is created, create Client/Employee record if needed
                    if (!string.IsNullOrWhiteSpace(userRoleName))
                    {
                        var roleLower = userRoleName.ToLowerInvariant();
                        if (roleLower == "client")
                        {
                            // Insert into Clients using DualWriteService
                            await _dualWriteService.ExecuteWriteAsync(
                                "Client",
                                "Clients",
                                "INSERT",
                                async (clientCon, clientTx) =>
                                {
                                    bool hasSyncColumn = await HasSyncStatusColumnAsync(clientCon, clientTx, "Clients");
                                    string clientSql = hasSyncColumn
                                        ? @"INSERT INTO Clients (user_id, sync_status) VALUES (@user_id, 'pending'); 
                                           SELECT CAST(SCOPE_IDENTITY() AS int);"
                                        : @"INSERT INTO Clients (user_id) VALUES (@user_id); 
                                           SELECT CAST(SCOPE_IDENTITY() AS int);";

                                    using var clientCmd = new SqlCommand(clientSql, clientCon, clientTx);
                                    clientCmd.Parameters.AddWithValue("@user_id", registeredUserId);
                                    var clientIdObj = await clientCmd.ExecuteScalarAsync();
                                    return (clientIdObj is int ci) ? ci : Convert.ToInt32(clientIdObj);
                                });
                        }
                        else if (roleLower == "staff" || roleLower == "admin")
                        {
                            // Insert into Employees (note: Employees table may not have sync support yet)
                            using var empCon = DbConnection.GetConnection();
                            await empCon.OpenAsync();
                            string employeeSql = "INSERT INTO Employees (user_id) VALUES (@user_id);";
                            using var empCmd = new SqlCommand(employeeSql, empCon);
                            empCmd.Parameters.AddWithValue("@user_id", registeredUserId);
                            await empCmd.ExecuteNonQueryAsync();
                        }
                    }

                    return registeredUserId;
                }

                // Fallback to original implementation (no DualWriteService)
                using var con = DbConnection.GetConnection();
                await con.OpenAsync();

                using var tx = await con.BeginTransactionAsync();

                string sql = @"INSERT INTO users (role_id, user_fname, user_mname, user_lname, user_brith_date, user_email, user_contact_number, user_password)
                               VALUES (@role_id,@fname,@mname,@lname,@birth,@email,@contact,@password); SELECT CAST(SCOPE_IDENTITY() AS int);";

                using var cmd = new SqlCommand(sql, con, (SqlTransaction)tx);
                cmd.Parameters.AddWithValue("@role_id", user.role_id);
                cmd.Parameters.AddWithValue("@fname", (object?)user.user_fname ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@mname", (object?)user.user_mname ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@lname", (object?)user.user_lname ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@birth", (object?)user.user_brith_date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@email", (object?)user.user_email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@contact", (object?)user.user_contact_number ?? DBNull.Value);
                // Hash password before storing
                var passwordToStore = string.IsNullOrWhiteSpace(user.user_password) 
                    ? DBNull.Value 
                    : (object)PasswordHasher.HashPassword(user.user_password);
                cmd.Parameters.AddWithValue("@password", passwordToStore);

                var idObj = await cmd.ExecuteScalarAsync();
                int newUserId = (idObj is int i) ? i : Convert.ToInt32(idObj);

                // Determine role name if not supplied
                string? roleName = user.roleName;
                if (string.IsNullOrWhiteSpace(roleName))
                {
                    using var roleCmd = new SqlCommand("SELECT role_name FROM Roles WHERE role_id=@rid", con, (SqlTransaction)tx);
                    roleCmd.Parameters.AddWithValue("@rid", user.role_id);
                    var rn = await roleCmd.ExecuteScalarAsync();
                    roleName = rn?.ToString();
                }

                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    var roleLower = roleName.ToLowerInvariant();
                    if (roleLower == "client")
                    {
                        // Insert into Clients(user_id)
                        string clientSql = "INSERT INTO Clients (user_id) VALUES (@user_id);";
                        using var clientCmd = new SqlCommand(clientSql, con, (SqlTransaction)tx);
                        clientCmd.Parameters.AddWithValue("@user_id", newUserId);
                        await clientCmd.ExecuteNonQueryAsync();
                    }
                    else if (roleLower == "staff" || roleLower == "admin")
                    {
                        // Insert into Employees(user_id)
                        string employeeSql = "INSERT INTO Employees (user_id) VALUES (@user_id);";
                        using var empCmd = new SqlCommand(employeeSql, con, (SqlTransaction)tx);
                        empCmd.Parameters.AddWithValue("@user_id", newUserId);
                        await empCmd.ExecuteNonQueryAsync();
                    }
                }

                await ((SqlTransaction)tx).CommitAsync();
                return newUserId;
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                System.Diagnostics.Debug.WriteLine($"Registration error: {ex}");
                throw;
            }
        }

        private async Task<bool> HasSyncStatusColumnAsync(SqlConnection con, SqlTransaction? tx, string tableName)
        {
            try
            {
                string sql = @"
                    SELECT COUNT(*) 
                    FROM sys.columns 
                    WHERE object_id = OBJECT_ID(@tableName) 
                    AND name = 'sync_status'";
                
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

        public async Task<bool> EmailExistsAsync(string email)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = "SELECT 1 FROM users WHERE user_email=@email";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@email", email);

            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        public async Task<int?> GetRoleIdByNameAsync(string roleName)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = "SELECT role_id FROM Roles WHERE LOWER(role_name)=LOWER(@name)";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@name", roleName);

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : Convert.ToInt32(result);
        }

        public async Task<List<User>> GetEmployeesAsync(int page, int pageSize, string? search = null, string? role = null)
        {
            var users = new List<User>();

            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            var sql = @"SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, R.role_name
                        FROM users U JOIN Roles R ON U.role_id=R.role_id";

            var whereParts = new List<string>();

            // IMPORTANT: Exclude clients from employee list
            whereParts.Add("LOWER(R.role_name) != 'client'");

            if (!string.IsNullOrWhiteSpace(search))
            {
                whereParts.Add("(U.user_fname LIKE @search OR U.user_lname LIKE @search OR U.user_email LIKE @search)");
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                whereParts.Add("LOWER(R.role_name)=LOWER(@role)");
            }

            if (whereParts.Count >0)
            {
                sql += " WHERE " + string.Join(" AND ", whereParts);
            }

            sql += " ORDER BY U.user_id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            using var cmd = new SqlCommand(sql, con);

            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", "%" + search + "%");
            if (!string.IsNullOrWhiteSpace(role)) cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@skip", (page -1) * pageSize);
            cmd.Parameters.AddWithValue("@take", pageSize);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }

            return users;
        }

        public async Task<int> GetEmployeesCountAsync(string? search = null, string? role = null)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            var sql = "SELECT COUNT(1) FROM users U JOIN Roles R ON U.role_id=R.role_id";

            var whereParts = new List<string>();

            // IMPORTANT: Exclude clients from employee count
            whereParts.Add("LOWER(R.role_name) != 'client'");

            if (!string.IsNullOrWhiteSpace(search)) whereParts.Add("(U.user_fname LIKE @search OR U.user_lname LIKE @search OR U.user_email LIKE @search)");
            if (!string.IsNullOrWhiteSpace(role)) whereParts.Add("LOWER(R.role_name)=LOWER(@role)");

            if (whereParts.Count >0) sql += " WHERE " + string.Join(" AND ", whereParts);

            using var cmd = new SqlCommand(sql, con);

            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", "%" + search + "%");
            if (!string.IsNullOrWhiteSpace(role)) cmd.Parameters.AddWithValue("@role", role);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<List<User>> GetClientsAsync(int page, int pageSize, string? search = null)
        {
            var users = new List<User>();

            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            var sql = @"SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, R.role_name
                        FROM users U JOIN Roles R ON U.role_id=R.role_id";

            var whereParts = new List<string>();

            // Only get clients
            whereParts.Add("LOWER(R.role_name) = 'client'");

            if (!string.IsNullOrWhiteSpace(search))
            {
                whereParts.Add("(U.user_fname LIKE @search OR U.user_lname LIKE @search OR U.user_email LIKE @search)");
            }

            if (whereParts.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", whereParts);
            }

            sql += " ORDER BY U.user_id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            using var cmd = new SqlCommand(sql, con);

            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", "%" + search + "%");
            cmd.Parameters.AddWithValue("@skip", (page - 1) * pageSize);
            cmd.Parameters.AddWithValue("@take", pageSize);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }

            return users;
        }

        public async Task<int> GetClientsCountAsync(string? search = null)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            var sql = "SELECT COUNT(1) FROM users U JOIN Roles R ON U.role_id=R.role_id";

            var whereParts = new List<string>();

            // Only get clients
            whereParts.Add("LOWER(R.role_name) = 'client'");

            if (!string.IsNullOrWhiteSpace(search)) whereParts.Add("(U.user_fname LIKE @search OR U.user_lname LIKE @search OR U.user_email LIKE @search)");

            if (whereParts.Count > 0) sql += " WHERE " + string.Join(" AND ", whereParts);

            using var cmd = new SqlCommand(sql, con);

            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", "%" + search + "%");

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateUserAsync(User user)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            // Build SQL dynamically to include password only if provided
            var updateFields = new List<string>
            {
                "user_fname = @fname",
                "user_mname = @mname",
                "user_lname = @lname",
                "user_brith_date = @birth",
                "user_email = @email",
                "user_contact_number = @contact"
            };

            if (!string.IsNullOrWhiteSpace(user.user_password))
            {
                updateFields.Add("user_password = @password");
            }

            string sql = $@"UPDATE users 
                          SET {string.Join(", ", updateFields)}
                          WHERE user_id = @user_id";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@user_id", user.userId);
            cmd.Parameters.AddWithValue("@fname", (object?)user.user_fname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mname", (object?)user.user_mname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lname", (object?)user.user_lname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@birth", (object?)user.user_brith_date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", (object?)user.user_email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@contact", (object?)user.user_contact_number ?? DBNull.Value);
            
            if (!string.IsNullOrWhiteSpace(user.user_password))
            {
                // Hash password before storing
                var hashedPassword = PasswordHasher.HashPassword(user.user_password);
                cmd.Parameters.AddWithValue("@password", hashedPassword);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            Console.WriteLine($"🔍 Querying user with ID: {userId}");

            string sql = @"
                SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, 
                U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, 
                R.role_name
                FROM users U
                JOIN Roles R ON U.role_id = R.role_id
                WHERE U.user_id = @id";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", userId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                Console.WriteLine("✅ User found!");
                try
                {
                    return MapUser(reader);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error mapping user: {ex.Message}");
                    return null;
                }
            }
            else
            {
                Console.WriteLine("❌ User NOT found!");
                return null;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            Console.WriteLine($"🔍 Querying user with email: {email}");

            string sql = @"
                SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, 
                U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, 
                R.role_name
                FROM users U
                JOIN Roles R ON U.role_id = R.role_id
                WHERE U.user_email = @email";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@email", email);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                Console.WriteLine("✅ User found!");
                try
                {
                    return MapUser(reader);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error mapping user: {ex.Message}");
                    return null;
                }
            }
            else
            {
                Console.WriteLine("❌ User NOT found!");
                return null;
            }
        }

        private static User MapUser(SqlDataReader reader)
        {
            string? ReadString(string name)
            {
                var ordinal = reader.GetOrdinal(name);
                if (reader.IsDBNull(ordinal)) return null;
                var obj = reader.GetValue(ordinal);
                if (obj is string s) return s;
                if (obj is byte[] b) return Encoding.UTF8.GetString(b);
                return obj?.ToString();
            }

            DateTime? ReadDate(string name)
            {
                var ordinal = reader.GetOrdinal(name);
                if (reader.IsDBNull(ordinal)) return null;
                var obj = reader.GetValue(ordinal);
                if (obj is DateTime dt) return dt;
                if (DateTime.TryParse(obj?.ToString(), out var parsed)) return parsed;
                return null;
            }

            return new User
            {
                userId = reader.GetInt32(reader.GetOrdinal("user_id")),
                role_id = reader.GetInt32(reader.GetOrdinal("role_id")),
                user_fname = ReadString("user_fname"),
                user_mname = ReadString("user_mname"),
                user_lname = ReadString("user_lname"),
                user_brith_date = ReadDate("user_brith_date"), // NOTE: match DB column name
                user_email = ReadString("user_email"),
                user_contact_number = ReadString("user_contact_number"),
                user_password = ReadString("user_password"),
                roleName = ReadString("role_name")?.ToLowerInvariant()
            };
        }
    }
}
