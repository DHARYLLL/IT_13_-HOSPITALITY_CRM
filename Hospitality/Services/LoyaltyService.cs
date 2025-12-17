using Hospitality.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Hospitality.Services;

public class LoyaltyService
{
    private readonly SyncService? _syncService;
    private readonly DualWriteService? _dualWriteService;

    public LoyaltyService()
    {
        // Default constructor for backward compatibility
    }

    public LoyaltyService(SyncService syncService)
    {
        _syncService = syncService;
    }

    public LoyaltyService(SyncService syncService, DualWriteService dualWriteService)
    {
        _syncService = syncService;
        _dualWriteService = dualWriteService;
    }
    // Get or create loyalty program for a client
    public async Task<LoyaltyProgram?> GetLoyaltyProgramAsync(int clientId)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            // Check if loyalty program exists
            string checkSql = @"
            SELECT loyalty_id, client_id, total_points, current_tier, 
            member_since, lifetime_stays, lifetime_spend, last_stay_date, next_tier_expiry
            FROM LoyaltyPrograms
            WHERE client_id = @clientId";

            using var checkCmd = new SqlCommand(checkSql, con);
            checkCmd.Parameters.AddWithValue("@clientId", clientId);

            using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LoyaltyProgram
                {
                    loyalty_id = reader.GetInt32(0),
                    client_id = reader.GetInt32(1),
                    total_points = reader.GetInt32(2),
                    current_tier = reader.GetString(3),
                    member_since = reader.GetDateTime(4),
                    lifetime_stays = reader.GetInt32(5),
                    lifetime_spend = reader.GetDecimal(6),
                    last_stay_date = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    next_tier_expiry = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                };
            }

            // If not exists, create new loyalty program
            await reader.CloseAsync();
            return await CreateLoyaltyProgramAsync(clientId, con);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting loyalty program: {ex.Message}");
            return null;
        }
    }

    private async Task<LoyaltyProgram?> CreateLoyaltyProgramAsync(int clientId, SqlConnection con)
    {
        try
        {
            string insertSql = @"
            INSERT INTO LoyaltyPrograms (client_id, total_points, current_tier, member_since, lifetime_stays, lifetime_spend)
            VALUES (@clientId, 0, 'Bronze', @memberSince, 0, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var insertCmd = new SqlCommand(insertSql, con);
            insertCmd.Parameters.AddWithValue("@clientId", clientId);
            insertCmd.Parameters.AddWithValue("@memberSince", DateTime.Now);

            var loyaltyId = await insertCmd.ExecuteScalarAsync();

            return new LoyaltyProgram
            {
                loyalty_id = Convert.ToInt32(loyaltyId),
                client_id = clientId,
                total_points = 0,
                current_tier = "Bronze",
                member_since = DateTime.Now,
                lifetime_stays = 0,
                lifetime_spend = 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating loyalty program: {ex.Message}");
            return null;
        }
    }

    // Add points for a booking
    public async Task<bool> AddPointsForBookingAsync(int clientId, int bookingId, decimal amount)
    {
        try
        {
            var loyalty = await GetLoyaltyProgramAsync(clientId);
            if (loyalty == null) return false;

            var tier = LoyaltyTier.GetTierByName(loyalty.current_tier);

            // Calculate points based on tier (10-20 points per PHP)
            int pointsPerPHP = tier.Name switch
            {
                "Bronze" => 10,
                "Silver" => 12,
                "Gold" => 15,
                "Platinum" => 20,
                _ => 10
            };
            int pointsToAdd = (int)(amount * pointsPerPHP);

            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                // Update loyalty points
                string updateSql = @"
        UPDATE LoyaltyPrograms 
    SET total_points = total_points + @points,
 lifetime_spend = lifetime_spend + @amount,
          lifetime_stays = lifetime_stays + 1,
               last_stay_date = @lastStay
              WHERE loyalty_id = @loyaltyId";

                using var updateCmd = new SqlCommand(updateSql, con, transaction);
                updateCmd.Parameters.AddWithValue("@points", pointsToAdd);
                updateCmd.Parameters.AddWithValue("@amount", amount);
                updateCmd.Parameters.AddWithValue("@lastStay", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                await updateCmd.ExecuteNonQueryAsync();

                // Record transaction
                string transSql = @"
          INSERT INTO LoyaltyTransactions (loyalty_id, points_earned, points_redeemed, transaction_type, description, transaction_date, booking_id)
       VALUES (@loyaltyId, @points, 0, 'earn', @description, @date, @bookingId)";

                using var transCmd = new SqlCommand(transSql, con, transaction);
                transCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                transCmd.Parameters.AddWithValue("@points", pointsToAdd);
                transCmd.Parameters.AddWithValue("@description", $"Points earned from booking #{bookingId}");
                transCmd.Parameters.AddWithValue("@date", DateTime.Now);
                transCmd.Parameters.AddWithValue("@bookingId", bookingId);
                await transCmd.ExecuteNonQueryAsync();

                // Check for tier upgrade
                int newTotalPoints = loyalty.total_points + pointsToAdd;
                var newTier = LoyaltyTier.GetTierByPoints(newTotalPoints);

                if (newTier.Name != loyalty.current_tier)
                {
                    string tierUpdateSql = "UPDATE LoyaltyPrograms SET current_tier = @tier WHERE loyalty_id = @loyaltyId";
                    using var tierCmd = new SqlCommand(tierUpdateSql, con, transaction);
                    tierCmd.Parameters.AddWithValue("@tier", newTier.Name);
                    tierCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                    await tierCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding points: {ex.Message}");
            return false;
        }
    }

    // Redeem points for a reward
    public async Task<bool> RedeemPointsAsync(int clientId, int rewardId)
    {
        try
        {
            var loyalty = await GetLoyaltyProgramAsync(clientId);
            if (loyalty == null) return false;

            var reward = await GetRewardByIdAsync(rewardId);
            if (reward == null || loyalty.total_points < reward.points_required)
                return false;

            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                // Deduct points
                string updateSql = @"
       UPDATE LoyaltyPrograms 
    SET total_points = total_points - @points
     WHERE loyalty_id = @loyaltyId AND total_points >= @points";

                using var updateCmd = new SqlCommand(updateSql, con, transaction);
                updateCmd.Parameters.AddWithValue("@points", reward.points_required);
                updateCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // Record redemption transaction
                string transSql = @"
      INSERT INTO LoyaltyTransactions (loyalty_id, points_earned, points_redeemed, transaction_type, description, transaction_date)
          VALUES (@loyaltyId, 0, @points, 'redeem', @description, @date)";

                using var transCmd = new SqlCommand(transSql, con, transaction);
                transCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                transCmd.Parameters.AddWithValue("@points", reward.points_required);
                transCmd.Parameters.AddWithValue("@description", $"Redeemed: {reward.reward_name}");
                transCmd.Parameters.AddWithValue("@date", DateTime.Now);
                await transCmd.ExecuteNonQueryAsync();

                // Check for tier downgrade
                int newTotalPoints = loyalty.total_points - reward.points_required;
                var newTier = LoyaltyTier.GetTierByPoints(newTotalPoints);

                if (newTier.Name != loyalty.current_tier)
                {
                    string tierUpdateSql = "UPDATE LoyaltyPrograms SET current_tier = @tier WHERE loyalty_id = @loyaltyId";
                    using var tierCmd = new SqlCommand(tierUpdateSql, con, transaction);
                    tierCmd.Parameters.AddWithValue("@tier", newTier.Name);
                    tierCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                    await tierCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();

                // Create RedeemedReward record using DualWriteService (syncs automatically when online)
                DateTime? expiryDate = null;
                // Set expiry date based on reward type: vouchers expire in 1 year, services in 6 months
                if (reward.reward_type.ToLower() == "voucher")
                {
                    expiryDate = DateTime.Now.AddYears(1);
                }
                else if (reward.reward_type.ToLower() == "service" || reward.reward_type.ToLower() == "upgrade")
                {
                    expiryDate = DateTime.Now.AddMonths(6);
                }

                // Generate a simple voucher code (format: REW-{rewardId}-{timestamp})
                string voucherCode = $"REW-{rewardId}-{DateTime.Now:yyyyMMddHHmmss}";
                DateTime redemptionDate = DateTime.Now;

                // Use DualWriteService if available (syncs automatically when online)
                if (_dualWriteService != null)
                {
                    try
                    {
                        int redeemedId = await _dualWriteService.ExecuteWriteAsync(
                            "RedeemedReward",
                            "RedeemedRewards",
                            "INSERT",
                            async (localCon, localTx) =>
                            {
                                string redeemedRewardSql = @"
                                    INSERT INTO RedeemedRewards (loyalty_id, reward_id, redemption_date, status, expiry_date, voucher_code, sync_status)
                                    VALUES (@loyaltyId, @rewardId, @redemptionDate, 'active', @expiryDate, @voucherCode, 'pending');
                                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                                using var redeemedCmd = new SqlCommand(redeemedRewardSql, localCon, localTx);
                                redeemedCmd.Parameters.AddWithValue("@loyaltyId", loyalty.loyalty_id);
                                redeemedCmd.Parameters.AddWithValue("@rewardId", rewardId);
                                redeemedCmd.Parameters.AddWithValue("@redemptionDate", redemptionDate);
                                redeemedCmd.Parameters.AddWithValue("@expiryDate", expiryDate ?? (object)DBNull.Value);
                                redeemedCmd.Parameters.AddWithValue("@voucherCode", voucherCode);
                                var result = await redeemedCmd.ExecuteScalarAsync();
                                return result != null ? Convert.ToInt32(result) : 0;
                            });

                        if (redeemedId > 0)
                        {
                            Console.WriteLine($"✅ RedeemedReward #{redeemedId} created and synced");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error creating RedeemedReward with DualWriteService: {ex.Message}");
                        // Fallback to direct insert if DualWriteService fails
                        await CreateRedeemedRewardDirectlyAsync(loyalty.loyalty_id, rewardId, redemptionDate, expiryDate, voucherCode);
                    }
                }
                else
                {
                    // Fallback to direct insert if DualWriteService is not available
                    await CreateRedeemedRewardDirectlyAsync(loyalty.loyalty_id, rewardId, redemptionDate, expiryDate, voucherCode);
                }

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error redeeming points: {ex.Message}");
            return false;
        }
    }

    // Get available rewards (for clients - only active)
    public async Task<List<LoyaltyReward>> GetAvailableRewardsAsync()
    {
        var rewards = new List<LoyaltyReward>();
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
   SELECT reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date
        FROM LoyaltyRewards
     WHERE is_active = 1 AND (expiry_date IS NULL OR expiry_date > @now)
              ORDER BY points_required";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rewards.Add(new LoyaltyReward
                {
                    reward_id = reader.GetInt32(0),
                    reward_name = reader.GetString(1),
                    reward_description = reader.GetString(2),
                    points_required = reader.GetInt32(3),
                    reward_type = reader.GetString(4),
                    is_active = reader.GetBoolean(5),
                    expiry_date = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting rewards: {ex.Message}");
        }
        return rewards;
    }

    // Get ALL rewards (for admin - includes inactive)
    public async Task<List<LoyaltyReward>> GetAllRewardsAsync()
    {
        var rewards = new List<LoyaltyReward>();
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
     SELECT reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date
         FROM LoyaltyRewards
                ORDER BY points_required";

            using var cmd = new SqlCommand(sql, con);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rewards.Add(new LoyaltyReward
                {
                    reward_id = reader.GetInt32(0),
                    reward_name = reader.GetString(1),
                    reward_description = reader.GetString(2),
                    points_required = reader.GetInt32(3),
                    reward_type = reader.GetString(4),
                    is_active = reader.GetBoolean(5),
                    expiry_date = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting all rewards: {ex.Message}");
        }
        return rewards;
    }

    // Get reward by ID (public version)
    public async Task<LoyaltyReward?> GetRewardByIdPublicAsync(int rewardId)
    {
        return await GetRewardByIdAsync(rewardId);
    }

    private async Task<LoyaltyReward?> GetRewardByIdAsync(int rewardId)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
   SELECT reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date
                FROM LoyaltyRewards
        WHERE reward_id = @rewardId";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@rewardId", rewardId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LoyaltyReward
                {
                    reward_id = reader.GetInt32(0),
                    reward_name = reader.GetString(1),
                    reward_description = reader.GetString(2),
                    points_required = reader.GetInt32(3),
                    reward_type = reader.GetString(4),
                    is_active = reader.GetBoolean(5),
                    expiry_date = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting reward: {ex.Message}");
        }
        return null;
    }

    // Create new reward
    public async Task<int> CreateRewardAsync(LoyaltyReward reward)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            // Check if sync_status column exists
            bool hasSyncColumn = await HasColumnAsync(con, "LoyaltyRewards", "sync_status");

            string sql = hasSyncColumn
                ? @"
                    INSERT INTO LoyaltyRewards (reward_name, reward_description, points_required, reward_type, is_active, expiry_date, sync_status)
                    VALUES (@name, @description, @points, @type, @isActive, @expiry, 'pending');
                    SELECT CAST(SCOPE_IDENTITY() AS INT);"
                : @"
                    INSERT INTO LoyaltyRewards (reward_name, reward_description, points_required, reward_type, is_active, expiry_date)
                    VALUES (@name, @description, @points, @type, @isActive, @expiry);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@name", reward.reward_name);
            cmd.Parameters.AddWithValue("@description", reward.reward_description);
            cmd.Parameters.AddWithValue("@points", reward.points_required);
            cmd.Parameters.AddWithValue("@type", reward.reward_type);
            cmd.Parameters.AddWithValue("@isActive", reward.is_active);
            cmd.Parameters.AddWithValue("@expiry", (object?)reward.expiry_date ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            int rewardId = Convert.ToInt32(result);

            // Mark for sync if sync service is available
            if (_syncService != null && rewardId > 0)
            {
                await _syncService.MarkForSyncAsync("LoyaltyRewards", rewardId, "INSERT");
            }

            return rewardId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating reward: {ex.Message}");
            return 0;
        }
    }

    private async Task<bool> HasColumnAsync(SqlConnection con, string tableName, string columnName)
    {
        try
        {
            string sql = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@tableName", tableName);
            cmd.Parameters.AddWithValue("@columnName", columnName);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    // Update existing reward
    public async Task<bool> UpdateRewardAsync(LoyaltyReward reward)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            // Check if sync_status column exists
            bool hasSyncColumn = await HasColumnAsync(con, "LoyaltyRewards", "sync_status");

            string sql = hasSyncColumn
                ? @"
                    UPDATE LoyaltyRewards 
                    SET reward_name = @name,
                        reward_description = @description,
                        points_required = @points,
                        reward_type = @type,
                        is_active = @isActive,
                        expiry_date = @expiry,
                        sync_status = 'pending',
                        last_modified = GETDATE()
                    WHERE reward_id = @rewardId"
                : @"
                    UPDATE LoyaltyRewards 
                    SET reward_name = @name,
                        reward_description = @description,
                        points_required = @points,
                        reward_type = @type,
                        is_active = @isActive,
                        expiry_date = @expiry
                    WHERE reward_id = @rewardId";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@rewardId", reward.reward_id);
            cmd.Parameters.AddWithValue("@name", reward.reward_name);
            cmd.Parameters.AddWithValue("@description", reward.reward_description);
            cmd.Parameters.AddWithValue("@points", reward.points_required);
            cmd.Parameters.AddWithValue("@type", reward.reward_type);
            cmd.Parameters.AddWithValue("@isActive", reward.is_active);
            cmd.Parameters.AddWithValue("@expiry", (object?)reward.expiry_date ?? DBNull.Value);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            
            // Mark for sync if sync service is available
            if (_syncService != null && rowsAffected > 0)
            {
                await _syncService.MarkForSyncAsync("LoyaltyRewards", reward.reward_id, "UPDATE");
            }
            
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating reward: {ex.Message}");
            return false;
        }
    }

    // Delete reward
    public async Task<bool> DeleteRewardAsync(int rewardId)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = "DELETE FROM LoyaltyRewards WHERE reward_id = @rewardId";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@rewardId", rewardId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting reward: {ex.Message}");
            return false;
        }
    }

    // Toggle reward active status
    public async Task<bool> ToggleRewardActiveAsync(int rewardId)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
    UPDATE LoyaltyRewards 
      SET is_active = CASE WHEN is_active = 1 THEN 0 ELSE 1 END
   WHERE reward_id = @rewardId";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@rewardId", rewardId);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling reward status: {ex.Message}");
            return false;
        }
    }

    // Get rewards count
    public async Task<int> GetRewardsCountAsync(string searchQuery = "", string typeFilter = "")
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = "SELECT COUNT(*) FROM LoyaltyRewards WHERE 1=1";

            if (!string.IsNullOrEmpty(searchQuery))
                sql += " AND (reward_name LIKE @search OR reward_description LIKE @search)";
            if (!string.IsNullOrEmpty(typeFilter))
                sql += " AND reward_type = @type";

            using var cmd = new SqlCommand(sql, con);
            if (!string.IsNullOrEmpty(searchQuery))
                cmd.Parameters.AddWithValue("@search", $"%{searchQuery}%");
            if (!string.IsNullOrEmpty(typeFilter))
                cmd.Parameters.AddWithValue("@type", typeFilter);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting rewards count: {ex.Message}");
            return 0;
        }
    }

    // Get paginated rewards
    public async Task<List<LoyaltyReward>> GetRewardsPagedAsync(int page, int pageSize, string searchQuery = "", string typeFilter = "")
    {
        var rewards = new List<LoyaltyReward>();
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
     SELECT reward_id, reward_name, reward_description, points_required, reward_type, is_active, expiry_date
            FROM LoyaltyRewards
             WHERE 1=1";

            if (!string.IsNullOrEmpty(searchQuery))
                sql += " AND (reward_name LIKE @search OR reward_description LIKE @search)";
            if (!string.IsNullOrEmpty(typeFilter))
                sql += " AND reward_type = @type";

            sql += " ORDER BY points_required OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            if (!string.IsNullOrEmpty(searchQuery))
                cmd.Parameters.AddWithValue("@search", $"%{searchQuery}%");
            if (!string.IsNullOrEmpty(typeFilter))
                cmd.Parameters.AddWithValue("@type", typeFilter);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rewards.Add(new LoyaltyReward
                {
                    reward_id = reader.GetInt32(0),
                    reward_name = reader.GetString(1),
                    reward_description = reader.GetString(2),
                    points_required = reader.GetInt32(3),
                    reward_type = reader.GetString(4),
                    is_active = reader.GetBoolean(5),
                    expiry_date = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting paginated rewards: {ex.Message}");
        }
        return rewards;
    }

    // Get loyalty transaction history
    public async Task<List<LoyaltyTransaction>> GetTransactionHistoryAsync(int clientId, int limit = 10)
    {
        var transactions = new List<LoyaltyTransaction>();
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
                SELECT TOP (@limit) lt.transaction_id, lt.loyalty_id, lt.points_earned, lt.points_redeemed, 
                lt.transaction_type, lt.description, lt.transaction_date, lt.booking_id
                FROM LoyaltyTransactions lt
                INNER JOIN LoyaltyPrograms lp ON lt.loyalty_id = lp.loyalty_id
                WHERE lp.client_id = @clientId
                ORDER BY lt.transaction_date DESC";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@clientId", clientId);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                transactions.Add(new LoyaltyTransaction
                {
                    transaction_id = reader.GetInt32(0),
                    loyalty_id = reader.GetInt32(1),
                    points_earned = reader.GetInt32(2),
                    points_redeemed = reader.GetInt32(3),
                    transaction_type = reader.GetString(4),
                    description = reader.GetString(5),
                    transaction_date = reader.GetDateTime(6),
                    booking_id = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting transaction history: {ex.Message}");
        }
        return transactions;
    }

    // Validate and get redeemed reward by voucher code
    public async Task<RedeemedReward?> ValidateVoucherCodeAsync(string voucherCode, int clientId)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
                SELECT rr.redeemed_id, rr.loyalty_id, rr.reward_id, rr.redemption_date, 
                       rr.status, rr.used_date, rr.booking_id, rr.expiry_date, rr.voucher_code, rr.notes,
                       lr.reward_name, lr.reward_description, lr.reward_type
                FROM RedeemedRewards rr
                INNER JOIN LoyaltyPrograms lp ON rr.loyalty_id = lp.loyalty_id
                INNER JOIN LoyaltyRewards lr ON rr.reward_id = lr.reward_id
                WHERE rr.voucher_code = @voucherCode 
                  AND lp.client_id = @clientId
                  AND rr.status = 'active'
                  AND (rr.expiry_date IS NULL OR rr.expiry_date > GETDATE())
                  AND rr.used_date IS NULL";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@voucherCode", voucherCode);
            cmd.Parameters.AddWithValue("@clientId", clientId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RedeemedReward
                {
                    redeemed_id = reader.GetInt32(0),
                    loyalty_id = reader.GetInt32(1),
                    reward_id = reader.GetInt32(2),
                    redemption_date = reader.GetDateTime(3),
                    status = reader.GetString(4),
                    used_date = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    booking_id = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    expiry_date = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    voucher_code = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                    reward_name = reader.IsDBNull(10) ? null : reader.GetString(10),
                    reward_description = reader.IsDBNull(11) ? null : reader.GetString(11),
                    reward_type = reader.IsDBNull(12) ? null : reader.GetString(12)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating voucher code: {ex.Message}");
        }
        return null;
    }

    // Get redeemed rewards for a client
    public async Task<List<RedeemedReward>> GetRedeemedRewardsAsync(int clientId)
    {
        var redeemedRewards = new List<RedeemedReward>();
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string sql = @"
                SELECT rr.redeemed_id, rr.loyalty_id, rr.reward_id, rr.redemption_date, 
                       rr.status, rr.used_date, rr.booking_id, rr.expiry_date, rr.voucher_code, rr.notes,
                       lr.reward_name, lr.reward_description, lr.reward_type
                FROM RedeemedRewards rr
                INNER JOIN LoyaltyPrograms lp ON rr.loyalty_id = lp.loyalty_id
                INNER JOIN LoyaltyRewards lr ON rr.reward_id = lr.reward_id
                WHERE lp.client_id = @clientId
                ORDER BY rr.redemption_date DESC";

            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@clientId", clientId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                redeemedRewards.Add(new RedeemedReward
                {
                    redeemed_id = reader.GetInt32(0),
                    loyalty_id = reader.GetInt32(1),
                    reward_id = reader.GetInt32(2),
                    redemption_date = reader.GetDateTime(3),
                    status = reader.GetString(4),
                    used_date = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    booking_id = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    expiry_date = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    voucher_code = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                    reward_name = reader.IsDBNull(10) ? null : reader.GetString(10),
                    reward_description = reader.IsDBNull(11) ? null : reader.GetString(11),
                    reward_type = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting redeemed rewards: {ex.Message}");
        }
        return redeemedRewards;
    }

    // Helper method to create RedeemedReward directly (fallback when DualWriteService is not available)
    private async Task CreateRedeemedRewardDirectlyAsync(int loyaltyId, int rewardId, DateTime redemptionDate, DateTime? expiryDate, string voucherCode)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();

            string redeemedRewardSql = @"
                INSERT INTO RedeemedRewards (loyalty_id, reward_id, redemption_date, status, expiry_date, voucher_code, sync_status)
                VALUES (@loyaltyId, @rewardId, @redemptionDate, 'active', @expiryDate, @voucherCode, 'pending');";

            using var redeemedCmd = new SqlCommand(redeemedRewardSql, con);
            redeemedCmd.Parameters.AddWithValue("@loyaltyId", loyaltyId);
            redeemedCmd.Parameters.AddWithValue("@rewardId", rewardId);
            redeemedCmd.Parameters.AddWithValue("@redemptionDate", redemptionDate);
            redeemedCmd.Parameters.AddWithValue("@expiryDate", expiryDate ?? (object)DBNull.Value);
            redeemedCmd.Parameters.AddWithValue("@voucherCode", voucherCode);
            await redeemedCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating RedeemedReward directly: {ex.Message}");
        }
    }

    // Mark a redeemed reward as used when applied to a booking
    public async Task<bool> MarkRewardAsUsedAsync(string voucherCode, int bookingId, int clientId)
    {
        try
        {
            using var con = Database.DbConnection.GetConnection();
            await con.OpenAsync();
            using var transaction = con.BeginTransaction();

            try
            {
                // First, get the redeemed reward to verify it belongs to this client
                string verifySql = @"
                    SELECT rr.redeemed_id, rr.loyalty_id
                    FROM RedeemedRewards rr
                    INNER JOIN LoyaltyPrograms lp ON rr.loyalty_id = lp.loyalty_id
                    WHERE rr.voucher_code = @voucherCode
                      AND lp.client_id = @clientId
                      AND rr.status = 'active'
                      AND rr.used_date IS NULL";

                using var verifyCmd = new SqlCommand(verifySql, con, transaction);
                verifyCmd.Parameters.AddWithValue("@voucherCode", voucherCode);
                verifyCmd.Parameters.AddWithValue("@clientId", clientId);

                using var reader = await verifyCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"❌ Voucher {voucherCode} not found or already used for client {clientId}");
                    return false;
                }

                int redeemedId = reader.GetInt32(0);
                await reader.CloseAsync();

                // Mark as used using DualWriteService if available
                if (_dualWriteService != null)
                {
                    try
                    {
                        bool success = await _dualWriteService.ExecuteWriteAsync(
                            "RedeemedReward",
                            "RedeemedRewards",
                            "UPDATE",
                            redeemedId,
                            async (localCon, localTx) =>
                            {
                                string updateSql = @"
                                    UPDATE RedeemedRewards 
                                    SET status = 'used',
                                        used_date = @usedDate,
                                        booking_id = @bookingId
                                    WHERE redeemed_id = @redeemedId";

                                using var updateCmd = new SqlCommand(updateSql, localCon, localTx);
                                updateCmd.Parameters.AddWithValue("@redeemedId", redeemedId);
                                updateCmd.Parameters.AddWithValue("@usedDate", DateTime.Now);
                                updateCmd.Parameters.AddWithValue("@bookingId", bookingId);
                                
                                int rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                                return rowsAffected > 0;
                            });

                        if (success)
                        {
                            Console.WriteLine($"✅ RedeemedReward #{redeemedId} marked as used for booking #{bookingId}");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ DualWriteService returned false for RedeemedReward #{redeemedId}");
                            // Fallback to direct update
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error marking reward as used with DualWriteService: {ex.Message}");
                        // Fallback to direct update
                    }
                }

                // Fallback: Direct update
                string updateSql = @"
                    UPDATE RedeemedRewards 
                    SET status = 'used',
                        used_date = @usedDate,
                        booking_id = @bookingId,
                        sync_status = 'pending'
                    WHERE redeemed_id = @redeemedId";

                using var updateCmd = new SqlCommand(updateSql, con, transaction);
                updateCmd.Parameters.AddWithValue("@redeemedId", redeemedId);
                updateCmd.Parameters.AddWithValue("@usedDate", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@bookingId", bookingId);
                
                int rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    // Mark for sync
                    if (_syncService != null)
                    {
                        await _syncService.MarkForSyncAsync("RedeemedRewards", redeemedId, "UPDATE");
                    }
                    
                    await transaction.CommitAsync();
                    Console.WriteLine($"✅ RedeemedReward #{redeemedId} marked as used for booking #{bookingId}");
                    return true;
                }
                else
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error marking reward as used: {ex.Message}");
            return false;
        }
    }
}
