# Loyalty System Testing Guide

## Quick Start Testing

### 1. Database Setup
Run the SQL script to create loyalty tables:
```sql
-- Execute in SQL Server Management Studio or Azure Data Studio
-- File: Hospitality/Database/LoyaltyProgramSetup.sql
```

This will:
- ? Add `booking_status` column to Bookings table (if not exists)
- ? Create LoyaltyPrograms table
- ? Create LoyaltyRewards table
- ? Create LoyaltyTransactions table
- ? Insert 8 sample rewards
- ? Create indexes for performance

### 2. Test User Setup

#### Create a Test Client
```sql
-- Create a test user if not exists
IF NOT EXISTS (SELECT * FROM users WHERE user_email = 'testclient@innsight.com')
BEGIN
    INSERT INTO users (role_id, user_fname, user_lname, user_email, user_password)
    VALUES (3, 'Test', 'Client', 'testclient@innsight.com', 'password123');
    
    DECLARE @userId INT = SCOPE_IDENTITY();
    
    -- Create client record
    INSERT INTO Clients (user_id)
    VALUES (@userId);
    
 PRINT 'Test client created successfully';
END
```

### 3. Test Scenarios

#### Scenario A: New Member Journey
1. **Login as test client**
   - Email: testclient@innsight.com
   - Password: password123

2. **Check loyalty dashboard**
   - Navigate to `/client/loyalty/{clientId}`
   - Should show Bronze tier with 0 points
   - View available rewards (all locked)

3. **Make a booking**
   - Go to "Book a Room"
   - Select dates and room
   - Complete booking

4. **Simulate booking completion**
   ```sql
   -- Get the booking ID from your booking
   DECLARE @bookingId INT = 1; -- Replace with actual booking ID
   
   -- Complete the booking (this awards points automatically)
   UPDATE Bookings SET booking_status = 'completed' WHERE booking_id = @bookingId;
   
   -- Or use the service method (recommended):
   -- await BookingService.CompleteBookingAsync(bookingId);
   ```

5. **Check loyalty points**
   - Refresh loyalty dashboard
   - Points should now be visible
   - Transaction history shows earned points

#### Scenario B: Tier Progression Test
```sql
-- Give test client Gold tier status for testing
UPDATE LoyaltyPrograms 
SET total_points = 10000,
    current_tier = 'Gold',
    lifetime_stays = 8,
    lifetime_spend = 5000.00,
    member_since = DATEADD(YEAR, -2, GETDATE())
WHERE client_id = (SELECT client_id FROM Clients WHERE user_id = (SELECT userId FROM users WHERE user_email = 'testclient@innsight.com'));
```

Expected results:
- ? Tier badge shows "?? Gold"
- ? Progress bar shows path to Platinum
- ? 10,000 points displayed
- ? Multiple rewards unlocked
- ? Can redeem available rewards

#### Scenario C: Reward Redemption
1. Ensure client has enough points (use SQL above)
2. Navigate to loyalty page
3. Find a reward (e.g., "Late Checkout Pass" - 1,500 points)
4. Click "Redeem" button
5. **Expected:**
   - Points deducted from balance
   - Transaction appears in activity history
   - Success message shown

#### Scenario D: Multi-Booking Point Accumulation
```sql
-- Create multiple completed bookings for testing
DECLARE @clientId INT = (SELECT client_id FROM Clients WHERE user_id = (SELECT userId FROM users WHERE user_email = 'testclient@innsight.com'));

-- Booking 1 - $200 stay
INSERT INTO Bookings (client_id, [check-in_date], [check-out_date], person_count, booking_status)
VALUES (@clientId, DATEADD(DAY, -30, GETDATE()), DATEADD(DAY, -28, GETDATE()), 2, 'completed');

DECLARE @booking1 INT = SCOPE_IDENTITY();

-- Add room to booking
INSERT INTO Booking_rooms (booking_id, room_id)
VALUES (@booking1, 1); -- Assuming room_id 1 exists

-- Booking 2 - $300 stay
INSERT INTO Bookings (client_id, [check-in_date], [check-out_date], person_count, booking_status)
VALUES (@clientId, DATEADD(DAY, -15, GETDATE()), DATEADD(DAY, -13, GETDATE()), 2, 'completed');

DECLARE @booking2 INT = SCOPE_IDENTITY();

INSERT INTO Booking_rooms (booking_id, room_id)
VALUES (@booking2, 2);

-- Now manually add points for these bookings (or use the service)
-- Bronze tier: 10 points per $1
-- Booking 1: 200 * 2 nights * 10 = 4000 points
-- Booking 2: 300 * 2 nights * 10 = 6000 points
```

### 4. Visual Testing Checklist

#### Loyalty Dashboard (`/client/loyalty/{clientId}`)
- ? Hero section displays correctly
- ? Points circle animates properly
- ? Tier badge shows correct icon and color
- ? Progress bar shows accurate percentage
- ? Benefits list displays current tier perks
- ? Next tier preview shows correctly
- ? Statistics show accurate numbers
- ? Rewards grid displays with proper status
- ? Activity log shows transactions
- ? Tier comparison table renders
- ? Responsive design works on mobile

#### Client Profile Integration
- ? Loyalty card shows on dashboard
- ? Points display in navigation bar
- ? Link to loyalty page works
- ? Member since date is accurate

### 5. Edge Cases to Test

#### No Loyalty Record
```sql
-- Delete loyalty record to test auto-creation
DELETE FROM LoyaltyPrograms WHERE client_id = @yourClientId;
```
- Visit loyalty page
- Should automatically create Bronze tier with 0 points

#### Insufficient Points for Reward
- Try to redeem reward with more points than available
- Button should be disabled
- Shows "Need X more" message

#### Maximum Tier (Platinum)
```sql
UPDATE LoyaltyPrograms 
SET total_points = 50000, 
  current_tier = 'Platinum'
WHERE client_id = @yourClientId;
```
- Progress section should show "You've reached the highest tier!"
- No next tier preview

#### Empty Transaction History
```sql
DELETE FROM LoyaltyTransactions WHERE loyalty_id IN (
    SELECT loyalty_id FROM LoyaltyPrograms WHERE client_id = @yourClientId
);
```
- Should show "No recent activity" message

### 6. Performance Testing

#### Check Query Performance
```sql
-- Test loyalty program retrieval
SET STATISTICS TIME ON;
SELECT * FROM LoyaltyPrograms WHERE client_id = 1;
SET STATISTICS TIME OFF;

-- Test transaction history
SET STATISTICS TIME ON;
SELECT TOP 10 * FROM LoyaltyTransactions WHERE loyalty_id = 1 ORDER BY transaction_date DESC;
SET STATISTICS TIME OFF;
```

#### Load Test Multiple Clients
```sql
-- Create 100 test loyalty programs
DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    IF NOT EXISTS (SELECT * FROM LoyaltyPrograms WHERE client_id = @i)
    BEGIN
  INSERT INTO LoyaltyPrograms (client_id, total_points, current_tier, member_since, lifetime_stays, lifetime_spend)
        VALUES (@i, FLOOR(RAND() * 20000), 
         CASE 
       WHEN FLOOR(RAND() * 20000) >= 15000 THEN 'Platinum'
             WHEN FLOOR(RAND() * 20000) >= 7000 THEN 'Gold'
               WHEN FLOOR(RAND() * 20000) >= 2500 THEN 'Silver'
        ELSE 'Bronze'
     END,
       DATEADD(YEAR, -FLOOR(RAND() * 5), GETDATE()),
         FLOOR(RAND() * 20),
                RAND() * 10000);
    END
    SET @i = @i + 1;
END
```

### 7. Common Issues & Solutions

#### Issue: Points not appearing after booking
**Solution:**
1. Check booking status is 'completed'
2. Verify client_id exists in Clients table
3. Check LoyaltyPrograms record exists
4. Review LoyaltyTransactions for errors

```sql
-- Diagnostic query
SELECT b.booking_id, b.booking_status, c.client_id, lp.loyalty_id, lp.total_points
FROM Bookings b
INNER JOIN Clients c ON b.client_id = c.client_id
LEFT JOIN LoyaltyPrograms lp ON c.client_id = lp.client_id
WHERE b.booking_id = @yourBookingId;
```

#### Issue: Tier not updating
**Solution:**
```csharp
// Manually trigger tier check in code
var loyalty = await LoyaltyService.GetLoyaltyProgramAsync(clientId);
var newTier = LoyaltyTier.GetTierByPoints(loyalty.total_points);

if (newTier.Name != loyalty.current_tier)
{
    // Update in database
    using var con = DbConnection.GetConnection();
    await con.OpenAsync();
    
    string sql = "UPDATE LoyaltyPrograms SET current_tier = @tier WHERE loyalty_id = @loyaltyId";
    using var cmd = new SqlCommand(sql, con);
    cmd.Parameters.AddWithValue("@tier", newTier.Name);
    cmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
    await cmd.ExecuteNonQueryAsync();
}
```

#### Issue: Rewards not loading
**Solution:**
```sql
-- Check rewards table
SELECT * FROM LoyaltyRewards WHERE is_active = 1;

-- Re-insert sample rewards if missing
-- (Run relevant section of LoyaltyProgramSetup.sql)
```

### 8. Test Data Cleanup

```sql
-- Clean up test data
DELETE FROM LoyaltyTransactions WHERE loyalty_id IN (
    SELECT loyalty_id FROM LoyaltyPrograms WHERE client_id = @testClientId
);

DELETE FROM LoyaltyPrograms WHERE client_id = @testClientId;

-- Or reset to default state
UPDATE LoyaltyPrograms 
SET total_points = 0,
    current_tier = 'Bronze',
    lifetime_stays = 0,
    lifetime_spend = 0
WHERE client_id = @testClientId;
```

### 9. Automated Testing Script

```csharp
// Example integration test
[Test]
public async Task LoyaltyProgram_PointsAwardedOnBookingCompletion()
{
    // Arrange
    var clientId = 1;
    var bookingId = 1;
    var bookingAmount = 500.00m;
    
    var loyaltyService = new LoyaltyService();
    var initialLoyalty = await loyaltyService.GetLoyaltyProgramAsync(clientId);
    var initialPoints = initialLoyalty?.total_points ?? 0;
    
    // Act
    await loyaltyService.AddPointsForBookingAsync(clientId, bookingId, bookingAmount);
    
    // Assert
    var updatedLoyalty = await loyaltyService.GetLoyaltyProgramAsync(clientId);
    Assert.IsTrue(updatedLoyalty.total_points > initialPoints);
Assert.AreEqual(initialPoints + 5000, updatedLoyalty.total_points); // Bronze: 10 points per $1
}
```

### 10. Success Criteria

? All database tables created without errors
? Client can view loyalty dashboard  
? Points are automatically awarded on booking completion  
? Tier progression works correctly  
? Rewards can be redeemed  
? Transaction history displays
? Statistics are accurate  
? Mobile responsive design works  
? Navigation links function properly  
? No console errors  
? Performance is acceptable (<500ms page load)

---

**Next Steps After Testing:**
1. Monitor real user behavior
2. Adjust tier thresholds based on data
3. Add seasonal/promotional rewards
4. Implement email notifications for tier upgrades
5. Create admin dashboard for loyalty management

