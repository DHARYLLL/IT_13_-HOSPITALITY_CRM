using System.Data;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using Hospitality.Database;

namespace Hospitality.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string? connectionString = null)
        {
            _connectionString = connectionString ?? DbConnection.Default;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            using var conn = CreateConnection();
            using var cmd = new SqlCommand(sql, conn);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
                }
            }
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<T?> ExecuteScalarAsync<T>(string sql, Dictionary<string, object>? parameters = null)
        {
            using var conn = CreateConnection();
            using var cmd = new SqlCommand(sql, conn);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
                }
            }
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return default;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        public async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            var list = new List<T>();
            using var conn = CreateConnection();
            using var cmd = new SqlCommand(sql, conn);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
                }
            }
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(map(reader));
            }
            return list;
        }

        public async Task<bool> TestAsync()
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
