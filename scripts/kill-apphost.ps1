<#
.SYNOPSIS
    Kills Aspire AppHost process(es). Supports killing a single PID, killing a PID process tree,
    or bulk-killing all AppHosts in the current git worktree.

.DESCRIPTION
    Invokes kill-apphost.cs which supports:
      - Normal kill (single PID)
      - -Tree <PID> kill (kills entire process tree)
      - -All kill (kills all matching AppHosts in current git worktree)

.PARAMETER ProcessId
    The process ID to kill. Required unless -All is specified.

.PARAMETER Tree
    If specified with -ProcessId, kills the entire process tree for that PID.

.PARAMETER All
    If specified, kills all AppHost processes in the current git worktree.
    Must NOT be combined with -ProcessId.

.EXAMPLE
    .\kill-apphost.ps1 -ProcessId 12345

.EXAMPLE
    .\kill-apphost.ps1 -ProcessId 12345 -Tree

.EXAMPLE
    .\kill-apphost.ps1 -All
#>

param(
    [Parameter(Mandatory = $false)]
    [int]$ProcessId,

    [switch]$Tree,

    [switch]$All
)

$scriptDir = $PSScriptRoot
$killAppCs = Join-Path $scriptDir "kill-apphost.cs"

if (-not (Test-Path $killAppCs)) {
    Write-Host "ERROR: kill-apphost.cs not found at $killAppCs" -ForegroundColor Red
    exit 1
}

if ($All -and $PSBoundParameters.ContainsKey('ProcessId')) {
    Write-Host "ERROR: -All cannot be combined with -ProcessId." -ForegroundColor Red
    exit 1
}

if (-not $All) {
    if (-not $PSBoundParameters.ContainsKey('ProcessId') -or $ProcessId -le 0) {
        Write-Host "ERROR: You must provide -ProcessId unless you specify -All." -ForegroundColor Red
        Write-Host "Examples:" -ForegroundColor Gray
        Write-Host "  .\kill-apphost.ps1 -ProcessId 12345" -ForegroundColor Gray
        Write-Host "  .\kill-apphost.ps1 -ProcessId 12345 -Tree" -ForegroundColor Gray
        Write-Host "  .\kill-apphost.ps1 -All" -ForegroundColor Gray
        exit 1
    }

    if ($Tree -and -not $PSBoundParameters.ContainsKey('ProcessId')) {
        Write-Host "ERROR: -Tree requires -ProcessId." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Killing AppHost..." -ForegroundColor Cyan

Push-Location $scriptDir
try {
    if ($All) {
        dotnet run "$killAppCs" -- -All
    }
    elseif ($Tree) {
        dotnet run "$killAppCs" -- -Tree $ProcessId
    }
    else {
        dotnet run "$killAppCs" -- $ProcessId
    }
}
finally {
    Pop-Location
}
