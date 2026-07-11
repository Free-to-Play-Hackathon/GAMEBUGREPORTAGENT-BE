param(
    [string]$Dataset = "demo-v1",
    [string]$Environment = $env:ASPNETCORE_ENVIRONMENT,
    [string]$Confirm
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Environment)) {
    $Environment = "Local"
}

if ($Environment -notin @("Local", "Demo", "Test")) {
    throw "Seed/reset is only allowed in Local, Demo, or Test. Current environment: $Environment"
}

if ($Confirm -ne "GAMEBUG_DEMO_RESET") {
    throw "Refusing to seed/reset without --confirm GAMEBUG_DEMO_RESET"
}

if ($Dataset -ne "demo-v1") {
    throw "Only demo-v1 is supported by the MVP seed script."
}

$env:ASPNETCORE_ENVIRONMENT = $Environment
dotnet run --project src/GameBug.Api -- seed --dataset $Dataset --confirm $Confirm
