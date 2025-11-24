using Hospitality.Database;
using Hospitality.Models;
using Microsoft.Data.SqlClient;

namespace Hospitality.Services
{
    public class UserService
    {
        public async Task<User?> LoginAsync(string email, string password)
        {
            User? user = null;

            using (SqlConnection con = DbConnection.GetConnection())
            {
                await con.OpenAsync();

                string query = @"SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, R.role_name
                                 FROM users U JOIN Roles R ON U.role_id = R.role_id
                                 WHERE U.user_email = @email AND U.user_password = @password";

                using var cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@password", password); // NOTE: hash before storing/compare in production

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    user = MapUser(reader);
                }
            }

            return user;
        }

        public async Task<int> RegisterAsync(User user)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"INSERT INTO users (role_id, user_fname, user_mname, user_lname, user_brith_date, user_email, user_contact_number, user_password)
                           VALUES (@role_id,@fname,@mname,@lname,@birth,@email,@contact,@password); SELECT SCOPE_IDENTITY();";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@role_id", user.role_id);
            cmd.Parameters.AddWithValue("@fname", (object?)user.user_fname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@mname", (object?)user.user_mname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lname", (object?)user.user_lname ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@birth", (object?)user.user_brith_date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", user.user_email);
            cmd.Parameters.AddWithValue("@contact", (object?)user.user_contact_number ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@password", user.user_password); // hash in real app

            var idObj = await cmd.ExecuteScalarAsync();
            int newUserId = Convert.ToInt32(idObj);

            // Insert into clients table if client role
            if (string.Equals(user.roleName, "client", StringComparison.OrdinalIgnoreCase))
            {
                string clientSql = "INSERT INTO clients (user_id) VALUES (@user_id);";
                using var clientCmd = new SqlCommand(clientSql, con);
                clientCmd.Parameters.AddWithValue("@user_id", newUserId);
                await clientCmd.ExecuteNonQueryAsync();
            }

            return newUserId;
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

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, R.role_name
                           FROM users U JOIN Roles R ON U.role_id=R.role_id WHERE U.user_id=@id";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", userId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync()) return MapUser(reader);

            return null;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            using var con = DbConnection.GetConnection();
            await con.OpenAsync();
            string sql = @"SELECT U.user_id, U.role_id, U.user_fname, U.user_mname, U.user_lname, U.user_brith_date, U.user_email, U.user_contact_number, U.user_password, R.role_name
 FROM users U JOIN Roles R ON U.role_id=R.role_id WHERE U.user_email=@email";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@email", email);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return MapUser(reader);
            return null;
        }

        private static User MapUser(SqlDataReader reader) => new()
        {
            userId = reader.GetInt32(reader.GetOrdinal("user_id")),
            role_id = reader.GetInt32(reader.GetOrdinal("role_id")),
            user_fname = reader["user_fname"] as string,
            user_mname = reader["user_mname"] as byte[],
            user_lname = reader["user_lname"] as byte[],
            user_brith_date = reader["user_brith_date"] as DateTime?,
            user_email = reader["user_email"] as string,
            user_contact_number = reader["user_contact_number"] as string,
            user_password = reader["user_password"] as string,
            roleName = reader["role_name"].ToString()?.ToLowerInvariant()
        };
    }
}
