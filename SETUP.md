# InnSight Hospitality CRM - Team Setup Guide

## ?? Quick Setup Instructions

### 1. Copy Configuration File

After cloning the repository, create your local configuration:

```powershell
# Windows PowerShell
copy Hospitality\appsettings.example.json Hospitality\appsettings.json

# Or Windows Command Prompt
copy Hospitality\appsettings.example.json Hospitality\appsettings.json

# Mac/Linux
cp Hospitality/appsettings.example.json Hospitality/appsettings.json
```

### 2. Update Database Connection

Open `Hospitality/appsettings.json` and update with YOUR database details:

**Windows Authentication (Recommended):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=HospitalityCRM;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

**SQL Server Authentication:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=HospitalityCRM;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

**Common Server Names:**
- `localhost` - Default SQL Server
- `.\SQLEXPRESS` - SQL Express
- `(localdb)\MSSQLLocalDB` - LocalDB

### 3. Run Database Scripts (In Order)

Open SQL Server Management Studio and run these scripts:

1. ? `Hospitality/Database/CreateDatabase.sql`
2. ? `Hospitality/Database/MessagesSetup.sql`
3. ? `Hospitality/Database/LoyaltyProgramSetup.sql`
4. ? `Hospitality/Database/PaymentsSetup.sql`

### 4. Configure PayMongo (Optional)

For payment testing:
1. Get test keys from https://dashboard.paymongo.com/
2. Update `appsettings.json`:
```json
{
  "PayMongo": {
    "PublicKey": "pk_test_YOUR_KEY",
    "SecretKey": "sk_test_YOUR_KEY",
    "WebhookSecret": "whsec_YOUR_SECRET"
  }
}
```

### 5. Build and Run

```bash
cd Hospitality
dotnet restore
dotnet build
dotnet run
```

## ?? IMPORTANT SECURITY RULES

### ? NEVER Commit:
- `appsettings.json` (contains secrets!)
- `appsettings.Development.json`
- `appsettings.Production.json`
- Any file with real credentials

### ? ALWAYS Verify Before Pushing:
```bash
git status
# If you see appsettings.json listed, DO NOT COMMIT IT!

# To unstage if accidentally added:
git reset HEAD Hospitality/appsettings.json
```

## ?? Features

- **Admin Portal**: Dashboard, Rooms, Employees, Reports, Analytics
- **Client Portal**: Booking, Loyalty Program, Messages, Receipts
- **Modern UI**: Responsive design with smart pagination
- **Payment Integration**: PayMongo integration ready
- **Loyalty System**: Points and rewards tracking
- **Messaging**: Client-admin communication

## ?? Key Folders

- `Components/Pages/` - All UI pages
- `Database/` - SQL setup scripts
- `Guide/` - Feature documentation
- `Services/` - Business logic
- `wwwroot/css/` - Stylesheets

## ?? Common Issues

**"Cannot connect to SQL Server"**
- Check if SQL Server is running
- Verify server name in connection string
- Try different server name formats

**"Database does not exist"**
- Run all SQL scripts in the correct order
- Check you're connected to the right server

**"appsettings.json not found"**
- Copy from `appsettings.example.json`
- Make sure it's in `Hospitality/` folder

## ?? Need Help?

Check these files:
- `Guide/MODERN_UI_REDESIGN.md` - UI documentation
- `Guide/BOOKING_PAYMENT_SYSTEM.md` - Booking system
- `QUICK_FIX_GUIDE.md` - Common fixes

---

**Project**: IT 13 - Hospitality CRM  
**Framework**: .NET 9 MAUI + Blazor  
**Database**: SQL Server  
**Last Updated**: December 2024
