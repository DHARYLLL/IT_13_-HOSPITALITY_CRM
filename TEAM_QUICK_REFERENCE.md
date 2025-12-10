# ?? Team Quick Reference Card

## Setup in 3 Steps

### 1?? Get the Code
```bash
git clone https://github.com/DHARYLLL/IT_13_-HOSPITALITY_CRM.git
cd IT_13_-HOSPITALITY_CRM
```

### 2?? Run Setup Script
```powershell
.\setup.ps1
```
The script will:
- ? Create `appsettings.json` from example
- ? Prompt for your database details
- ? Update connection string automatically
- ? Restore NuGet packages

### 3?? Setup Database
Open SQL Server Management Studio (SSMS) and run **in order**:
1. `Database/CreateDatabase.sql`
2. `Database/MessagesSetup.sql`
3. `Database/LoyaltyProgramSetup.sql`
4. `Database/PaymentsSetup.sql`

## ?? Run the Project

**Visual Studio:**
- Open `Hospitality.sln`
- Press `F5`

**Command Line:**
```bash
cd Hospitality
dotnet run
```

## ? Quick Commands

```bash
# Pull latest changes
git pull origin main

# Check what you're about to commit
git status

# Build project
dotnet build

# Clean and rebuild
dotnet clean && dotnet build

# Restore packages
dotnet restore
```

## ?? Security Checklist

Before every commit:
```bash
git status
```

**If you see `appsettings.json` listed:**
```bash
# Remove from staging
git reset HEAD Hospitality/appsettings.json
```

**NEVER commit:**
- ? `appsettings.json`
- ? `appsettings.*.json` (except `.example.json`)
- ? Any file with passwords or API keys

## ?? Common Connection Strings

**Local SQL Server:**
```
Server=localhost;Database=HospitalityCRM;Trusted_Connection=True;TrustServerCertificate=True;
```

**SQL Express:**
```
Server=.\SQLEXPRESS;Database=HospitalityCRM;Trusted_Connection=True;TrustServerCertificate=True;
```

**LocalDB:**
```
Server=(localdb)\MSSQLLocalDB;Database=HospitalityCRM;Trusted_Connection=True;TrustServerCertificate=True;
```

**SQL Authentication:**
```
Server=localhost;Database=HospitalityCRM;User Id=YOUR_USER;Password=YOUR_PASS;TrustServerCertificate=True;
```

## ?? Quick Fixes

**Problem: "Cannot connect to SQL Server"**
```
Solution:
1. Check SQL Server is running
2. Verify server name in connection string
3. Try different server name format
```

**Problem: "Database does not exist"**
```
Solution:
1. Run CreateDatabase.sql in SSMS
2. Run other setup scripts in order
```

**Problem: "Build failed"**
```bash
dotnet clean
dotnet restore
dotnet build
```

**Problem: "Accidentally committed appsettings.json"**
```bash
git rm --cached Hospitality/appsettings.json
git commit -m "Remove appsettings.json"
git push
# Then rotate all credentials!
```

## ?? Documentation

| Topic | File |
|-------|------|
| Setup Guide | `SETUP.md` |
| Modern UI | `Guide/MODERN_UI_REDESIGN.md` |
| Booking System | `Guide/BOOKING_PAYMENT_SYSTEM.md` |
| Messages | `Guide/Message/` |
| Loyalty Program | `Guide/Loyalty/` |
| Quick Fixes | `QUICK_FIX_GUIDE.md` |

## ?? Project Structure

```
Hospitality/
??? Components/Pages/     # All UI pages
??? Services/       # Business logic
??? Database/             # SQL scripts
??? Models/    # Data models
??? wwwroot/css/       # Stylesheets
??? appsettings.json   # ?? DO NOT COMMIT
```

## ?? Admin Login (After Setup)

**Default Admin:**
- Email: `admin@innsight.com`
- Password: `admin123`

**Default Staff:**
- Email: `staff@innsight.com`
- Password: `staff123`

**Test Client:**
- Email: `test@client.com`
- Password: `test123`

_(Create these users after running database scripts)_

## ?? Pro Tips

1. **Always pull before starting work:**
   ```bash
   git pull origin main
 ```

2. **Use branches for features:**
   ```bash
   git checkout -b feature/your-feature
   ```

3. **Check before committing:**
   ```bash
   git status
   git diff
   ```

4. **Keep your database in sync:**
   - Pull latest code
   - Check for new SQL scripts in `Database/`
   - Run any new scripts in SSMS

5. **Test locally before pushing:**
   - Build and run
   - Test your changes
   - Check console for errors

## ?? Need Help?

1. Check `SETUP.md` for detailed instructions
2. Look in `Guide/` folder for feature docs
3. Check `QUICK_FIX_GUIDE.md` for common issues
4. Ask team on Discord/Teams
5. Check Git commit history for examples

---

**Remember:** NEVER commit `appsettings.json`! ??

**Happy Coding! ??**
