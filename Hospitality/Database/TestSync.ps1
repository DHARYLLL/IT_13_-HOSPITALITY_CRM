# Test Database Sync
# This script checks connectivity and displays pending sync status

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " DATABASE SYNC STATUS CHECK" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Connection strings
$localConnectionString = "Data Source=LAPTOP-UE341BKJ\SQLEXPRESS;Initial Catalog=CRM;Integrated Security=True;Connect Timeout=5;Encrypt=True;Trust Server Certificate=True"
$onlineConnectionString = "Data Source=db32979.public.databaseasp.net;Initial Catalog=db32979;User ID=db32979;Password=8c=Ha?Z9!G3z;Connect Timeout=10;Encrypt=True;Trust Server Certificate=True"

# Test Local Database
Write-Host "Testing LOCAL database connection..." -NoNewline
try {
    $localConnection = New-Object System.Data.SqlClient.SqlConnection($localConnectionString)
    $localConnection.Open()
    Write-Host " OK" -ForegroundColor Green
    
    # Count pending records
    $query = @"
    SELECT 
        'Bookings' AS TableName, 
        COUNT(*) AS PendingCount 
    FROM Bookings 
    WHERE sync_status = 'pending'
    UNION ALL
    SELECT 
        'Payments', 
        COUNT(*) 
    FROM Payments 
    WHERE sync_status = 'pending'
 UNION ALL
    SELECT 
        'Messages', 
        COUNT(*) 
    FROM Messages 
    WHERE sync_status = 'pending'
    UNION ALL
    SELECT 
        'Rooms', 
        COUNT(*) 
    FROM rooms 
    WHERE sync_status = 'pending'
"@
 
    $cmd = $localConnection.CreateCommand()
    $cmd.CommandText = $query
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null
    
    Write-Host "`nPending Sync Records:" -ForegroundColor Yellow
    $totalPending = 0
    foreach ($row in $dataset.Tables[0].Rows) {
        $tableName = $row["TableName"]
      $count = $row["PendingCount"]
        $totalPending += $count
      if ($count -gt 0) {
  Write-Host "  $tableName : $count pending" -ForegroundColor Red
        } else {
    Write-Host "  $tableName : 0 pending" -ForegroundColor Green
     }
    }
    
    Write-Host "`nTotal Pending: $totalPending records" -ForegroundColor $(if ($totalPending -gt 0) { "Red" } else { "Green" })
 
    $localConnection.Close()
} catch {
    Write-Host " FAILED" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host ""

# Test Online Database
Write-Host "Testing ONLINE database connection..." -NoNewline
try {
    $onlineConnection = New-Object System.Data.SqlClient.SqlConnection($onlineConnectionString)
    $onlineConnection.Open()
    Write-Host " OK" -ForegroundColor Green
    Write-Host "  Server: db32979.public.databaseasp.net" -ForegroundColor Gray
    Write-Host "  Database: db32979" -ForegroundColor Gray
    $onlineConnection.Close()
} catch {
    Write-Host " FAILED" -ForegroundColor Red
Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "`nRecords will remain in 'pending' status until online connection is restored." -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " RECOMMENDATIONS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($totalPending -gt 0) {
    Write-Host "  You have $totalPending records waiting to sync!" -ForegroundColor Yellow
    Write-Host "`n  To sync manually:" -ForegroundColor White
    Write-Host "  1. Open the Admin Dashboard in your app" -ForegroundColor White
    Write-Host "  2. Click the database sync button (??? with badge)" -ForegroundColor White
    Write-Host "  3. Wait for 'Successfully synced' message" -ForegroundColor White
    Write-Host "`n  OR run this in your C# code:" -ForegroundColor White
    Write-Host "     await SyncService.SyncAllAsync();" -ForegroundColor Cyan
} else {
    Write-Host "  All data is synced! ?" -ForegroundColor Green
}

Write-Host ""
