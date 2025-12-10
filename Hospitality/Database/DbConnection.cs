using Microsoft.Data.SqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hospitality.Database
{
    public static class DbConnection
    {
        // Local SQL Server (works offline)
        public const string Local = "Data Source=LAPTOP-UE341BKJ\\SQLEXPRESS;Initial Catalog=CRM;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False";

        // Online MonsterASP database (requires internet)
        public const string Online = "Data Source=db32979.public.databaseasp.net;Initial Catalog=db32979;User ID=db32979;Password=8c=Ha?Z9!G3z;Connect Timeout=15;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False";

        // Default points to local for offline-first approach
        public const string Default = Local;

        /// <summary>
        /// Gets a connection to the local database (always available)
        /// </summary>
        internal static SqlConnection GetConnection()
        {
            return new SqlConnection(Local);
        }

        /// <summary>
        /// Gets a connection to the local database
        /// </summary>
        public static SqlConnection GetLocalConnection()
        {
            return new SqlConnection(Local);
        }

        /// <summary>
        /// Gets a connection to the online database
        /// </summary>
        public static SqlConnection GetOnlineConnection()
        {
            return new SqlConnection(Online);
        }

        /// <summary>
        /// Tests if online database is reachable
        /// </summary>
        public static async Task<bool> CanConnectToOnlineAsync()
        {
            try
            {
                using var con = new SqlConnection(Online);
                // Use a cancellation token to timeout faster
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await con.OpenAsync(cts.Token);
                Console.WriteLine("✅ Successfully connected to online database");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Cannot connect to online database: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tests if local database is reachable
        /// </summary>
        public static async Task<bool> CanConnectToLocalAsync()
        {
            try
            {
                using var con = new SqlConnection(Local);
                await con.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
