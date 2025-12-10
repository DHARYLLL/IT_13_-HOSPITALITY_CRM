# Force Immediate Sync
# Run this to sync pending records RIGHT NOW

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " FORCE IMMEDIATE SYNC" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Connection strings
$localConnectionString = "Data Source=LAPTOP-UE341BKJ\SQLEXPRESS;Initial Catalog=CRM;Integrated Security=True;Connect Timeout=5;Encrypt=True;Trust Server Certificate=True"
$onlineConnectionString = "Data Source=db32979.public.databaseasp.net;Initial Catalog=db32979;User ID=db32979;Password=8c=Ha?Z9!G3z;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True"

# Test connectivity first
Write-Host "Testing connectivity..." -NoNewline
try {
  $localCon = New-Object System.Data.SqlClient.SqlConnection($localConnectionString)
    $localCon.Open()
    Write-Host " LOCAL: OK" -ForegroundColor Green
    
    $onlineCon = New-Object System.Data.SqlClient.SqlConnection($onlineConnectionString)
    $onlineCon.Open()
    Write-Host "       ONLINE: OK" -ForegroundColor Green
} catch {
    Write-Host " FAILED" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit
}

Write-Host "`nFinding pending bookings..." -ForegroundColor Yellow

# Get pending bookings
$query = "SELECT booking_id, client_id, [check-in_date], [check-out_date], person_count, booking_status, client_request FROM Bookings WHERE sync_status = 'pending'"
$cmd = $localCon.CreateCommand()
$cmd.CommandText = $query
$adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
$dataSet = New-Object System.Data.DataSet
$adapter.Fill($dataSet) | Out-Null

$pendingCount = $dataSet.Tables[0].Rows.Count

if ($pendingCount -eq 0) {
    Write-Host "No pending bookings found." -ForegroundColor Green
    $localCon.Close()
    $onlineCon.Close()
    exit
}

Write-Host "Found $pendingCount pending booking(s)" -ForegroundColor Yellow
Write-Host ""

$synced = 0
$errors = 0

foreach ($row in $dataSet.Tables[0].Rows) {
    $bookingId = $row["booking_id"]
    $clientId = $row["client_id"]
    $checkInDate = $row["check-in_date"]
    $checkOutDate = $row["check-out_date"]
    $personCount = $row["person_count"]
    $bookingStatus = if ($row.IsNull("booking_status")) { [DBNull]::Value } else { $row["booking_status"] }
    $clientRequest = if ($row.IsNull("client_request")) { [DBNull]::Value } else { $row["client_request"] }
    
    Write-Host "Syncing booking #$bookingId..." -NoNewline
    
    try {
        # Enable IDENTITY_INSERT
        $enableCmd = $onlineCon.CreateCommand()
        $enableCmd.CommandText = "SET IDENTITY_INSERT Bookings ON;"
     $enableCmd.ExecuteNonQuery() | Out-Null
        
        # MERGE statement
      $mergeSql = @"
MERGE INTO Bookings AS target
USING (SELECT @booking_id AS booking_id) AS source
ON target.booking_id = source.booking_id
WHEN MATCHED THEN
    UPDATE SET 
        client_id = @client_id,
        [check-in_date] = @check_in_date,
        [check-out_date] = @check_out_date,
        person_count = @person_count,
      booking_status = @booking_status,
        client_request = @client_request
WHEN NOT MATCHED THEN
    INSERT (booking_id, client_id, [check-in_date], [check-out_date], person_count, booking_status, client_request)
    VALUES (@booking_id, @client_id, @check_in_date, @check_out_date, @person_count, @booking_status, @client_request);
"@
        
        $mergeCmd = $onlineCon.CreateCommand()
        $mergeCmd.CommandText = $mergeSql
     $mergeCmd.Parameters.AddWithValue("@booking_id", $bookingId) | Out-Null
        $mergeCmd.Parameters.AddWithValue("@client_id", $clientId) | Out-Null
  $mergeCmd.Parameters.AddWithValue("@check_in_date", $checkInDate) | Out-Null
        $mergeCmd.Parameters.AddWithValue("@check_out_date", $checkOutDate) | Out-Null
   $mergeCmd.Parameters.AddWithValue("@person_count", $personCount) | Out-Null
     $mergeCmd.Parameters.AddWithValue("@booking_status", $bookingStatus) | Out-Null
        $mergeCmd.Parameters.AddWithValue("@client_request", $clientRequest) | Out-Null
        
   $mergeCmd.ExecuteNonQuery() | Out-Null
      
        # Disable IDENTITY_INSERT
  $disableCmd = $onlineCon.CreateCommand()
        $disableCmd.CommandText = "SET IDENTITY_INSERT Bookings OFF;"
        $disableCmd.ExecuteNonQuery() | Out-Null
    
    # Mark as synced in local database
    $updateCmd = $localCon.CreateCommand()
        $updateCmd.CommandText = "UPDATE Bookings SET sync_status = 'synced', last_modified = GETDATE() WHERE booking_id = @id"
        $updateCmd.Parameters.AddWithValue("@id", $bookingId) | Out-Null
   $updateCmd.ExecuteNonQuery() | Out-Null
   
        Write-Host " SYNCED ?" -ForegroundColor Green
        $synced++
    } catch {
     Write-Host " FAILED ?" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        $errors++
    }
}

$localCon.Close()
$onlineCon.Close()

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " SYNC COMPLETE" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Successfully synced: $synced booking(s)" -ForegroundColor Green
if ($errors -gt 0) {
    Write-Host "Errors: $errors booking(s)" -ForegroundColor Red
}

Write-Host "`nVerify in online database:" -ForegroundColor Yellow
Write-Host "  SELECT * FROM Bookings ORDER BY booking_id DESC;" -ForegroundColor Cyan

Write-Host ""
