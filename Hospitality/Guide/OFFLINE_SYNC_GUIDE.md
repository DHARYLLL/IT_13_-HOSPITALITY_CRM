# Simplified Offline-First Sync System with Dual-Write Support

## Overview

This system enables your Hospitality app to work **offline-first** with automatic dual-write support:

- **When ONLINE**: Data is saved to **BOTH** local and online databases simultaneously
- **When OFFLINE**: Data is saved to local database with `sync_status = 'pending'`
- **When back ONLINE**: Records with `sync_status = 'pending'` are automatically synced

## Simplified Architecture

```
?????????????????????????????????????
?    MAUI App              ?
?????????????????????????????????????
           ?
    ???????????????
    ?        ?
????????????? ?????????????
?Connectivity? ?Sync    ?
?  Service   ? ?  Service?
?(Monitors)  ? ?(Syncs)    ?
????????????? ?????????????
    ?           ?
    ?????????????
?
   ????????????????
   ? DualWrite    ?
   ?  Service     ?
   ????????????????
       ?
    ?????????????
    ?  App      ?
    ? Services  ?
    ?????????????
          ?
???????????????????????????????
?  ?
?    ????????????  ????????????
?    ?  LOCAL   ?  ? ONLINE   ?
?    ? Database ?  ? Database ?
?    ????????????  ????????????
?  ?
?  Tables with tracking:     ?
?  - sync_status column      ?
?  - last_modified column    ?
?              ?
???????????????????????????????
```

**No SyncQueue or SyncStatus tables needed!**

## How It Works

### Scenario 1: User is ONLINE
```
User Action ? DualWriteService ? Write to LOCAL (sync_status = 'pending')
          ? Write to ONLINE (success!)
    ? Update LOCAL (sync_status = 'synced')
```

### Scenario 2: User is OFFLINE
```
User Action ? DualWriteService ? Write to LOCAL (sync_status = 'pending')
  ? Record stays pending for later sync
```

### Scenario 3: Back ONLINE (Auto-Sync)
```
Connection Restored ? ConnectivityService ? SyncService
    ? Find records with sync_status = 'pending'
      ? Push to ONLINE
          ? Mark as 'synced'
```

## Key Components

### 1. DualWriteService
The core service that handles intelligent write operations:

```csharp
// Automatically decides whether to:
// - Write to BOTH databases (when online)
// - Write to LOCAL only (when offline)

await _dualWriteService.ExecuteWriteAsync(
  "Booking",// Entity type
    "Bookings", // Table name
    "INSERT",           // Operation type
    async (con, tx) =>  // Local database action
    {
   // Your insert/update logic here
        return entityId;
    });
```

**What it does:**
1. Always writes to local database first (sets `sync_status = 'pending'`)
2. If online, immediately tries to write to online database
3. If online write succeeds, updates local record to `sync_status = 'synced'`
4. If offline or online fails, record stays `'pending'` for later sync

### 2. ConnectivityService
Monitors network and database availability:

```csharp
// Events you can subscribe to:
_connectivity.ConnectivityChanged += (isOnline) => { ... };
_connectivity.OnlineDbAvailable += () => { ... };  // Triggers auto-sync

// Check current status:
bool isOnline = await _connectivity.CheckOnlineDatabaseAsync();
string mode = _connectivity.CurrentMode; // "Online" or "Offline"
```

### 3. SyncService
Handles finding and syncing pending records:

```csharp
// Manually trigger sync
var result = await _syncService.SyncAllAsync();

// Get pending count
int pending = await _syncService.GetPendingChangesCountAsync();
```

**What it does:**
- Queries each table for `WHERE sync_status = 'pending'`
- Syncs those records to online database
- Updates them to `sync_status = 'synced'`

## Setup Instructions

### Step 1: Update Connection Strings

Edit `Hospitality\Database\DbConnection.cs`:

```csharp
// Local database (always available)
public const string Local = "Data Source=YOUR_LOCAL_SERVER\\SQLEXPRESS;...";

// Online database (requires internet)
public const string Online = "Data Source=db32979.public.databaseasp.net;...Password=YOUR_PASSWORD;...";
```

### Step 2: Run Database Setup Script

Execute `Hospitality\Database\SyncSetup.sql` on your **LOCAL** database:

```sql
-- This adds sync_status and last_modified columns to tables
-- No SyncQueue or SyncStatus tables created!
```

### Step 3: (Optional) Remove Old Tables

If you previously had SyncQueue/SyncStatus tables, run:

```sql
-- Execute Hospitality\Database\RemoveSyncTables.sql
```

### Step 4: Ensure Online Database Schema Matches

Your MonsterASP database should have the same tables as local.

## Service Registration (MauiProgram.cs)

Services are automatically registered with dual-write support:

```csharp
// Core services (must be first)
builder.Services.AddSingleton<ConnectivityService>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddSingleton<DualWriteService>();

// Application services with dual-write support
builder.Services.AddSingleton<BookingService>(sp => 
    new BookingService(
        sp.GetRequiredService<SyncService>(),
 sp.GetRequiredService<DualWriteService>()));

builder.Services.AddSingleton<PaymentService>(sp =>
    new PaymentService(
        sp.GetRequiredService<DualWriteService>(),
sp.GetRequiredService<SyncService>()));

// Similar for: RoomService, MessageService
```

## Supported Entity Types

| Entity | Table | Dual-Write | Auto-Sync |
|--------|-------|------------|-----------|
| Booking | Bookings | ? | ? |
| Payment | Payments | ? | ? |
| Message | Messages | ? | ? |
| Room | rooms | ? | ? |
| User | Users | ? | ? |
| Client | Clients | ? | ? |
| LoyaltyProgram | LoyaltyPrograms | ? | ? |
| LoyaltyTransaction | LoyaltyTransactions | ? | ? |

## Adding Dual-Write to Your Service

### Example: Custom Service with Dual-Write

```csharp
public class MyCustomService
{
    private readonly DualWriteService? _dualWriteService;
    private readonly SyncService? _syncService;

    public MyCustomService(DualWriteService dualWriteService, SyncService syncService)
{
        _dualWriteService = dualWriteService;
        _syncService = syncService;
    }

    public async Task<int> CreateRecordAsync(MyModel model)
    {
   if (_dualWriteService != null)
        {
      return await _dualWriteService.ExecuteWriteAsync(
     "MyEntity",
         "MyTable",
  "INSERT",
  async (con, tx) =>
 {
               // Insert into local database
        string sql = "INSERT INTO MyTable (...) VALUES (...); SELECT SCOPE_IDENTITY();";
          using var cmd = new SqlCommand(sql, con, tx);
             // Add parameters...
return Convert.ToInt32(await cmd.ExecuteScalarAsync());
           });
        }

        // Fallback for backward compatibility
 // ... original implementation ...
    }

    public async Task<bool> UpdateRecordAsync(int id, MyModel model)
    {
        if (_dualWriteService != null)
   {
      return await _dualWriteService.ExecuteWriteAsync(
     "MyEntity",
         "MyTable",
"UPDATE",
       id,
         async (con, tx) =>
        {
    string sql = "UPDATE MyTable SET ... WHERE id = @id";
        using var cmd = new SqlCommand(sql, con, tx);
      // Add parameters...
      await cmd.ExecuteNonQueryAsync();
     return true;
   },
        async (onlineCon, onlineTx) =>
  {
          // Optional: Custom online action
 // If not provided, uses automatic sync
          return true;
                });
        }

        // Fallback...
    }
}
```

### Register in MauiProgram.cs

```csharp
builder.Services.AddSingleton<MyCustomService>(sp =>
    new MyCustomService(
      sp.GetRequiredService<DualWriteService>(),
        sp.GetRequiredService<SyncService>()));
```

## UI Integration

### Sync Status Indicator

Add to your pages to show sync status:

```razor
<link rel="stylesheet" href="css/sync-status.css" />
<SyncStatusIndicator showDetails="true" />
```

### Manual Refresh Button

```razor
@inject SyncService SyncService
@inject ConnectivityService ConnectivityService

<button @onclick="RefreshAndSync">?? Sync Now</button>

@code {
    private async Task RefreshAndSync()
    {
  await ConnectivityService.RefreshAndSyncAsync();
    }
}
```

### Offline Banner

```razor
@inject ConnectivityService ConnectivityService

@if (!ConnectivityService.CanReachOnlineDatabase)
{
    <div class="offline-banner">
        ?? Working offline - changes will sync when connected
    </div>
}
```

## Monitoring Sync Status

### View Pending Records

```sql
-- Check each table for pending records
SELECT * FROM rooms WHERE sync_status = 'pending';
SELECT * FROM Bookings WHERE sync_status = 'pending';
SELECT * FROM Payments WHERE sync_status = 'pending';
SELECT * FROM Messages WHERE sync_status = 'pending';
```

### Count Pending Records

```sql
-- Count all pending records
SELECT 
    'rooms' AS TableName, COUNT(*) AS Pending FROM rooms WHERE sync_status = 'pending'
UNION ALL
SELECT 'Bookings', COUNT(*) FROM Bookings WHERE sync_status = 'pending'
UNION ALL
SELECT 'Payments', COUNT(*) FROM Payments WHERE sync_status = 'pending'
UNION ALL
SELECT 'Messages', COUNT(*) FROM Messages WHERE sync_status = 'pending';
```

### Reset Pending Records (if needed)

```sql
-- Mark all records as synced (use with caution!)
UPDATE rooms SET sync_status = 'synced' WHERE sync_status = 'pending';
UPDATE Bookings SET sync_status = 'synced' WHERE sync_status = 'pending';
UPDATE Payments SET sync_status = 'synced' WHERE sync_status = 'pending';
UPDATE Messages SET sync_status = 'synced' WHERE sync_status = 'pending';
```

## Files Reference

| File | Purpose |
|------|---------|
| `Services\DualWriteService.cs` | Handles dual-write logic |
| `Services\ConnectivityService.cs` | Monitors network/database connectivity |
| `Services\SyncService.cs` | Finds and syncs pending records |
| `Database\DbConnection.cs` | Local + Online connection strings |
| `Database\SyncSetup.sql` | Adds sync_status columns to tables |
| `Database\RemoveSyncTables.sql` | Removes old SyncQueue/SyncStatus tables |
| `Components\Shared\SyncStatusIndicator.razor` | UI sync status component |
| `wwwroot\css\sync-status.css` | Sync indicator styling |

## Best Practices

1. **Always use DualWriteService** for write operations
2. **Read from local database** - it's always available and up-to-date
3. **Don't block UI on sync** - sync runs in background
4. **Show offline status** to users so they know their changes will sync
5. **Handle backward compatibility** - keep fallback code for services without DualWriteService
6. **Monitor pending records** - check `sync_status = 'pending'` periodically

## Troubleshooting

### Sync Not Working
1. Check `ConnectivityService` is registered
2. Verify online connection string is correct
3. Test: `await DbConnection.CanConnectToOnlineAsync()`
4. Check tables have `sync_status` column: `SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE COLUMN_NAME = 'sync_status'`

### Data Not Appearing Online
1. Check for pending records: `SELECT * FROM [table] WHERE sync_status = 'pending'`
2. Verify table schemas match between local and online
3. Check for foreign key issues
4. Manually trigger sync: `await SyncService.SyncAllAsync()`

### Duplicate Records
The MERGE statements prevent duplicates, but verify primary keys match between databases.

### Old SyncQueue/SyncStatus Tables Still Exist
Run `Hospitality\Database\RemoveSyncTables.sql` to clean them up. They're no longer used!

## What Changed from Previous Version

### Removed ?
- **SyncQueue table** - No longer tracks changes in separate table
- **SyncStatus table** - No longer tracks last sync times
- **QueueChangeAsync()** - No longer needed

### Simplified ?
- **Table-level tracking only** - Each table's `sync_status` column tracks sync state
- **Direct sync from source tables** - SyncService reads directly from Bookings, Payments, etc.
- **Fewer database operations** - No INSERT into SyncQueue on every change
- **Easier to understand** - Just check `sync_status = 'pending'` to see what needs sync

---

**Status:** ? Simplified Sync System Active  
**Build:** ? Successful
**Features:** Online dual-write, Offline tracking, Auto-sync on reconnection

*Built for InnSight Hospitality CRM*
