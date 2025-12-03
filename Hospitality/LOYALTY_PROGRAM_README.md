# InnSight Loyalty & Rewards Program

## Overview
A comprehensive customer loyalty and rewards system for the InnSight Hospitality CRM. This system allows clients to earn points on bookings, track their membership tier, and redeem rewards.

## Features

### ?? **Multi-Tier Membership System**
- **Bronze Tier** (0-2,499 points)
  - 10 points per $1 spent
  - Member-only rates
  - Free WiFi

- **Silver Tier** (2,500-6,999 points)
  - 12 points per $1 spent
  - Priority check-in
  - Complimentary room upgrades (subject to availability)

- **Gold Tier** (7,000-14,999 points)
  - 15 points per $1 spent
  - Late checkout (2pm)
  - Complimentary welcome drink
  - Exclusive member rates

- **Platinum Tier** (15,000+ points)
  - 20 points per $1 spent
- Guaranteed room upgrades
  - Late checkout (4pm)
  - Complimentary breakfast for two
  - Personal concierge service
  - 50% bonus points on stays

### ?? **Key Features**
- ? Automatic point accrual on completed bookings
- ? Real-time tier progression tracking
- ? Redeemable rewards catalog
- ? Transaction history and activity log
- ? Lifetime statistics (stays, spend, points)
- ? Visual progress indicators
- ? Mobile-responsive design

## Database Setup

### 1. Run the SQL Setup Script
Execute the `LoyaltyProgramSetup.sql` script in your SQL Server database:

```sql
-- Navigate to: Hospitality/Database/LoyaltyProgramSetup.sql
-- Run the script in SQL Server Management Studio or Azure Data Studio
```

This will create:
- `LoyaltyPrograms` table
- `LoyaltyRewards` table
- `LoyaltyTransactions` table
- Sample rewards data
- Indexes for performance

### 2. Verify Tables
Check that the following tables exist:
```sql
SELECT * FROM LoyaltyPrograms;
SELECT * FROM LoyaltyRewards;
SELECT * FROM LoyaltyTransactions;
```

## Usage

### For Clients

#### Access Loyalty Dashboard
1. Log in to your client account
2. Navigate to **Loyalty** in the top navigation
3. View your:
   - Current points and tier
   - Progress to next tier
   - Available rewards
   - Transaction history
   - Lifetime statistics

#### Earn Points
Points are automatically earned when:
- Booking is completed (checked out)
- Points calculated based on your tier:
  - Bronze: 10 points per $1
  - Silver: 12 points per $1
  - Gold: 15 points per $1
  - Platinum: 20 points per $1

#### Redeem Rewards
1. Go to the Loyalty page
2. Browse available rewards
3. Click "Redeem" on any reward you have enough points for
4. Points will be deducted automatically
5. Reward will be applied to your account

### For Developers

#### Add Points Programmatically
```csharp
// Inject the service
@inject Hospitality.Services.LoyaltyService LoyaltyService

// Add points for a booking
await LoyaltyService.AddPointsForBookingAsync(clientId, bookingId, bookingAmount);
```

#### Get Loyalty Program
```csharp
var loyaltyProgram = await LoyaltyService.GetLoyaltyProgramAsync(clientId);
Console.WriteLine($"Tier: {loyaltyProgram.current_tier}");
Console.WriteLine($"Points: {loyaltyProgram.total_points}");
```

#### Redeem Points
```csharp
bool success = await LoyaltyService.RedeemPointsAsync(clientId, rewardId);
if (success)
{
    // Redemption successful
}
```

## API Reference

### LoyaltyService Methods

#### `GetLoyaltyProgramAsync(int clientId)`
Gets or creates a loyalty program for a client.

**Returns:** `LoyaltyProgram?`

#### `AddPointsForBookingAsync(int clientId, int bookingId, decimal amount)`
Adds points for a completed booking. Automatically calculates points based on tier.

**Parameters:**
- `clientId`: The client's ID
- `bookingId`: The booking ID
- `amount`: The booking amount in dollars

**Returns:** `bool` - Success status

#### `RedeemPointsAsync(int clientId, int rewardId)`
Redeems points for a reward.

**Parameters:**
- `clientId`: The client's ID
- `rewardId`: The reward ID to redeem

**Returns:** `bool` - Success status

#### `GetAvailableRewardsAsync()`
Gets all active rewards that haven't expired.

**Returns:** `List<LoyaltyReward>`

#### `GetTransactionHistoryAsync(int clientId, int limit = 10)`
Gets transaction history for a client.

**Parameters:**
- `clientId`: The client's ID
- `limit`: Maximum number of transactions to return

**Returns:** `List<LoyaltyTransaction>`

## Models

### LoyaltyProgram
```csharp
public class LoyaltyProgram
{
    public int loyalty_id { get; set; }
    public int client_id { get; set; }
    public int total_points { get; set; }
public string current_tier { get; set; } // Bronze, Silver, Gold, Platinum
    public DateTime member_since { get; set; }
    public int lifetime_stays { get; set; }
    public decimal lifetime_spend { get; set; }
    public DateTime? last_stay_date { get; set; }
}
```

### LoyaltyReward
```csharp
public class LoyaltyReward
{
    public int reward_id { get; set; }
    public string reward_name { get; set; }
    public string reward_description { get; set; }
    public int points_required { get; set; }
    public string reward_type { get; set; } // voucher, upgrade, service
    public bool is_active { get; set; }
    public DateTime? expiry_date { get; set; }
}
```

### LoyaltyTransaction
```csharp
public class LoyaltyTransaction
{
    public int transaction_id { get; set; }
    public int loyalty_id { get; set; }
    public int points_earned { get; set; }
    public int points_redeemed { get; set; }
    public string transaction_type { get; set; } // earn, redeem
    public string description { get; set; }
    public DateTime transaction_date { get; set; }
    public int? booking_id { get; set; }
}
```

## Pages

### `/client/loyalty/{clientId}`
Main loyalty dashboard showing:
- Current tier and points
- Progress visualization
- Tier benefits
- Available rewards
- Transaction history
- Tier comparison table

### Navigation
The loyalty page is accessible from:
- Client Profile Dashboard
- New Booking page
- Room Selection page
- Top navigation bar (when logged in)

## Customization

### Add New Rewards
```sql
INSERT INTO LoyaltyRewards (reward_name, reward_description, points_required, reward_type, is_active)
VALUES ('Custom Reward', 'Description here', 5000, 'voucher', 1);
```

### Modify Tier Thresholds
Edit `Hospitality/Models/LoyaltyProgram.cs`:
```csharp
public static readonly List<LoyaltyTier> Tiers = new()
{
    new LoyaltyTier { Name = "Bronze", MinPoints = 0, MaxPoints = 2499, ... },
  // Modify thresholds here
};
```

### Change Points Earning Rate
Modify `AddPointsForBookingAsync` in `LoyaltyService.cs`:
```csharp
int bonusMultiplier = tier.Name switch
{
    "Silver" => 12,
    "Gold" => 15,
    "Platinum" => 20,
    _ => 10
};
```

## Testing

### Test Loyalty System
1. Create a client account
2. Make a booking
3. Complete the booking (set status to 'completed')
4. Check loyalty dashboard
5. Verify points are credited
6. Try redeeming a reward

### Sample Test Data
```sql
-- Give test client some points
UPDATE LoyaltyPrograms 
SET total_points = 10000, 
    current_tier = 'Gold',
    lifetime_stays = 5,
lifetime_spend = 2500
WHERE client_id = 1;
```

## Troubleshooting

### Points Not Appearing
- Verify booking status is 'completed'
- Check that client has a loyalty program record
- Review transaction history for errors
- Check database foreign key relationships

### Rewards Not Loading
- Ensure rewards are marked as `is_active = 1`
- Check expiry dates
- Verify database connection

### Tier Not Updating
- Points calculation may be cached
- Refresh the page
- Check tier thresholds in code

## Future Enhancements
- ?? Points expiration system
- ?? Referral bonuses
- ?? Birthday rewards
- ?? Special promotions and double points events
- ?? Partner rewards integration
- ?? Mobile app push notifications
- ?? Gamification elements (badges, achievements)

## Support
For issues or questions, contact the development team or refer to the main project documentation.

---

**Version:** 1.0  
**Last Updated:** 2024  
**License:** Proprietary - InnSight Hospitality CRM
