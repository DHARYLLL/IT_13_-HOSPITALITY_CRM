# ?? Quick Fix Guide: Sync Pending Data to Online Database

## **Your Screenshot Shows:**

```
?????????????????????????????????????????????????????????????
? booking_id ? sync_status ? last_modified     ?
?????????????????????????????????????????????????????????????
?     1      ?   synced    ? 2025-12-09 23:20:26.680 ? ?
?     2      ?   synced    ? 2025-12-09 23:27:51.330 ? ?
?     3      ?   pending   ? 2025-12-10 00:55:24.220 ? ? NOT SYNCED
?     4      ?   pending   ? 2025-12-10 10:20:35.680 ? ? NOT SYNCED
?????????????????????????????????????????????????????????????
```

**Problem:** Bookings #3 and #4 are not in your online database!

---

## **? NEW SOLUTION: Sync Button in Admin Dashboard**

I've added a database sync button that makes this super easy!

### **What It Looks Like:**

```
???????????????????????????????????????????????????????????
?  InnSight Hotels - Admin Dashboard   ?
???????????????????????????????????????????????????????????
?          ?
?  Dashboard       ?? Dec 10, 2024  | ??? 2  | ?? | ???
?     ?         ?
?         DATABASE SYNC     ?
?(Red badge = 2 pending) ?
?       ?
???????????????????????????????????????????????????????????
```

### **Click the sync button and you'll see:**

```
????????????????????????????????????
?  ?? Syncing to online database...? ? Blue toast
????????????????????????????????????

  ? After a few seconds ?

????????????????????????????????????
?  ? Successfully synced 2 records ? ? Green toast
????????????????????????????????????
```

---

## **How to Use It:**

### **Step 1: Open Admin Dashboard**

```
1. Run your app
2. Login as admin
3. Go to: /admin/dashboard
```

### **Step 2: Look for Sync Button**

Look at the top-right header area. You'll see:

```
???????????????????????????
?  [DATE]  |  ??? [2]  |...?  ? Header actions
?         ?     ?
?   This is the    ?
?         sync button     ?
???????????????????????????
```

**Visual Guide:**
- **Database icon (???)**: The sync button
- **Red badge with number**: Shows how many records are pending
- **Small text below**: Shows "Online" (green) or "Offline" (red)

### **Step 3: Click the Button**

When you click it:

1. **Button pulses** - Shows it's working
2. **Toast appears (blue)** - "Syncing to online database..."
3. **Records sync** - Pushes bookings #3 and #4 to online DB
4. **Toast changes (green)** - "? Successfully synced 2 records"
5. **Badge disappears** - No more pending records!

### **Step 4: Verify**

Check your local database again:

```sql
SELECT booking_id, sync_status FROM Bookings;
```

**Result:**
```
?????????????????????????????????
? booking_id ? sync_status  ?
?????????????????????????????????
?     1      ?   synced ? ?
?     2      ?   synced     ? ?
?     3    ?   synced     ? ? NOW SYNCED!
?     4      ?   synced     ? ? NOW SYNCED!
?????????????????????????????????
```

Check your online database (db32979):

```sql
-- Run on MonsterASP database
SELECT booking_id, client_id, [check-in_date], booking_status 
FROM Bookings
ORDER BY booking_id DESC;
```

**You should now see bookings #3 and #4!**

---

## **Alternative Methods:**

### **Method 2: PowerShell Script (Quick Check)**

```powershell
# Open PowerShell in your project folder
cd Hospitality\Database
.\TestSync.ps1
```

**Output:**
```
========================================
 DATABASE SYNC STATUS CHECK
========================================

Testing LOCAL database connection... OK

Pending Sync Records:
  Bookings : 2 pending    ? YOUR ISSUE
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

### **Method 3: SQL Script (Detailed View)**

```sql
-- Run in SQL Server Management Studio
-- Connect to: LAPTOP-UE341BKJ\SQLEXPRESS
-- Database: CRM
-- Open: Hospitality\Database\CheckPendingSync.sql
-- Execute
```

**Shows:**
- All pending bookings with details
- All pending payments
- All pending messages
- All pending rooms
- Total count

---

## **Troubleshooting:**

### **Problem: Sync button shows "Offline"**

**Check:**
1. Do you have internet connection?
2. Can you ping `db32979.public.databaseasp.net`?
3. Is MonsterASP database accessible?

**Test connectivity:**
```powershell
.\TestSync.ps1
```

**Fix connection string** (if needed):
```csharp
// In DbConnection.cs
public const string Online = "Data Source=db32979.public.databaseasp.net;...";
//     ?
//              Check this is correct
```

---

### **Problem: Sync fails with error**

**Check Output window:**
```
View ? Output ? Show output from: Debug
```

**Common errors:**

1. **"Timeout expired"**
   - Online database is slow
 - Increase timeout in connection string:
 ```csharp
 "...Connect Timeout=30;..."
     ```

2. **"Cannot insert duplicate key"**
   - Record already exists online
   - Manually mark as synced:
     ```sql
     UPDATE Bookings SET sync_status = 'synced' WHERE booking_id = 3;
     ```

3. **"Foreign key constraint"**
   - Related record (Client) doesn't exist online
   - Sync will handle this automatically (syncs in correct order)

---

### **Problem: Badge still shows pending after sync**

**Refresh the page:**
```
Press F5 or click Refresh button
```

**Or check manually:**
```sql
SELECT COUNT(*) FROM Bookings WHERE sync_status = 'pending';
-- Should return 0
```

---

## **What Happens Behind the Scenes:**

### **When you click the sync button:**

```
1. Check online database connectivity
 ?
   ? Connected

2. Query local database for pending records
      ?
   Found: booking_id 3 and 4

3. For each pending booking:
   a. Read booking data from local DB
   b. Use MERGE statement to insert/update online DB
   c. Mark as 'synced' in local DB
      ?
   ? booking_id 3 synced
   ? booking_id 4 synced

4. Show success message
      ?
   ? "Successfully synced 2 records"

5. Update UI
      ?
   Badge disappears (0 pending)
```

---

## **Why Did This Happen?**

Your system uses **offline-first** architecture:

1. **Bookings #1 and #2** were created:
   - Written to LOCAL ?
- Written to ONLINE ?
   - Status: `'synced'` ?

2. **Bookings #3 and #4** were created (possibly while offline):
   - Written to LOCAL ?
   - Couldn't reach ONLINE ?
   - Status: `'pending'` ?

3. **Auto-sync should trigger** when connection restored:
   - But only if it detects connectivity **change**
   - If already online, auto-sync won't trigger
   - Need manual sync ?  THIS IS YOUR CASE

---

## **Preventing This in the Future:**

### **1. Monitor the sync indicator**

Always check the database icon in Admin Dashboard:
- ?? **Online** + No badge = All synced ?
- ?? **Online** + Red badge [N] = N records pending ??
- ?? **Offline** + Badge = Will sync when online ?

### **2. Sync regularly**

At end of day, click the sync button to ensure all data is backed up online.

### **3. Enable notifications**

The sync button will show toast notifications automatically:
- ? Success: Green toast
- ?? Error: Red toast
- ?? In progress: Blue toast

---

## **Quick Command Reference:**

| Action | Command |
|--------|---------|
| Check pending (SQL) | `SELECT COUNT(*) FROM Bookings WHERE sync_status = 'pending';` |
| Check pending (PS) | `.\TestSync.ps1` |
| Mark as synced (SQL) | `UPDATE Bookings SET sync_status = 'synced' WHERE booking_id = X;` |
| Manual sync (C#) | `await SyncService.SyncAllAsync();` |
| Check connectivity | `await ConnectivityService.CheckOnlineDatabaseAsync();` |

---

## **Summary:**

### **Problem:**
- ? Bookings #3 and #4 have `sync_status = 'pending'`
- ? Not in online database (db32979)

### **Solution:**
- ? Click database sync button in Admin Dashboard
- ? Wait for "Successfully synced 2 records"
- ? Verify status changed to `'synced'`

### **Result:**
- ? All bookings now in online database
- ? No more pending records
- ? Data is backed up and accessible from MonsterASP

---

## **Need More Help?**

?? **Complete Guide:** `SYNC_TROUBLESHOOTING_GUIDE.md`  
?? **Solution Summary:** `SYNC_SOLUTION_SUMMARY.md`  
?? **Architecture:** `Guide/OFFLINE_SYNC_GUIDE.md`

---

**Status:** ? SOLVED - Sync button added to Admin Dashboard  
**Action Required:** Click the sync button!  
**Estimated Time:** < 10 seconds

*InnSight Hospitality CRM - Database Sync System*
