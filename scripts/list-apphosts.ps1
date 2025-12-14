<#
.SYNOPSIS
    Lists all running Aspire AppHost processes.

.DESCRIPTION
    This script invokes the list-apphosts.cs C# wrapper to display
    information about all Aspire AppHost instances, including process ID,
    memory usage, and start time.

.EXAMPLE
    .\list-apphosts.ps1
    Lists all AppHost instances with their details.
#>

$scriptDir = $PSScriptRoot
$listAppCs = Join-Path $scriptDir "list-apphosts.cs"

if (-not (Test-Path $listAppCs)) {
    Write-Host "ERROR: list-apphosts.cs not found at $listAppCs" -ForegroundColor Red
    exit 1
}

Push-Location $scriptDir
try {
    dotnet run $listAppCs
}
finally {
    Pop-Location
}