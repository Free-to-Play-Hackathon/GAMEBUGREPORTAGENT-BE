# Game Bug Repro Agent - Phase 1 Demo Script
# Ensure the API is running before running this script (e.g. dotnet run --project src/GameBug.Api)

$baseUrl = "http://localhost:5000"
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Game Bug Repro Agent - Phase 1 Automated Demo" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Check if server is running
Write-Host "`n[1/5] Checking API Health..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health/live" -Method Get
    Write-Host "API is Live! Status: $($health.Status)" -ForegroundColor Green
} catch {
    Write-Host "Error: Cannot connect to API at $baseUrl." -ForegroundColor Red
    Write-Host "Please start the API by running: dotnet run --project src/GameBug.Api" -ForegroundColor Yellow
    Exit
}

# Create dummy attachment files
$tempLogPath = "$PSScriptRoot/demo_log.txt"
$tempImagePath = "$PSScriptRoot/demo_screenshot.png"
"This is a dummy log attachment file." | Out-File -FilePath $tempLogPath -Encoding utf8
# PNG Magic Bytes signature: 89 50 4E 47 0D 0A 1A 0A
[byte[]]$pngSignature = 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
[System.IO.File]::WriteAllBytes($tempImagePath, $pngSignature)

# Generate a random idempotency key
$idempotencyKey = "demo-key-" + (New-Guid).Guid.Substring(0, 8)
Write-Host "`nUsing Idempotency Key: $idempotencyKey" -ForegroundColor DarkCyan

# Helper to build multipart boundary
function Send-BugReport {
    param(
        [string]$Description,
        [string]$IdempotencyKey,
        [string]$ImagePath,
        [string]$LogPath
    )

    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    
    $bodyLines = @()
    # Description
    $bodyLines += "--$boundary"
    $bodyLines += 'Content-Disposition: form-data; name="description"'
    $bodyLines += ""
    $bodyLines += $Description

    # Platform
    $bodyLines += "--$boundary"
    $bodyLines += 'Content-Disposition: form-data; name="platform"'
    $bodyLines += ""
    $bodyLines += "PC"

    # Image file
    if ($ImagePath -and (Test-Path $ImagePath)) {
        $imgBytes = [System.IO.File]::ReadAllBytes($ImagePath)
        $imgName = [System.IO.Path]::GetFileName($ImagePath)
        $bodyLines += "--$boundary"
        $bodyLines += "Content-Disposition: form-data; name=`"attachments``; filename=`"$imgName`""
        $bodyLines += "Content-Type: image/png"
        $bodyLines += ""
        $bodyLines += [System.Text.Encoding]::GetEncoding('iso-8859-1').GetString($imgBytes)
    }

    # Log file
    if ($LogPath -and (Test-Path $LogPath)) {
        $logBytes = [System.IO.File]::ReadAllBytes($LogPath)
        $logName = [System.IO.Path]::GetFileName($LogPath)
        $bodyLines += "--$boundary"
        $bodyLines += "Content-Disposition: form-data; name=`"attachments``; filename=`"$logName`""
        $bodyLines += "Content-Type: text/plain"
        $bodyLines += ""
        $bodyLines += [System.Text.Encoding]::GetEncoding('iso-8859-1').GetString($logBytes)
    }

    $bodyLines += "--$boundary--"
    $bodyText = $bodyLines -join $LF
    $bodyBytes = [System.Text.Encoding]::GetEncoding('iso-8859-1').GetBytes($bodyText)

    $headers = @{
        "Idempotency-Key" = $IdempotencyKey
    }

    try {
        $response = Invoke-WebRequest -Uri "$baseUrl/api/v1/bug-reports" `
                                      -Method Post `
                                      -Headers $headers `
                                      -ContentType "multipart/form-data; boundary=$boundary" `
                                      -Body $bodyBytes `
                                      -SkipHttpErrorCheck
        return $response
    } catch {
        return $_.Exception.Response
    }
}

# 2. Submit initial report
Write-Host "`n[2/5] Submitting initial bug report..." -ForegroundColor Yellow
$res1 = Send-BugReport -Description "This is a demo bug report describing a graphics glitch." `
                       -IdempotencyKey $idempotencyKey `
                       -ImagePath $tempImagePath `
                       -LogPath $tempLogPath

Write-Host "HTTP Status: $($res1.StatusCode)" -ForegroundColor Green
Write-Host "Response Body:" -ForegroundColor Gray
Write-Host $res1.Content -ForegroundColor Gray
$report1 = $res1.Content | ConvertFrom-Json

# 3. Replay exact same report (Idempotency Replay)
Write-Host "`n[3/5] Re-submitting the exact same request (expecting Idempotency Replay)..." -ForegroundColor Yellow
$res2 = Send-BugReport -Description "This is a demo bug report describing a graphics glitch." `
                       -IdempotencyKey $idempotencyKey `
                       -ImagePath $tempImagePath `
                       -LogPath $tempLogPath

Write-Host "HTTP Status: $($res2.StatusCode)" -ForegroundColor Green
Write-Host "Response Body:" -ForegroundColor Gray
Write-Host $res2.Content -ForegroundColor Gray
$report2 = $res2.Content | ConvertFrom-Json

if ($report1.reportId -eq $report2.reportId) {
    Write-Host "SUCCESS: Both requests returned the exact same Report ID ($($report1.reportId)). Idempotency replayed correctly!" -ForegroundColor Green
} else {
    Write-Host "FAILURE: Report IDs do not match." -ForegroundColor Red
}

# 4. Modify description but reuse the same Idempotency Key (Conflict check)
Write-Host "`n[4/5] Submitting with modified payload but reusing the same key (expecting 409 Conflict)..." -ForegroundColor Yellow
$res3 = Send-BugReport -Description "MODIFIED DESCRIPTION - graphics glitch." `
                       -IdempotencyKey $idempotencyKey `
                       -ImagePath $tempImagePath `
                       -LogPath $tempLogPath

Write-Host "HTTP Status: $($res3.StatusCode)" -ForegroundColor ( ($res3.StatusCode -eq 409) ? "Green" : "Red" )
Write-Host "Response Body:" -ForegroundColor Gray
Write-Host $res3.Content -ForegroundColor Gray

# 5. Retrieve the created report
Write-Host "`n[5/5] Retrieving the created report details..." -ForegroundColor Yellow
try {
    $reportDetails = Invoke-RestMethod -Uri "$baseUrl/api/v1/bug-reports/$($report1.reportId)" -Method Get
    Write-Host "Successfully retrieved report detail!" -ForegroundColor Green
    Write-Host "Report Status: $($reportDetails.status)" -ForegroundColor Green
    Write-Host "Attachments Count: $($reportDetails.attachments.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error retrieving report details: $_" -ForegroundColor Red
}

# Cleanup temp files
Remove-Item $tempLogPath -ErrorAction SilentlyContinue
Remove-Item $tempImagePath -ErrorAction SilentlyContinue

Write-Host "`nDemo completed!" -ForegroundColor Cyan
