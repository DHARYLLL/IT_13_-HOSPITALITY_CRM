using Hospitality.Database;
using Microsoft.Data.SqlClient;

namespace Hospitality.Services;

public class AnalyticsService
{
    // ===== SALES ANALYTICS =====

    public async Task<SalesAnalytics> GetSalesAnalyticsAsync(int days = 30)
    {
        var analytics = new SalesAnalytics();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        DateTime startDate = DateTime.Today.AddDays(-days);

        // Revenue by room type
        string roomTypeSql = @"
            SELECT r.room_name, 
  COUNT(DISTINCT b.booking_id) as bookings,
      SUM(r.room_price * DATEDIFF(day, b.[check-in_date], b.[check-out_date])) as revenue
  FROM Bookings b
            INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
         INNER JOIN rooms r ON br.room_id = r.room_id
       WHERE b.[check-in_date] >= @startDate
         GROUP BY r.room_name
            ORDER BY revenue DESC";

        using var roomCmd = new SqlCommand(roomTypeSql, con);
        roomCmd.Parameters.AddWithValue("@startDate", startDate);
        using var roomReader = await roomCmd.ExecuteReaderAsync();

        while (await roomReader.ReadAsync())
        {
            analytics.RevenueByRoomType.Add(new RoomTypeRevenue
            {
                RoomType = roomReader.GetString(0),
                Bookings = roomReader.GetInt32(1),
                Revenue = roomReader.IsDBNull(2) ? 0 : roomReader.GetDecimal(2)
            });
        }
        await roomReader.CloseAsync();

        // Monthly revenue trend
        string monthlySql = @"
      SELECT DATEPART(YEAR, b.[check-in_date]) as year,
     DATEPART(MONTH, b.[check-in_date]) as month,
         SUM(r.room_price * DATEDIFF(day, b.[check-in_date], b.[check-out_date])) as revenue,
            COUNT(DISTINCT b.booking_id) as bookings
     FROM Bookings b
            INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
  INNER JOIN rooms r ON br.room_id = r.room_id
            WHERE b.[check-in_date] >= DATEADD(MONTH, -12, GETDATE())
  GROUP BY DATEPART(YEAR, b.[check-in_date]), DATEPART(MONTH, b.[check-in_date])
    ORDER BY year, month";

        using var monthlyCmd = new SqlCommand(monthlySql, con);
        using var monthlyReader = await monthlyCmd.ExecuteReaderAsync();

        while (await monthlyReader.ReadAsync())
        {
            analytics.MonthlyTrend.Add(new MonthlyRevenue
            {
                Year = monthlyReader.GetInt32(0),
                Month = monthlyReader.GetInt32(1),
                Revenue = monthlyReader.IsDBNull(2) ? 0 : monthlyReader.GetDecimal(2),
                Bookings = monthlyReader.GetInt32(3)
            });
        }
        await monthlyReader.CloseAsync();

        // Booking lead time (days between booking creation and check-in)
        string leadTimeSql = @"
            SELECT AVG(DATEDIFF(day, b.[check-in_date], GETDATE())) as avg_lead_time
        FROM Bookings b
            WHERE b.[check-in_date] >= @startDate";

        using var leadCmd = new SqlCommand(leadTimeSql, con);
        leadCmd.Parameters.AddWithValue("@startDate", startDate);
        var leadResult = await leadCmd.ExecuteScalarAsync();
        analytics.AverageLeadTime = leadResult != DBNull.Value ? Math.Abs(Convert.ToInt32(leadResult)) : 0;

        // Top performing days of week
        string dayOfWeekSql = @"
            SELECT DATENAME(WEEKDAY, b.[check-in_date]) as day_name,
  COUNT(DISTINCT b.booking_id) as bookings,
         SUM(r.room_price * DATEDIFF(day, b.[check-in_date], b.[check-out_date])) as revenue
FROM Bookings b
    INNER JOIN Booking_rooms br ON b.booking_id = br.booking_id
            INNER JOIN rooms r ON br.room_id = r.room_id
        WHERE b.[check-in_date] >= @startDate
 GROUP BY DATENAME(WEEKDAY, b.[check-in_date]), DATEPART(WEEKDAY, b.[check-in_date])
 ORDER BY DATEPART(WEEKDAY, b.[check-in_date])";

        using var dayCmd = new SqlCommand(dayOfWeekSql, con);
        dayCmd.Parameters.AddWithValue("@startDate", startDate);
        using var dayReader = await dayCmd.ExecuteReaderAsync();

        while (await dayReader.ReadAsync())
        {
            analytics.RevenueByDayOfWeek.Add(new DayOfWeekRevenue
            {
                DayName = dayReader.GetString(0),
                Bookings = dayReader.GetInt32(1),
                Revenue = dayReader.IsDBNull(2) ? 0 : dayReader.GetDecimal(2)
            });
        }

        return analytics;
    }

    // ===== MARKETING ANALYTICS =====

    public async Task<MarketingAnalytics> GetMarketingAnalyticsAsync()
    {
        var analytics = new MarketingAnalytics();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Customer segmentation by loyalty tier
        string tierSql = @"
            SELECT lp.current_tier, 
         COUNT(*) as member_count,
       AVG(CAST(lp.total_points AS FLOAT)) as avg_points,
       AVG(lp.lifetime_spend) as avg_spend,
           AVG(CAST(lp.lifetime_stays AS FLOAT)) as avg_stays
FROM LoyaltyPrograms lp
       GROUP BY lp.current_tier
        ORDER BY 
         CASE lp.current_tier 
           WHEN 'Platinum' THEN 1 
      WHEN 'Gold' THEN 2 
        WHEN 'Silver' THEN 3 
        ELSE 4 
         END";

        try
        {
            using var tierCmd = new SqlCommand(tierSql, con);
            using var tierReader = await tierCmd.ExecuteReaderAsync();

            while (await tierReader.ReadAsync())
            {
                analytics.CustomerSegments.Add(new CustomerSegment
                {
                    Tier = tierReader.GetString(0),
                    MemberCount = tierReader.GetInt32(1),
                    AvgPoints = tierReader.IsDBNull(2) ? 0 : (int)tierReader.GetDouble(2),
                    AvgSpend = tierReader.IsDBNull(3) ? 0 : tierReader.GetDecimal(3),
                    AvgStays = tierReader.IsDBNull(4) ? 0 : (int)tierReader.GetDouble(4)
                });
            }
            await tierReader.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ LoyaltyPrograms table may not exist: {ex.Message}");
        }

        // Reward redemption analytics
        // Reward redemption analyticsemo
        string rewardSql = @"
    SELECT lr.reward_name, lr.reward_type,
    COUNT(lt.transaction_id) as redemptions,
       SUM(lt.points_redeemed) as total_points_used
       FROM LoyaltyTransactions lt
            INNER JOIN LoyaltyPrograms lp ON lt.loyalty_id = lp.loyalty_id
    INNER JOIN LoyaltyRewards lr ON lt.description LIKE '%' + lr.reward_name + '%'
        WHERE lt.transaction_type = 'redeem'
            GROUP BY lr.reward_name, lr.reward_type
  ORDER BY redemptions DESC";

        try
        {
            using var rewardCmd = new SqlCommand(rewardSql, con);
            using var rewardReader = await rewardCmd.ExecuteReaderAsync();

            while (await rewardReader.ReadAsync())
            {
                analytics.RewardPerformance.Add(new RewardAnalytics
                {
                    RewardName = rewardReader.GetString(0),
                    RewardType = rewardReader.GetString(1),
                    Redemptions = rewardReader.GetInt32(2),
                    TotalPointsUsed = rewardReader.GetInt32(3)
                });
            }
            await rewardReader.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ LoyaltyRewards/Transactions tables may not exist: {ex.Message}");
        }

        // Customer retention - repeat bookings
        string retentionSql = @"
  SELECT 
    COUNT(DISTINCT CASE WHEN booking_count = 1 THEN client_id END) as one_time,
          COUNT(DISTINCT CASE WHEN booking_count BETWEEN 2 AND 3 THEN client_id END) as repeat_2_3,
      COUNT(DISTINCT CASE WHEN booking_count >= 4 THEN client_id END) as loyal_4_plus
            FROM (
     SELECT client_id, COUNT(*) as booking_count
 FROM Bookings
            GROUP BY client_id
            ) AS customer_bookings";

        using var retentionCmd = new SqlCommand(retentionSql, con);
        using var retentionReader = await retentionCmd.ExecuteReaderAsync();

        if (await retentionReader.ReadAsync())
        {
            analytics.OneTimeCustomers = retentionReader.IsDBNull(0) ? 0 : retentionReader.GetInt32(0);
            analytics.RepeatCustomers = retentionReader.IsDBNull(1) ? 0 : retentionReader.GetInt32(1);
            analytics.LoyalCustomers = retentionReader.IsDBNull(2) ? 0 : retentionReader.GetInt32(2);
        }
        await retentionReader.CloseAsync();

        // New customers this month
        string newCustomersSql = @"
   SELECT COUNT(DISTINCT c.client_id)
      FROM clients c
            INNER JOIN users u ON c.user_id = u.user_id
            WHERE MONTH(u.user_brith_date) = MONTH(GETDATE()) 
            OR c.client_id IN (
        SELECT DISTINCT b.client_id 
           FROM Bookings b 
     WHERE b.[check-in_date] >= DATEADD(MONTH, -1, GETDATE())
         GROUP BY b.client_id
             HAVING COUNT(*) = 1
            )";

        try
        {
            using var newCmd = new SqlCommand(newCustomersSql, con);
            var newResult = await newCmd.ExecuteScalarAsync();
            analytics.NewCustomersThisMonth = newResult != DBNull.Value ? Convert.ToInt32(newResult) : 0;
        }
        catch
        {
            analytics.NewCustomersThisMonth = 0;
        }

        return analytics;
    }

    // ===== CONVERSION ANALYTICS =====

    public async Task<ConversionAnalytics> GetConversionAnalyticsAsync(int days = 30)
    {
        var analytics = new ConversionAnalytics();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        DateTime startDate = DateTime.Today.AddDays(-days);

        // Booking status breakdown
        string statusSql = @"
        SELECT 
        ISNULL(booking_status, 'confirmed') as status,
        COUNT(*) as count
        FROM Bookings
        WHERE [check-in_date] >= @startDate
        GROUP BY booking_status";

        using var statusCmd = new SqlCommand(statusSql, con);
        statusCmd.Parameters.AddWithValue("@startDate", startDate);
        using var statusReader = await statusCmd.ExecuteReaderAsync();

        while (await statusReader.ReadAsync())
        {
            var status = statusReader.GetString(0);
            var count = statusReader.GetInt32(1);

            switch (status.ToLower())
            {
                case "completed": analytics.CompletedBookings = count; break;
                case "cancelled": analytics.CancelledBookings = count; break;
                case "confirmed": analytics.ConfirmedBookings = count; break;
                case "checked-in": analytics.CheckedInBookings = count; break;
            }
        }

        analytics.TotalBookings = analytics.CompletedBookings + analytics.CancelledBookings +
       analytics.ConfirmedBookings + analytics.CheckedInBookings;

        if (analytics.TotalBookings > 0)
        {
            analytics.CancellationRate = (decimal)analytics.CancelledBookings / analytics.TotalBookings * 100;
            analytics.CompletionRate = (decimal)analytics.CompletedBookings / analytics.TotalBookings * 100;
        }

        return analytics;
    }

    // ===== GUEST DEMOGRAPHICS =====

    public async Task<GuestDemographics> GetGuestDemographicsAsync()
    {
        var demographics = new GuestDemographics();
        using var con = DbConnection.GetConnection();
        await con.OpenAsync();

        // Guest count by booking frequency
        string frequencySql = @"
        SELECT 
        CASE 
        WHEN booking_count = 1 THEN 'First-time'
        WHEN booking_count BETWEEN 2 AND 5 THEN 'Regular'
        WHEN booking_count > 5 THEN 'VIP'
        END as category,
        COUNT(*) as guest_count
        FROM (
        SELECT client_id, COUNT(*) as booking_count
        FROM Bookings
        GROUP BY client_id
        ) AS guests
        GROUP BY 
        CASE 
        WHEN booking_count = 1 THEN 'First-time'
        WHEN booking_count BETWEEN 2 AND 5 THEN 'Regular'
        WHEN booking_count > 5 THEN 'VIP'
        END";

        using var freqCmd = new SqlCommand(frequencySql, con);
        using var freqReader = await freqCmd.ExecuteReaderAsync();

        while (await freqReader.ReadAsync())
        {
            demographics.GuestCategories.Add(new GuestCategory
            {
                Category = freqReader.GetString(0),
                Count = freqReader.GetInt32(1)
            });
        }
        await freqReader.CloseAsync();

        // Average party size
        string partySizeSql = @"
        SELECT AVG(CAST(person_count AS FLOAT)) as avg_party_size
        FROM Bookings
        WHERE [check-in_date] >= DATEADD(MONTH, -3, GETDATE())";

        using var partyCmd = new SqlCommand(partySizeSql, con);
        var partyResult = await partyCmd.ExecuteScalarAsync();
        demographics.AveragePartySize = partyResult != DBNull.Value ? Math.Round(Convert.ToDecimal(partyResult), 1) : 0;

        return demographics;
    }
}

// ===== ANALYTICS MODELS =====

public class SalesAnalytics
{
    public List<RoomTypeRevenue> RevenueByRoomType { get; set; } = new();
    public List<MonthlyRevenue> MonthlyTrend { get; set; } = new();
    public List<DayOfWeekRevenue> RevenueByDayOfWeek { get; set; } = new();
    public int AverageLeadTime { get; set; }
}

public class RoomTypeRevenue
{
    public string RoomType { get; set; } = "";
    public int Bookings { get; set; }
    public decimal Revenue { get; set; }
}

public class MonthlyRevenue
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
    public int Bookings { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy");
}

public class DayOfWeekRevenue
{
    public string DayName { get; set; } = "";
    public int Bookings { get; set; }
    public decimal Revenue { get; set; }
}

public class MarketingAnalytics
{
    public List<CustomerSegment> CustomerSegments { get; set; } = new();
    public List<RewardAnalytics> RewardPerformance { get; set; } = new();
    public int OneTimeCustomers { get; set; }
    public int RepeatCustomers { get; set; }
    public int LoyalCustomers { get; set; }
    public int NewCustomersThisMonth { get; set; }
    public decimal RetentionRate => (RepeatCustomers + LoyalCustomers) > 0 &&
        (OneTimeCustomers + RepeatCustomers + LoyalCustomers) > 0
        ? (decimal)(RepeatCustomers + LoyalCustomers) / (OneTimeCustomers + RepeatCustomers + LoyalCustomers) * 100
        : 0;
}

public class CustomerSegment
{
    public string Tier { get; set; } = "";
    public int MemberCount { get; set; }
    public int AvgPoints { get; set; }
    public decimal AvgSpend { get; set; }
    public int AvgStays { get; set; }
}

public class RewardAnalytics
{
    public string RewardName { get; set; } = "";
    public string RewardType { get; set; } = "";
    public int Redemptions { get; set; }
    public int TotalPointsUsed { get; set; }
}

public class ConversionAnalytics
{
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CheckedInBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal CancellationRate { get; set; }
    public decimal CompletionRate { get; set; }
}

public class GuestDemographics
{
    public List<GuestCategory> GuestCategories { get; set; } = new();
    public decimal AveragePartySize { get; set; }
}

public class GuestCategory
{
    public string Category { get; set; } = "";
    public int Count { get; set; }
}
