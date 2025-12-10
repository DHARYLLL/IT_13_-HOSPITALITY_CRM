# ?? ONE-CLICK SYNC FIX

Your sync is showing errors because the app is running **old compiled code** (before the bug fix).

---

## **? EASIEST SOLUTION - Double-Click This File:**

### **Windows:**
```
FixSyncAndRun.bat
```

**Location:** `C:\Users\dhary\source\repos\Hospitality\FixSyncAndRun.bat`

**What it does:**
1. ? Stops any running instances of your app
2. ? Cleans old compiled files
3. ? Rebuilds with the bug fix
4. ? Syncs all pending records (bookings #3, #4, etc.)
5. ? Starts your app with the fixed code

**Just double-click and wait!**

---

## **Alternative: PowerShell Version**

### **Right-click on this file and select "Run with PowerShell":**
```
FixSyncAndRun.ps1
```

**Location:** `C:\Users\dhary\source\repos\Hospitality\FixSyncAndRun.ps1`

---

## **Manual Method (If you prefer):**

### **Open PowerShell in: `C:\Users\dhary\source\repos\Hospitality`**

```powershell
# Navigate to project
cd C:\Users\dhary\source\repos\Hospitality

# Clean
dotnet clean

# Rebuild
dotnet build

# Sync pending
cd Hospitality\Database
.\ForceSyncNow.ps1

# Run app
cd ..\..
dotnet run
```

---

## **What You'll See:**

### **Console Output:**
```
========================================
 HOSPITALITY CRM - COMPLETE SYNC FIX
========================================

[1/5] Stopping any running instances...
[2/5] Cleaning old build files...
[3/5] Rebuilding with the bug fix...

Build successful! ?

[4/5] Syncing pending records to online database...

========================================
 FORCE IMMEDIATE SYNC
========================================

Testing connectivity... LOCAL: OK
      ONLINE: OK

Finding pending bookings...
Found 3 pending booking(s)

Syncing booking #3... SYNCED ?
Syncing booking #4... SYNCED ?
Syncing booking #X... SYNCED ?

========================================
 SYNC COMPLETE
========================================

Successfully synced: 3 booking(s)
Errors: 0

[5/5] Starting the app...
```

### **Your Dashboard Will Show:**
- **"?? Online"** (Green indicator)
- **"? All synced"** (No orange badge)
- **NO red error message**

---

## **Why This Happens:**

Your issue:
```
1. You run the app
   ?
2. App compiles and starts
   ?
3. We fix the bug in SyncService.cs
   ?
4. App is STILL RUNNING with old code ?
   ?
5. Sync keeps failing with old bug
```

The solution:
```
1. STOP the app
   ?
2. Clean old compiled files
   ?
3. Rebuild with fixed code ?
   ?
4. Force sync pending records
   ?
5. Run app with NEW code ?
```

---

## **Verify It Worked:**

### **Check Local Database:**
```sql
-- In SSMS, connect to: LAPTOP-UE341BKJ\SQLEXPRESS
-- Database: CRM

SELECT booking_id, sync_status 
FROM Bookings 
ORDER BY booking_id;
```

**Expected:**
```
booking_id | sync_status
-----------|------------
    1      | synced
    2      | synced
    3      | synced  ? Was pending!
    4      | synced  ? Was pending!
```

### **Check Online Database:**
```sql
-- In SSMS, connect to: db32979.public.databaseasp.net
-- Database: db32979
-- Username: db32979
-- Password: 8c=Ha?Z9!G3z

SELECT * FROM Bookings ORDER BY booking_id DESC;
```

**You should see bookings #3 and #4!**

---

## **Files Created:**

| File | Location | What It Does |
|------|----------|-------------|
| `FixSyncAndRun.bat` | Project root | **Double-click this!** (Windows batch file) |
| `FixSyncAndRun.ps1` | Project root | PowerShell version (more detailed output) |
| `ForceSyncNow.ps1` | `Hospitality\Database\` | Syncs pending records (called by above) |

---

## **Quick Start:**

### **Option 1: Batch File (Simplest)**
1. Navigate to: `C:\Users\dhary\source\repos\Hospitality`
2. Double-click: `FixSyncAndRun.bat`
3. Wait for it to complete
4. Your app will start with the fix!

### **Option 2: PowerShell**
1. Navigate to: `C:\Users\dhary\source\repos\Hospitality`
2. Right-click: `FixSyncAndRun.ps1`
3. Select: "Run with PowerShell"
4. Wait for it to complete

---

## **? Troubleshooting:**

### **"Cannot find FixSyncAndRun.bat"**

Make sure you're in the project root:
```
C:\Users\dhary\source\repos\Hospitality\
```

The file should be right there next to `Hospitality.sln`.

### **"Access Denied" or "Script Execution Blocked"**

For PowerShell, run this first:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then try again.

### **Still showing errors after running?**

1. Make sure the app fully stopped before rebuild
2. Check the console output for any error messages
3. Verify both databases are accessible
4. Try running the sync script manually:
   ```powershell
   cd Hospitality\Database
   .\ForceSyncNow.ps1
   ```

---

## **Summary:**

| Current State | After Running Fix |
|---------------|-------------------|
| ? Sync error showing | ? No sync errors |
| ? "3 pending" badge | ? "All synced" |
| ? Red error toast | ? Green check |
| ? Old compiled code | ? Fixed code running |

---

## **The Bug That Was Fixed:**

In `Services/SyncService.cs` line 249:

```csharp
// ? BEFORE (Wrong):
INSERT (..., [check-out-date], ...)  ? Hyphen!

// ? AFTER (Fixed):
INSERT (..., [check-out_date], ...)  ? Underscore!
```

This typo caused all booking syncs to fail silently. The fix is already in the code, but you need to rebuild to use it!

---

**Just double-click `FixSyncAndRun.bat` and you're done!** ??

---

**Status:** ? **ONE-CLICK FIX READY**  
**Action Required:** Double-click `FixSyncAndRun.bat`  
**Time:** ~2 minutes (includes rebuild + sync + app start)

*InnSight Hospitality CRM - Sync System*
