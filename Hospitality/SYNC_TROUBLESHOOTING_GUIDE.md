# Why Your Data Isn't Syncing to Online Database

## **The Problem**

Your local database has records with `sync_status = 'pending'`, but they haven't been pushed to your MonsterASP online database yet.

### **Screenshot Analysis:**
- **booking_id 1, 2**: `sync_status = 'synced'` ? (Already in online DB)
- **booking_id 3**: `sync_status = 'pending'` ? (Waiting to sync)
- **booking_id 4**: `sync_status = 'pending'` ? (Waiting to sync)

## **Why This Happens**

Your system uses **offline-first** architecture with **dual-write** support:

1. **When ONLINE**: Data writes to BOTH local and online databases simultaneously
2. **When OFFLINE**: Data writes to local only, marked as `'pending'`
3. **Auto-sync triggers**: Only when connectivity changes from offline ? online

### **The Issue:**
If you're already online, auto-sync won't trigger automatically. You need to manually initiate the sync.

---

## **Solution 1: Use the Admin Dashboard Sync Button (EASIEST)**

I've just added a database sync button to your AdminDashboard!

### **Steps:**

1. **Open your app** and navigate to `/admin/dashboard`

2. **Look for the database icon** in the header (top-right area)
   - ?? Online indicator if connected
   - ?? Offline indicator if not connected
   - Red badge shows number of pending records

3. **Click the database button** to manually sync

4. **Wait for confirmation** toast message:
   - ? "Successfully synced X records" (Green)
   - ?? Error message if sync fails (Red)

### **What It Looks Like:**

```
???????????????????????????????????????????????????
?  Dashboard     ?? Date  | ??? [4] | ??  ?
?  ?       ?
?       Sync button with      ?
? 4 pending records     ?
???????????????????????????????????????????????????
```

---

## **Solution 2: Run PowerShell Script (QUICK CHECK)**

### **Check Pending Status:**

```powershell
cd Hospitality\Database
.\TestSync.ps1
```

**This will:**
- ? Test local database connection
- ? Test online database connection
- ? Show pending records count per table
- ? Display recommendations

**Example Output:**
```
========================================
 DATABASE SYNC STATUS CHECK
========================================

Testing LOCAL database connection... OK

Pending Sync Records:
  Bookings : 2 pending
  Payments : 0 pending
  Messages : 0 pending
  Rooms : 0 pending

Total Pending: 2 records

Testing ONLINE database connection... OK
  Server: db32979.public.databaseasp.net
  Database: db32979

========================================
 RECOMMENDATIONS
========================================

  You have 2 records waiting to sync!

  To sync manually:
  1. Open the Admin Dashboard in your app
  2. Click the database sync button (??? with badge)
  3. Wait for 'Successfully synced' message
```

---

## **Solution 3: Run SQL Script (DETAILED VIEW)**

### **Check What's Pending:**

```sql
-- Run on LOCAL database
USE CRM;

-- See all pending bookings
SELECT * FROM Bookings WHERE sync_status = 'pending';

-- See all pending records across tables
SELECT 'Bookings' AS Table, COUNT(*) AS Pending FROM Bookings WHERE sync_status = 'pending'
UNION ALL
SELECT 'Payments', COUNT(*) FROM Payments WHERE sync_status = 'pending'
UNION ALL
SELECT 'Messages', COUNT(*) FROM Messages WHERE sync_status = 'pending'
UNION ALL
SELECT 'Rooms', COUNT(*) FROM rooms WHERE sync_status = 'pending';
```

**Or use the provided script:**
```sql
-- In SQL Server Management Studio
-- Open: Hospitality\Database\CheckPendingSync.sql
-- Execute on LOCAL database (CRM)
```

---

## **Solution 4: Programmatically Sync in Code**

### **Option A: Inject SyncService**

```csharp
@inject Hospitality.Services.SyncService SyncService

private async Task ManualSync()
{
    var result = await SyncService.SyncAllAsync();
    
    if (result.Success)
    {
        Console.WriteLine($"? Synced {result.PushedCount} records");
    }
    else
    {
        Console.WriteLine($"? Sync failed: {result.Message}");
    }
}
```

### **Option B: Force Connectivity Refresh**

```csharp
@inject Hospitality.Services.ConnectivityService ConnectivityService

private async Task ForceSync()
{
    // This will check connectivity and trigger auto-sync if online
    bool isOnline = await ConnectivityService.RefreshAndSyncAsync();
    
    if (isOnline)
 {
        Console.WriteLine("? Connected and syncing...");
    }
    else
 {
        Console.WriteLine("? Still offline");
    }
}
```

---

## **Understanding the Sync Flow**

### **Normal Flow (When Everything Works):**

```
1. User creates booking
   ?
2. DualWriteService writes to LOCAL (sync_status = 'pending')
   ?
3. DualWriteService detects ONLINE connection
 ?
4. Writes to ONLINE database immediately
   ?
5. Updates LOCAL (sync_status = 'synced')
   ? Done!
```

### **Offline Flow (When Network Is Down):**

```
1. User creates booking
   ?
2. DualWriteService writes to LOCAL (sync_status = 'pending')
   ?
3. DualWriteService detects OFFLINE
   ?
4. Record stays 'pending'
   ? Waiting for connectivity...
   
Later, when connection restored:
   ?
5. ConnectivityService detects ONLINE
   ?
6. Fires OnlineDbAvailable event
   ?
7. SyncService automatically syncs all 'pending' records
   ?
8. Updates LOCAL (sync_status = 'synced')
   ? Done!
```

### **Your Situation (Already Online but Not Synced):**

```
1. Bookings created (possibly while offline)
   ?
2. Records marked 'pending'
   ?
3. Connection restored but auto-sync didn't trigger
   ?
4. Records still 'pending'
   
Manual intervention needed:
   ?
5. Click sync button in Admin Dashboard
   OR
6. Run ConnectivityService.RefreshAndSyncAsync()
   ?
7. SyncService syncs all 'pending' records
   ?
8. Records now 'synced'
   ? Done!
```

---

## **Troubleshooting**

### **Problem: Sync Button Shows "Offline" but I Have Internet**

**Possible Causes:**
1. Online database connection string is incorrect
2. Firewall blocking MonsterASP server
3. MonsterASP database is temporarily down

**Fix:**
1. Check connection string in `DbConnection.cs`:
   ```csharp
   public const string Online = "Data Source=db32979.public.databaseasp.net;...";
   ```

2. Test connection manually:
   ```csharp
   bool canConnect = await DbConnection.CanConnectToOnlineAsync();
   Console.WriteLine($"Can connect: {canConnect}");
   ```

3. Run PowerShell test:
   ```powershell
   .\TestSync.ps1
   ```

---

### **Problem: Sync Fails with Error**

**Common Errors:**

#### **"Timeout expired"**
- Online database is slow or unreachable
- Increase timeout in connection string:
  ```csharp
  "...Connect Timeout=30;..."  // Increase from 15 to 30
  ```

#### **"Cannot insert duplicate key"**
- Record already exists online but local still shows 'pending'
- Manually mark as synced:
  ```sql
  UPDATE Bookings SET sync_status = 'synced' WHERE booking_id = 3;
  ```

#### **"Foreign key constraint failed"**
- Related record (Client, Room) doesn't exist online yet
- Sync order matters:
  1. Clients first
  2. Rooms first
  3. Then Bookings
- Solution: Run full sync which handles order automatically

---

### **Problem: Sync Button Not Appearing**

**Check:**

1. **Services registered?** Look in `MauiProgram.cs`:
   ```csharp
 builder.Services.AddSingleton<ConnectivityService>();
   builder.Services.AddSingleton<SyncService>();
   ```

2. **Injected in page?** Check `AdminDashboard.razor`:
   ```csharp
   @inject Hospitality.Services.SyncService SyncService
   @inject Hospitality.Services.ConnectivityService ConnectivityService
   ```

3. **CSS loaded?** Check header:
   ```html
   <link rel="stylesheet" href="css/admin-dashboard.css" />
   ```

---

## **Best Practices**

### **? DO:**

1. **Always use DualWriteService** for write operations
2. **Monitor pending count** regularly in dashboard
3. **Sync before important operations** (end of day, backups)
4. **Show offline indicator** to users
5. **Test both online and offline scenarios**

### **? DON'T:**

1. **Don't manually write to online database** - always go through DualWriteService
2. **Don't delete 'pending' records** - they need to sync first
3. **Don't assume online = synced** - check sync_status column
4. **Don't ignore sync errors** - they indicate data inconsistency

---

## **Quick Reference Commands**

### **Check Pending Count (SQL):**
```sql
SELECT COUNT(*) FROM Bookings WHERE sync_status = 'pending';
```

### **Mark All as Synced (SQL - Use with Caution!):**
```sql
UPDATE Bookings SET sync_status = 'synced' WHERE sync_status = 'pending';
```

### **Force Sync (C#):**
```csharp
await SyncService.SyncAllAsync();
```

### **Check Connectivity (C#):**
```csharp
bool isOnline = await ConnectivityService.CheckOnlineDatabaseAsync();
```

### **Get Pending Count (C#):**
```csharp
int pending = await SyncService.GetPendingChangesCountAsync();
```

---

## **Files Created/Modified**

| File | Description |
|------|-------------|
| `Components/Pages/AdminDashboard.razor` | ? Added sync button and status |
| `wwwroot/css/admin-dashboard.css` | ? Added sync button styles |
| `Database/CheckPendingSync.sql` | ? New - Check pending records |
| `Database/TestSync.ps1` | ? New - Test connectivity & pending count |
| `SYNC_TROUBLESHOOTING_GUIDE.md` | ? New - This document |

---

## **Summary**

Your data isn't syncing because:
1. ? Local database has pending records
2. ? System is designed to work offline-first
3. ?? Auto-sync only triggers on connectivity change
4. ?? Manual sync needed when already online

**Immediate Solution:**
1. Open Admin Dashboard (`/admin/dashboard`)
2. Click database sync button (??? icon with red badge)
3. Wait for "Successfully synced" message
4. Verify records now show `sync_status = 'synced'`

**Alternative:**
```powershell
# Run this to check status
.\Hospitality\Database\TestSync.ps1
```

---

**Status:** ? Sync UI Added to Dashboard  
**Build:** ? No Errors  
**Next Step:** Run the app and click the sync button!

*Built for InnSight Hospitality CRM*
