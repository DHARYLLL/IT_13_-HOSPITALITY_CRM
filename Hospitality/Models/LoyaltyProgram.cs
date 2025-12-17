namespace Hospitality.Models;

public class LoyaltyProgram
{
    public int loyalty_id { get; set; }
    public int client_id { get; set; }
    public int total_points { get; set; }
    public string current_tier { get; set; } = "Bronze";
    public DateTime member_since { get; set; }
    public int lifetime_stays { get; set; }
    public decimal lifetime_spend { get; set; }
    public DateTime? last_stay_date { get; set; }
    public DateTime? next_tier_expiry { get; set; }
    
    // PascalCase aliases
    public int LoyaltyId => loyalty_id;
    public int ClientId => client_id;
  public int TotalPoints => total_points;
    public string CurrentTier => current_tier;
    public DateTime MemberSince => member_since;
    public int LifetimeStays => lifetime_stays;
    public decimal LifetimeSpend => lifetime_spend;
}

public class LoyaltyTier
{
    public string Name { get; set; } = "";
    public int MinPoints { get; set; }
    public int MaxPoints { get; set; }
    public string Color { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<string> Benefits { get; set; } = new();
    
    public static readonly List<LoyaltyTier> Tiers = new()
    {
        new LoyaltyTier
        {
Name = "Bronze",
     MinPoints = 0,
       MaxPoints = 2499,
  Color = "#cd7f32",
     Icon = "🥉",
  Benefits = new List<string>
         {
     "Earn 10 points per peso spent",
                "Member-only rates and offers",
                "Free WiFi"
            }
        },
        new LoyaltyTier
        {
      Name = "Silver",
            MinPoints = 2500,
            MaxPoints = 6999,
 Color = "#c0c0c0",
 Icon = "🥈",
    Benefits = new List<string>
  {
         "All Bronze benefits",
  "Earn 12 points per peso spent",
          "Priority check-in",
     "Complimentary room upgrade (subject to availability)"
            }
 },
        new LoyaltyTier
        {
    Name = "Gold",
      MinPoints = 7000,
       MaxPoints = 14999,
            Color = "#ffd700",
       Icon = "🥇",
            Benefits = new List<string>
     {
     "All Silver benefits",
      "Earn 15 points per peso spent",
          "Late checkout (2pm)",
    "Complimentary welcome drink",
 "Access to exclusive member rates"
  }
        },
        new LoyaltyTier
        {
          Name = "Platinum",
      MinPoints = 15000,
      MaxPoints = int.MaxValue,
          Color = "#e5e4e2",
Icon = "💎",
            Benefits = new List<string>
        {
    "All Gold benefits",
       "Earn 20 points per peso spent",
           "Guaranteed room upgrade",
  "Late checkout (4pm)",
        "Complimentary breakfast for two",
        "Personal concierge service",
  "50% bonus points on stays"
            }
        }
    };
    
    public static LoyaltyTier GetTierByPoints(int points)
    {
        return Tiers.LastOrDefault(t => points >= t.MinPoints) ?? Tiers[0];
    }
    
    public static LoyaltyTier GetTierByName(string name)
    {
        return Tiers.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? Tiers[0];
    }
  
    public static LoyaltyTier GetNextTier(string currentTier)
    {
      var currentIndex = Tiers.FindIndex(t => t.Name.Equals(currentTier, StringComparison.OrdinalIgnoreCase));
        if (currentIndex >= 0 && currentIndex < Tiers.Count - 1)
        {
   return Tiers[currentIndex + 1];
}
        return Tiers[^1]; // Return highest tier if already at max
  }
}

public class LoyaltyReward
{
    public int reward_id { get; set; }
    public string reward_name { get; set; } = "";
    public string reward_description { get; set; } = "";
    public int points_required { get; set; }
  public string reward_type { get; set; } = ""; // voucher, upgrade, service
    public bool is_active { get; set; } = true;
    public DateTime? expiry_date { get; set; }
    
    public int RewardId => reward_id;
    public string RewardName => reward_name;
public string RewardDescription => reward_description;
 public int PointsRequired => points_required;
    public string RewardType => reward_type;
}

public class LoyaltyTransaction
{
    public int transaction_id { get; set; }
    public int loyalty_id { get; set; }
    public int points_earned { get; set; }
    public int points_redeemed { get; set; }
    public string transaction_type { get; set; } = ""; // earn, redeem
    public string description { get; set; } = "";
    public DateTime transaction_date { get; set; }
    public int? booking_id { get; set; }
    
    public int TransactionId => transaction_id;
    public int LoyaltyId => loyalty_id;
    public int PointsEarned => points_earned;
    public int PointsRedeemed => points_redeemed;
    public string TransactionType => transaction_type;
    public string Description => description;
    public DateTime TransactionDate => transaction_date;
}

public class RedeemedReward
{
    public int redeemed_id { get; set; }
    public int loyalty_id { get; set; }
    public int reward_id { get; set; }
    public DateTime redemption_date { get; set; }
    public string status { get; set; } = "active";
    public DateTime? used_date { get; set; }
    public int? booking_id { get; set; }
    public DateTime? expiry_date { get; set; }
    public string voucher_code { get; set; } = "";
    public string? notes { get; set; }
    
    // Reward details (populated when loading)
    public string? reward_name { get; set; }
    public string? reward_description { get; set; }
    public string? reward_type { get; set; }
    
    public int RedeemedId => redeemed_id;
    public int LoyaltyId => loyalty_id;
    public int RewardId => reward_id;
    public DateTime RedemptionDate => redemption_date;
    public string Status => status;
    public DateTime? UsedDate => used_date;
    public int? BookingId => booking_id;
    public DateTime? ExpiryDate => expiry_date;
    public string VoucherCode => voucher_code;
    public bool IsExpired => expiry_date.HasValue && expiry_date.Value < DateTime.Now;
    public bool IsUsed => used_date.HasValue;
}
