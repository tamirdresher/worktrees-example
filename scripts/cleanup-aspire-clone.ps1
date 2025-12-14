#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleanup all Docker containers and volumes for the current Aspire clone.

.DESCRIPTION
    This script removes all Docker containers and volumes created by Aspire
    for the current clone (based on git folder name). This is useful for:
    - Complete cleanup when switching branches
    - Resolving container/volume conflicts
    - Fresh start after testing

.PARAMETER Force
    Skip confirmation prompts and force removal of all resources.

.PARAMETER DryRun
    Show what would be removed without actually removing anything.

.EXAMPLE
    .\scripts\cleanup-aspire-clone.ps1
    Interactive cleanup with confirmation prompts

.EXAMPLE
    .\scripts\cleanup-aspire-clone.ps1 -Force
    Force cleanup without prompts

.EXAMPLE
    .\scripts\cleanup-aspire-clone.ps1 -DryRun
    Preview what would be removed
#>

param(
    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Determine clone name from git folder
function Get-CloneName {
    try {
        $gitDir = git rev-parse --git-dir 2>$null
        if ($gitDir) {
            $gitCommonDir = git rev-parse --git-common-dir 2>$null
            if ($gitCommonDir -and $gitCommonDir -ne $gitDir) {
                # This is a worktree
                $worktreePath = Split-Path -Parent $gitDir
                return Split-Path -Leaf $worktreePath
            } else {
                # Regular clone or main worktree
                $repoRoot = git rev-parse --show-toplevel 2>$null
                if ($repoRoot) {
                    return Split-Path -Leaf $repoRoot
                }
            }
        }
    } catch {
        # Ignore errors
    }
    return "default"
}

$cloneName = Get-CloneName
Write-Host "Clone Name: $cloneName" -ForegroundColor Cyan
Write-Host ""

# Find all containers for this clone
Write-Host "Searching for Aspire containers..." -ForegroundColor Yellow
$containerPatterns = @(
    "Onboarding-$cloneName",
    "*-$cloneName",
    "aspire-*-$cloneName"
)

$containers = @()
foreach ($pattern in $containerPatterns) {
    $found = docker ps -a --filter "name=$pattern" --format "{{.ID}}|{{.Names}}" 2>$null
    if ($found) {
        $containers += $found
    }
}

# Find all volumes for this clone
Write-Host "Searching for Aspire volumes..." -ForegroundColor Yellow
$volumePatterns = @(
    "cosmosdata-$cloneName",
    "storage-$cloneName",
    "*-$cloneName"
)

$volumes = @()
foreach ($pattern in $volumePatterns) {
    $found = docker volume ls --filter "name=$pattern" --format "{{.Name}}" 2>$null
    if ($found) {
        $volumes += $found
    }
}

# Display what will be removed
Write-Host ""
Write-Host "=== Resources to Remove ===" -ForegroundColor Cyan
Write-Host ""

if ($containers.Count -eq 0) {
    Write-Host "  No containers found" -ForegroundColor Gray
} else {
    Write-Host "Containers ($($containers.Count)):" -ForegroundColor Yellow
    foreach ($container in $containers) {
        $parts = $container -split '\|'
        $id = $parts[0]
        $name = $parts[1]
        Write-Host "  - $name ($id)" -ForegroundColor White
    }
}

Write-Host ""

if ($volumes.Count -eq 0) {
    Write-Host "  No volumes found" -ForegroundColor Gray
} else {
    Write-Host "Volumes ($($volumes.Count)):" -ForegroundColor Yellow
    foreach ($volume in $volumes) {
        Write-Host "  - $volume" -ForegroundColor White
    }
}

Write-Host ""

# Exit if dry run
if ($DryRun) {
    Write-Host "[DRY RUN] No changes made" -ForegroundColor Green
    exit 0
}

# Exit if nothing to remove
if ($containers.Count -eq 0 -and $volumes.Count -eq 0) {
    Write-Host "Nothing to remove" -ForegroundColor Green
    exit 0
}

# Confirm removal
if (-not $Force) {
    Write-Host "WARNING: This will permanently remove all listed resources!" -ForegroundColor Red
    $response = Read-Host "Continue? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Host "Cancelled" -ForegroundColor Yellow
        exit 0
    }
}

# Remove containers
if ($containers.Count -gt 0) {
    Write-Host ""
    Write-Host "Removing containers..." -ForegroundColor Yellow
    foreach ($container in $containers) {
        $parts = $container -split '\|'
        $id = $parts[0]
        $name = $parts[1]
        
        try {
            Write-Host "  Removing $name..." -NoNewline
            docker rm -f $id 2>&1 | Out-Null
            Write-Host " ✓" -ForegroundColor Green
        } catch {
            Write-Host " ✗ Failed: $_" -ForegroundColor Red
        }
    }
}

# Remove volumes
if ($volumes.Count -gt 0) {
    Write-Host ""
    Write-Host "Removing volumes..." -ForegroundColor Yellow
    foreach ($volume in $volumes) {
        try {
            Write-Host "  Removing $volume..." -NoNewline
            docker volume rm $volume 2>&1 | Out-Null
            Write-Host " ✓" -ForegroundColor Green
        } catch {
            Write-Host " ✗ Failed: $_" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "Cleanup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Note: You may need to restart the AppHost to recreate containers." -ForegroundColor Cyan