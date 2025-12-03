# ? Quick Reference Guide - Loyalty Program

## ?? 5-Minute Setup

### Step 1: Run SQL Script (2 min)
```sql
-- Open: Hospitality/Database/LoyaltyProgramSetup.sql
-- Execute in SQL Server Management Studio
-- ? Creates 3 tables + 8 sample rewards
```

### Step 2: Build Project (1 min)
```bash
dotnet build
# ? Should compile without errors
```

### Step 3: Access System (2 min)
```
1. Login as client
2. Navigate to /client/loyalty/{clientId}
3. ? See your loyalty dashboard!
```

---

## ?? Key URLs

| Page | URL | Purpose |
|------|-----|---------|
| Loyalty Dashboard | `/client/loyalty/{clientId}` | Main loyalty page |
| Client Profile | `/client/profile/{clientId}` | User dashboard |
| New Booking | `/booking/new/{clientId}` | Create booking |
| Room Selection | `/booking/rooms/{clientId}` | Choose rooms |

---

## ?? Tier System Quick Reference

| Tier | Icon | Points | Earn Rate | Key Benefit |
|------|------|--------|-----------|-------------|
| Bronze | ?? | 0-2,499 | 10 pts/$1 | Member rates |
| Silver | ?? | 2,500-6,999 | 12 pts/$1 | Priority check-in |
| Gold | ?? | 7,000-14,999 | 15 pts/$1 | Late checkout |
| Platinum | ?? | 15,000+ | 20 pts/$1 | Breakfast included |

---

## ?? Common Code Snippets

### Award Points
```csharp
await LoyaltyService.AddPointsForBookingAsync(clientId, bookingId, amount);
```

### Get Loyalty Data
```csharp
var loyalty = await LoyaltyService.GetLoyaltyProgramAsync(clientId);
```

### Redeem Reward
```csharp
bool success = await LoyaltyService.RedeemPointsAsync(clientId, rewardId);
```

### Complete Booking (Auto-Awards Points)
```csharp
await BookingService.CompleteBookingAsync(bookingId);
```

---

## ??? Database Tables

### LoyaltyPrograms
- `loyalty_id` (PK)
- `client_id` (FK)
- `total_points`
- `current_tier`
- `member_since`
- `lifetime_stays`
- `lifetime_spend`

### LoyaltyRewards
- `reward_id` (PK)
- `reward_name`
- `points_required`
- `reward_type`
- `is_active`

### LoyaltyTransactions
- `transaction_id` (PK)
- `loyalty_id` (FK)
- `points_earned`
- `points_redeemed`
- `transaction_type`
- `booking_id` (FK)

---

## ?? Quick Test Script

```sql
-- Give yourself 10,000 Gold tier points
UPDATE LoyaltyPrograms 
SET total_points = 10000,
    current_tier = 'Gold',
    lifetime_stays = 5,
lifetime_spend = 2500.00
WHERE client_id = YOUR_CLIENT_ID;

-- Check it worked
SELECT * FROM LoyaltyPrograms WHERE client_id = YOUR_CLIENT_ID;

-- View available rewards
SELECT reward_name, points_required FROM LoyaltyRewards WHERE is_active = 1;
```

---

## ?? Key Features Checklist

- ? 4-tier membership system
- ? Automatic point accrual
- ? Tier progression tracking
- ? Reward catalog (8 pre-loaded)
- ? Redemption system
- ? Transaction history
- ? Lifetime statistics
- ? Progress visualizations
- ? Responsive design
- ? Database integration

---

## ?? Troubleshooting

### Points not appearing?
```sql
-- Check if booking is completed
SELECT booking_id, booking_status FROM Bookings WHERE booking_id = YOUR_BOOKING_ID;

-- Check loyalty record exists
SELECT * FROM LoyaltyPrograms WHERE client_id = YOUR_CLIENT_ID;

-- Check transactions
SELECT * FROM LoyaltyTransactions WHERE loyalty_id = YOUR_LOYALTY_ID;
```

### Tier not updating?
```csharp
// Refresh page or manually update:
var tier = LoyaltyTier.GetTierByPoints(points);
// Update database with new tier
```

### Rewards not loading?
```sql
-- Check active rewards
SELECT * FROM LoyaltyRewards WHERE is_active = 1 AND (expiry_date IS NULL OR expiry_date > GETDATE());
```

---

## ?? File Locations

```
Hospitality/
??? Models/
?   ??? LoyaltyProgram.cs ................. Data models
??? Services/
?   ??? LoyaltyService.cs ................. Business logic
?   ??? BookingService.cs ................. Booking + points
??? Components/Pages/
?   ??? ClientLoyalty.razor ............... Main UI
?   ??? ClientProfile.razor ............... Dashboard
?   ??? NewBooking.razor .................. Booking form
?   ??? RoomSelection.razor................ Room selection
??? wwwroot/css/
?   ??? client-loyalty.css ................ Styling
??? Database/
?   ??? LoyaltyProgramSetup.sql ........... Database script
??? LOYALTY_PROGRAM_README.md ............. Full documentation
??? LOYALTY_TESTING_GUIDE.md .............. Testing guide
??? LOYALTY_IMPLEMENTATION_SUMMARY.md ..... What was built
??? LOYALTY_UI_STRUCTURE.md ............... UI breakdown
??? LOYALTY_QUICK_REFERENCE.md ............ This file
```

---

## ?? Color Scheme

```
Primary:   #5e1369 (Purple)
Secondary: #9937c8 (Light Purple)
Success:   #059669 (Green)
Error:     #dc2626 (Red)
Bronze:    #cd7f32
Silver:    #c0c0c0
Gold:      #ffd700
Platinum:  #e5e4e2
```

---

## ?? Key Service Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `GetLoyaltyProgramAsync()` | Get/create loyalty | `LoyaltyProgram?` |
| `AddPointsForBookingAsync()` | Award points | `bool` |
| `RedeemPointsAsync()` | Redeem reward | `bool` |
| `GetAvailableRewardsAsync()` | List rewards | `List<LoyaltyReward>` |
| `GetTransactionHistoryAsync()` | Get history | `List<LoyaltyTransaction>` |

---

## ?? Need Help?

1. **Full Documentation**: `LOYALTY_PROGRAM_README.md`
2. **Testing Guide**: `LOYALTY_TESTING_GUIDE.md`
3. **Implementation Details**: `LOYALTY_IMPLEMENTATION_SUMMARY.md`
4. **UI Structure**: `LOYALTY_UI_STRUCTURE.md`

---

## ? Pre-Flight Checklist

Before going live:
- [ ] SQL script executed
- [ ] Build successful
- [ ] Loyalty page loads
- [ ] Points can be earned
- [ ] Rewards can be redeemed
- [ ] Transactions recorded
- [ ] Tier progression works
- [ ] Mobile responsive
- [ ] Navigation updated
- [ ] Service registered

---

## ?? Success Indicators

You'll know it's working when:
- ? Client can access `/client/loyalty/{id}`
- ? Points circle displays correctly
- ? Tier badge shows in navigation
- ? Rewards grid is populated
- ? Transaction history appears
- ? Booking completion awards points
- ? Tier upgrades automatically
- ? Redemption deducts points

---

## ?? Quick Stats Queries

```sql
-- Total members by tier
SELECT current_tier, COUNT(*) as members
FROM LoyaltyPrograms
GROUP BY current_tier;

-- Average points by tier
SELECT current_tier, AVG(total_points) as avg_points
FROM LoyaltyPrograms
GROUP BY current_tier;

-- Most popular rewards
SELECT reward_name, COUNT(*) as redemptions
FROM LoyaltyTransactions lt
JOIN LoyaltyRewards lr ON lt.description LIKE '%' + lr.reward_name + '%'
WHERE lt.transaction_type = 'redeem'
GROUP BY reward_name
ORDER BY redemptions DESC;

-- Points earned this month
SELECT SUM(points_earned) as monthly_points
FROM LoyaltyTransactions
WHERE transaction_type = 'earn'
AND transaction_date >= DATEADD(MONTH, -1, GETDATE());
```

---

## ?? Status Check

```bash
# Check if service is registered
# MauiProgram.cs should have:
builder.Services.AddSingleton<LoyaltyService>();

# Check if navigation links work
# ClientProfile.razor should have:
<a href="/client/loyalty/@id" class="nav-link">Loyalty</a>

# Check if build succeeds
dotnet build
# Should output: Build succeeded
```

---

## ?? Backup Commands

```sql
-- Backup loyalty data
SELECT * INTO LoyaltyPrograms_Backup FROM LoyaltyPrograms;
SELECT * INTO LoyaltyTransactions_Backup FROM LoyaltyTransactions;

-- Restore if needed
INSERT INTO LoyaltyPrograms SELECT * FROM LoyaltyPrograms_Backup;
INSERT INTO LoyaltyTransactions SELECT * FROM LoyaltyTransactions_Backup;
```

---

## ?? You're Ready!

The loyalty program is **production-ready** and fully integrated.

**Next Steps:**
1. Test with real users
2. Monitor engagement metrics
3. Adjust tier thresholds if needed
4. Add seasonal rewards
5. Track ROI and retention

---

**Quick Reference Version:** 1.0  
**Last Updated:** 2024  
**Build Status:** ? **Success**

*Happy loyalty building! ??*
