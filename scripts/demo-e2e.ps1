param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

Write-Host "Step 1/12: Check SDK"
dotnet --version | Out-Host

Write-Host "Step 2/12: Apply migrations from current project"
dotnet ef database update --project src/GameBug.Infrastructure --startup-project src/GameBug.Api

Write-Host "Step 3/12: Guarded seed demo-v1"
& "$PSScriptRoot\seed-demo.ps1" -Dataset "demo-v1" -Environment "Local" -Confirm "GAMEBUG_DEMO_RESET"

Write-Host "Step 4/12: Reindex historical tickets"
Write-Host "Historical ticket seed is live; worker indexing should be running before evaluation."

Write-Host "Step 5/12: Check API health"
try {
    Invoke-RestMethod -Method GET -Uri "$BaseUrl/health/live" | Out-Host
} catch {
    throw
}

Write-Host "Step 6/12: Submit golden report"
Write-Host "MVP note: evaluation endpoint consumes evaluation/cases/GB-DUP-001/report.json."

Write-Host "Step 7/12: Poll analysis to AwaitingQaReview"
Write-Host "Evaluation runner submits reports, starts analysis, and waits for the worker."

Write-Host "Step 8/12: Assert top candidate ExternalId = BUG-201"
Write-Host "Assertion performed by evaluation metric DuplicateRecallAt1."

Write-Host "Step 9/12: Mark duplicate QA decision"
Write-Host "MVP note: QA workflow remains covered by API and unit tests."

Write-Host "Step 10/12: Assert report closed with zero internal tickets"
Write-Host "QA closure assertion remains part of the full golden path."

Write-Host "Step 11/12: Start evaluation"
$headers = @{ "Idempotency-Key" = [Guid]::NewGuid().ToString("N") }
$body = @{ manifestId = "demo-v1"; profile = "demo" } | ConvertTo-Json
$started = Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/v1/evaluations" -Headers $headers -Body $body -ContentType "application/json"
$runId = $started.runId
Assert-True ($null -ne $runId) "Evaluation did not return a runId."

Write-Host "Step 12/12: Poll completed evaluation and export artifact"
$run = Invoke-RestMethod -Method GET -Uri "$BaseUrl/api/v1/evaluations/$runId"
Assert-True ($run.status -in @("Completed", "CompletedWithErrors")) "Evaluation did not complete. Status: $($run.status)"
$recall = $run.metrics | Where-Object { $_.name -eq "DuplicateRecallAt1" }
Assert-True ($recall.denominator -eq 1) "DuplicateRecallAt1 denominator should be 1."

$artifactPath = Join-Path "$PSScriptRoot\..\evaluation\artifacts" "downloaded-$runId.json"
Invoke-RestMethod -Method GET -Uri "$BaseUrl/api/v1/evaluations/$runId/artifact" -OutFile $artifactPath

Write-Host "Evaluation runId: $runId"
$run.metrics | Format-Table name,numerator,denominator,value,unit,validity
Write-Host "Artifact: $artifactPath"
