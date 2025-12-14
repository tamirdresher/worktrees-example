<#
.SYNOPSIS
    Starts the Aspire AppHost via the C# wrapper (app.cs).
 
.DESCRIPTION
    This script simply calls `dotnet run app.cs` which:
      - Finds a free port and configures the Aspire AppHost
      - Starts the AppHost process
      - Streams its output
      - Detects the dashboard URL
      - Prints the URL and PID, then exits while AppHost keeps running
 
.PARAMETER ProjectPath
    Path to the AppHost project directory.
    Defaults to ../src/NoteTaker.AppHost (relative to this script), same as before.
 
.PARAMETER Timeout
    Currently not used (reserved for future enhancements).
    The C# wrapper exits when the dashboard is ready or the process ends.
 
.EXAMPLE
    .\start-apphost.ps1
 
.EXAMPLE
    .\start-apphost.ps1 -ProjectPath "./MyAppHost"
#>
 
param(
    [string]$ProjectPath = "$PSScriptRoot/../src/NoteTaker.AppHost",
    [int]$Timeout = 0  # Reserved, currently ignored (logic lives in app.cs)
)
 
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Aspire AppHost Starter (PowerShell front-end)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
 
# Resolve project path (so app.cs gets a clean absolute path)
$resolvedPath = Resolve-Path $ProjectPath -ErrorAction SilentlyContinue
 
if (-not $resolvedPath) {
    Write-Host "Error: Project path not found: $ProjectPath" -ForegroundColor Red
    exit 1
}
 
Write-Host "Project: $resolvedPath" -ForegroundColor Gray
Write-Host ""
 
# Ensure app.cs exists next to this script
$appCsPath = Join-Path $PSScriptRoot "start-apphost.cs"
if (-not (Test-Path $appCsPath)) {
    Write-Host "Error: start-apphost.cs not found at: $appCsPath" -ForegroundColor Red
    Write-Host "Please place start-apphost.cs in the same folder as this script." -ForegroundColor Yellow
    exit 1
}
 
Write-Host "Running C# wrapper: start-apphost.cs" -ForegroundColor Yellow
Write-Host ""
 
Push-Location $PSScriptRoot
try {
    # Call:
    #   dotnet run start-apphost.cs
    # or:
    #   dotnet run start-apphost.cs -- <ProjectPath>
    if ($ProjectPath) {
        dotnet run start-apphost.cs -- "$resolvedPath"
    } else {
        dotnet run start-apphost.cs
    }
}
finally {
    Pop-Location
}
 