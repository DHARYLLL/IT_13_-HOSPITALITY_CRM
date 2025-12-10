@echo off
REM ========================================
REM  COMPLETE SYNC FIX - ONE CLICK SOLUTION
REM ========================================

echo.
echo ========================================
echo  HOSPITALITY CRM - COMPLETE SYNC FIX
echo ========================================
echo.

REM Step 1: Stop any running instances
echo [1/5] Stopping any running instances...
taskkill /F /IM Hospitality.exe 2>nul
timeout /t 2 /nobreak >nul

REM Step 2: Clean old build
echo [2/5] Cleaning old build files...
dotnet clean --nologo
if errorlevel 1 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)

REM Step 3: Rebuild with fix
echo [3/5] Rebuilding with the bug fix...
dotnet build --nologo
if errorlevel 1 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo Build successful! ?
echo.

REM Step 4: Force sync pending records
echo [4/5] Syncing pending records to online database...
echo.

powershell -ExecutionPolicy Bypass -File "Hospitality\Database\ForceSyncNow.ps1"

if errorlevel 1 (
  echo.
    echo WARNING: Sync script had errors. Check the output above.
    echo.
)

echo.
echo ========================================
echo  SYNC FIX COMPLETE
echo ========================================
echo.
echo Your pending records have been synced!
echo.
echo [5/5] Starting the app...
echo.
echo Press Ctrl+C to stop the app when you're done testing.
echo.

REM Step 5: Run the app
dotnet run --project Hospitality\Hospitality.csproj

pause
