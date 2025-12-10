# Offline-First Sync System with Dual-Write Support

## Overview

This system enables your Hospitality app to work **offline-first** with automatic dual-write support:

- **When ONLINE**: Data is saved to **BOTH** local and online databases simultaneously
- **When OFFLINE**: Data is saved to local database and queued for sync
- **When back ONLINE**: Pending changes are automatically synced to online database

## Architecture

```
???????????????????????????????????????????????????????????????????
?        MAUI App    ?
???????????????????????????????????????????????????????????????????
?          ?
?  ??????????????????? ???????????????????          ?
?  ? ConnectivitySvc ?    ?   SyncService   ?           ?
?  ? (Monitors Net)  ?    ? (Queues/Syncs)  ?             ?
?  ???????????????????    ???????????????????     ?
?    ?           ? ?
?    ????????????????????????     ?
?         ?       ?
?           ?????????????????????????       ?
?           ?   DualWriteService    ? ??? NEW! Handles dual-write ?
?           ?  (Online/Offline)     ?         ?
?           ?????????????????????????   ?
?      ?   ?
?  ???????????????????????????????????????????   ?
?  ?   Application Services       ?       ?
?  ?  BookingService, PaymentService,        ?           ?
?  ?  MessageService, RoomService, etc.      ?          ?
?  ???????????????????????????????????????????    ?
?      ?           ?
???????????????????????????????????????????????????????????????????
      ?
        ?????????????????????????????????
     ?         ?
        ?       ?
?????????????????????         ?????????????????????
?  LOCAL SQL SERVER ?? ONLINE SQL SERVER ?
? (Always Available)???????????   (MonsterASP)    ?
?  ?  Sync   ?       ?
?  - Bookings       ?         ?  - Bookings ?
?  - Payments   ?         ?  - Payments       ?
?  - Messages       ?         ?  - Messages       ?
?  - Rooms          ?     ?  - Rooms          ?
?  - SyncQueue      ?         ? ?
?  - SyncStatus     ?   ?        ?
?????????????????????         ?????????????????????
```

## How Dual-Write Works

### Scenario 1: User is ONLINE
```
User Action ? DualWriteService ? Write to LOCAL ? ? Write to ONLINE ? ? Done!
      ?        ?
   (Transaction 1)  (Transaction 2)
```

### Scenario 2: User is OFFLINE
```
User Action ? DualWriteService ? Write to LOCAL ? ? Queue for Sync ? Done!
          ?        ?
     (Transaction)     (SyncQueue table)
```

### Scenario 3: Back ONLINE (Auto-Sync)
```
Connection Restored ? ConnectivityService ? SyncService ? Push Pending Changes ? Done!
            ?        ?
       (Detects Online)    (Reads SyncQueue, pushes to Online)
```

## Key Components

### 1. DualWriteService
The core service that handles intelligent write operations:

```csharp
// Automatically decides whether to:
// - Write to BOTH databases (when online)
// - Write to LOCAL only + queue (when offline)

await _dualWriteService.ExecuteWriteAsync(
    "Booking",          // Entity type
    "Bookings",         // Table name
    "INSERT",   // Operation type
    async (con, tx) =>  // Local database action
    {
        // Your insert/update logic here
        return entityId;
    });
```

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
Handles the sync queue and push operations:

```csharp
// Queue a change for sync
await _syncService.QueueChangeAsync("Booking", bookingId, "INSERT", "Bookings");

// Manually trigger sync
var result = await _syncService.SyncAllAsync();

// Get pending count
int pending = await _syncService.GetPendingChangesCountAsync();
```

## Setup Instructions

### Step 1: Update Connection Strings

Edit `Hospitality\Database\DbConnection.cs`:

```csharp
// Local database (always available)
public const string Local = "Data Source=YOUR_LOCAL_SERVER\\SQLEXPRESS;...;

// Online database (requires internet)
public const string Online = "Data Source=db32979.public.databaseasp.net;...Password=YOUR_PASSWORD;...";
```

### Step 2: Run Database Setup Script

Execute `Hospitality\Database\SyncSetup.sql` on your **LOCAL** database:

```sql
-- This creates:
-- 1. SyncQueue table (tracks pending changes)
-- 2. SyncStatus table (tracks last sync times)
-- 3. Adds sync_status and last_modified columns to main tables
```

### Step 3: Ensure Online Database Schema Matches

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

## Sync Queue Management

### View Pending Changes
```sql
SELECT * FROM SyncQueue 
WHERE sync_status = 'pending' 
ORDER BY created_at;
```

### Clear Failed Syncs
```sql
UPDATE SyncQueue 
SET sync_status = 'pending', retry_count = 0 
WHERE sync_status = 'failed';
```

### Delete Old Completed
```sql
DELETE FROM SyncQueue 
WHERE sync_status = 'completed' 
AND created_at < DATEADD(day, -7, GETDATE());
```

## Error Handling

### Automatic Retry
Failed syncs are retried up to 5 times automatically.

### View Errors
```sql
SELECT entity_type, entity_id, error_message, retry_count 
FROM SyncQueue 
WHERE sync_status = 'failed';
```

## Files Reference

| File | Purpose |
|------|---------|
| `Services\DualWriteService.cs` | **NEW** - Handles dual-write logic |
| `Services\ConnectivityService.cs` | Monitors network/database connectivity |
| `Services\SyncService.cs` | Handles sync queue and push operations |
| `Database\DbConnection.cs` | Local + Online connection strings |
| `Database\SyncSetup.sql` | Creates sync tracking tables |
| `Components\Shared\SyncStatusIndicator.razor` | UI sync status component |
| `wwwroot\css\sync-status.css` | Sync indicator styling |

## Best Practices

1. **Always use DualWriteService** for write operations
2. **Read from local database** - it's always available and up-to-date
3. **Don't block UI on sync** - sync runs in background
4. **Show offline status** to users so they know their changes will sync
5. **Handle backward compatibility** - keep fallback code for services without DualWriteService

## Troubleshooting

### Sync Not Working
1. Check `ConnectivityService` is registered
2. Verify online connection string is correct
3. Test: `await DbConnection.CanConnectToOnlineAsync()`

### Data Not Appearing Online
1. Check SyncQueue for pending/failed entries
2. Verify table schemas match
3. Check for foreign key issues

### Duplicate Records
The MERGE statements prevent duplicates, but verify primary keys match between databases.

---

**Status:** ? Dual-Write Implementation Complete  
**Build:** ? Successful  
**Features:** Online dual-write, Offline queueing, Auto-sync on reconnection

*Built for InnSight Hospitality CRM*
