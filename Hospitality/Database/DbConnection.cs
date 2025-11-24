using Microsoft.Data.SqlClient;

namespace Hospitality.Database
{
    public static class DbConnection
    {
        // DEV ONLY - Do not ship production credentials in the client.
        public const string Default = "Data Source=LAPTOP-UE341BKJ\\SQLEXPRESS;Initial Catalog=CRM;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False";

        internal static SqlConnection GetConnection()
        {
            // Returns a new SQL connection using the development connection string.
            return new SqlConnection(Default);
        }
    }
}
