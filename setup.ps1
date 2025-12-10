# InnSight Hospitality CRM Setup Script
# PowerShell script to help team members set up the project quickly

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " InnSight Hospitality CRM - Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "Hospitality")) {
    Write-Host "? Error: Please run this script from the repository root directory" -ForegroundColor Red
    Write-Host "   (The folder containing the Hospitality folder)" -ForegroundColor Yellow
    exit 1
}

# Step 1: Check if appsettings.json already exists
Write-Host "Step 1: Checking configuration files..." -ForegroundColor Yellow

if (Test-Path "Hospitality/appsettings.json") {
    Write-Host "? appsettings.json already exists" -ForegroundColor Green
    $overwrite = Read-Host "   Do you want to reset it from example? (y/N)"
    if ($overwrite -eq "y" -or $overwrite -eq "Y") {
        Copy-Item "Hospitality/appsettings.example.json" "Hospitality/appsettings.json" -Force
        Write-Host "? Reset appsettings.json from example" -ForegroundColor Green
    } else {
   Write-Host "   Skipping..." -ForegroundColor Gray
    }
} else {
    if (Test-Path "Hospitality/appsettings.example.json") {
        Copy-Item "Hospitality/appsettings.example.json" "Hospitality/appsettings.json"
        Write-Host "? Created appsettings.json from example" -ForegroundColor Green
    } else {
        Write-Host "? Error: appsettings.example.json not found!" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# Step 2: Prompt for database configuration
Write-Host "Step 2: Database Configuration" -ForegroundColor Yellow
Write-Host "   Please enter your SQL Server details:" -ForegroundColor White
Write-Host ""

$serverName = Read-Host "   Server name (press Enter for 'localhost')"
if ([string]::IsNullOrWhiteSpace($serverName)) {
    $serverName = "localhost"
}

$dbName = Read-Host "   Database name (press Enter for 'HospitalityCRM')"
if ([string]::IsNullOrWhiteSpace($dbName)) {
    $dbName = "HospitalityCRM"
}

Write-Host ""
Write-Host "   Authentication Method:" -ForegroundColor White
Write-Host "   1. Windows Authentication (Recommended)" -ForegroundColor White
Write-Host "   2. SQL Server Authentication" -ForegroundColor White
$authChoice = Read-Host "   Select (1 or 2)"

if ($authChoice -eq "2") {
    $username = Read-Host "   SQL Username"
  $password = Read-Host "   SQL Password" -AsSecureString
    $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
   [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    )
    $connectionString = "Server=$serverName;Database=$dbName;User Id=$username;Password=$passwordPlain;TrustServerCertificate=True;"
} else {
    $connectionString = "Server=$serverName;Database=$dbName;Trusted_Connection=True;TrustServerCertificate=True;"
}

# Update appsettings.json
try {
    $config = Get-Content "Hospitality/appsettings.json" -Raw | ConvertFrom-Json
    if ($config.PSObject.Properties.Name -contains "ConnectionStrings") {
        $config.ConnectionStrings.DefaultConnection = $connectionString
    } else {
        $config | Add-Member -MemberType NoteProperty -Name "ConnectionStrings" -Value @{
  DefaultConnection = $connectionString
        } -Force
    }
    $config | ConvertTo-Json -Depth 10 | Set-Content "Hospitality/appsettings.json"
    Write-Host "? Updated connection string in appsettings.json" -ForegroundColor Green
} catch {
    Write-Host "? Error updating appsettings.json: $_" -ForegroundColor Red
    Write-Host "   Please update it manually" -ForegroundColor Yellow
}

Write-Host ""

# Step 3: Check for .NET SDK
Write-Host "Step 3: Checking prerequisites..." -ForegroundColor Yellow

try {
 $dotnetVersion = dotnet --version
    Write-Host "? .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
 Write-Host "? .NET SDK not found!" -ForegroundColor Red
 Write-Host "   Please install .NET 9 SDK from: https://dot.net" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 4: Restore packages
Write-Host "Step 4: Restoring NuGet packages..." -ForegroundColor Yellow

Push-Location "Hospitality"
try {
    dotnet restore 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? NuGet packages restored successfully" -ForegroundColor Green
    } else {
     Write-Host "??  Warning: Some packages may not have restored correctly" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? Error restoring packages: $_" -ForegroundColor Red
}
Pop-Location

Write-Host ""

# Step 5: Summary and next steps
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "?? Next Steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Open SQL Server Management Studio (SSMS)" -ForegroundColor White
Write-Host "   Connect to: $serverName" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Run these SQL scripts IN ORDER:" -ForegroundColor White
Write-Host "   ? Database/CreateDatabase.sql" -ForegroundColor Gray
Write-Host "   ? Database/SyncSetup.sql (adds sync tracking)" -ForegroundColor Gray
Write-Host "   ? Database/MessagesSetup.sql" -ForegroundColor Gray
Write-Host "   ? Database/LoyaltyProgramSetup.sql" -ForegroundColor Gray
Write-Host "   ? Database/PaymentsSetup.sql" -ForegroundColor Gray
Write-Host ""
Write-Host "3. (Optional) Configure PayMongo API keys in appsettings.json" -ForegroundColor White
Write-Host "   Get test keys from: https://dashboard.paymongo.com/" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Build and run the project:" -ForegroundColor White
Write-Host "   dotnet build" -ForegroundColor Gray
Write-Host "   dotnet run --project Hospitality" -ForegroundColor Gray
Write-Host ""
Write-Host "   Or open Hospitality.sln in Visual Studio and press F5" -ForegroundColor Gray
Write-Host ""

Write-Host "??  IMPORTANT:" -ForegroundColor Red
Write-Host "   Never commit your appsettings.json file to Git!" -ForegroundColor Yellow
Write-Host "   It contains sensitive database credentials." -ForegroundColor Yellow
Write-Host ""

Write-Host "?? Documentation:" -ForegroundColor Cyan
Write-Host "   - SETUP.md - Detailed setup guide" -ForegroundColor Gray
Write-Host "   - Guide/ folder - Feature documentation" -ForegroundColor Gray
Write-Host "   - QUICK_FIX_GUIDE.md - Common issues and fixes" -ForegroundColor Gray
Write-Host ""

Write-Host "Happy coding! ??" -ForegroundColor Green
Write-Host ""
