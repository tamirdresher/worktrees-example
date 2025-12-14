#!/usr/bin/env pwsh
# Cleanup script - Stops all Cosmos DB containers and removes clone state

Write-Host "Clone Cleanup" -ForegroundColor Red
Write-Host "======================================" -ForegroundColor Red
Write-Host ""

# Stop all running Cosmos DB containers
Write-Host "Stopping Cosmos DB containers..." -ForegroundColor Yellow
$containers = docker ps -a --filter "ancestor=mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator" --format "{{.ID}}"

if ($containers) {
    $containers | ForEach-Object {
        Write-Host "  Removing container: $_" -ForegroundColor Gray
        docker rm -f $_ | Out-Null
    }
    Write-Host "All Cosmos DB containers stopped." -ForegroundColor Green
} else {
    Write-Host "No Cosmos DB containers found." -ForegroundColor Gray
}

Write-Host ""

# Remove state file
$stateFile = ".aspire/state/cosmos-clones.json"
if (Test-Path $stateFile) {
    Write-Host "Removing clone state file..." -ForegroundColor Yellow
    Remove-Item -Path $stateFile -Force
    Write-Host "Clone state file removed." -ForegroundColor Green
} else {
    Write-Host "No clone state file found." -ForegroundColor Gray
}

Write-Host ""
Write-Host "Cleanup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Note: Docker volumes are preserved. To remove them, run:" -ForegroundColor Cyan
Write-Host "  docker volume ls | Select-String 'cosmosdata' | ForEach-Object { docker volume rm `$_.ToString().Split()[1] }" -ForegroundColor Gray