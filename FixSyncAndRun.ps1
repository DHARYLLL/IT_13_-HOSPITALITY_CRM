# ========================================
#  COMPLETE SYNC FIX - ONE CLICK SOLUTION
# ========================================

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " HOSPITALITY CRM - COMPLETE SYNC FIX" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Make sure we're in the right directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Step 1: Stop any running instances
Write-Host "[1/5] Stopping any running instances..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like "*Hospitality*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Step 2: Clean old build
Write-Host "[2/5] Cleaning old build files..." -ForegroundColor Yellow
$cleanResult = dotnet clean --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Clean failed!" -ForegroundColor Red
  Write-Host $cleanResult -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 3: Rebuild with fix
Write-Host "[3/5] Rebuilding with the bug fix..." -ForegroundColor Yellow
$buildResult = dotnet build --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "`nBuild successful! ?`n" -ForegroundColor Green

# Step 4: Force sync pending records
Write-Host "[4/5] Syncing pending records to online database...`n" -ForegroundColor Yellow

# Run the sync script
& ".\Hospitality\Database\ForceSyncNow.ps1"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nWARNING: Sync script had errors. Check the output above." -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " SYNC FIX COMPLETE" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Your pending records have been synced!" -ForegroundColor Green
Write-Host "`n[5/5] Starting the app...`n" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop the app when you're done testing.`n" -ForegroundColor Gray

# Step 5: Run the app
dotnet run --project Hospitality\Hospitality.csproj
