<#
.SYNOPSIS
    Stops a running Aspire AppHost process.

.DESCRIPTION
    This script stops an Aspire AppHost process by Process ID.
    It attempts graceful shutdown first, then forceful termination if needed.

.PARAMETER ProcessId
    The Process ID of the AppHost to stop. Use list-apphosts.ps1 to find process IDs.

.PARAMETER JobId
    (Legacy) The ID of the background job to stop. Kept for backward compatibility.

.EXAMPLE
    .\stop-apphost.ps1 -ProcessId 12345
    Stops the AppHost with process ID 12345.

.EXAMPLE
    .\stop-apphost.ps1 -JobId 5
    Stops the AppHost running in job ID 5 (legacy mode).
#>

param(
    [Parameter(Mandatory=$false, ParameterSetName='ProcessId', Position=0)]
    [int]$ProcessId,
    
    [Parameter(Mandatory=$false, ParameterSetName='JobId')]
    [int]$JobId
)

# Handle both ProcessId and JobId for backward compatibility
if ($ProcessId) {
    Write-Host "Stopping Aspire AppHost (Process ID: $ProcessId)..." -ForegroundColor Yellow
    
    # Check if process exists
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    
    if (-not $process) {
        Write-Host "Error: Process with ID $ProcessId not found." -ForegroundColor Red
        Write-Host ""
        Write-Host "Available dotnet processes:" -ForegroundColor Gray
        Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -like "*NoteTaker.AppHost*" } |
            Format-Table Id, ProcessName, StartTime -AutoSize
        exit 1
    }
    
    # Display process info
    Write-Host "Process Details:" -ForegroundColor Gray
    Write-Host "  ID:          $($process.Id)" -ForegroundColor Gray
    Write-Host "  Name:        $($process.ProcessName)" -ForegroundColor Gray
    Write-Host "  Start Time:  $($process.StartTime)" -ForegroundColor Gray
    Write-Host ""
    
    # Stop the process
    try {
        Write-Host "Stopping process gracefully..." -ForegroundColor Yellow
        $process.CloseMainWindow() | Out-Null
        
        # Wait for graceful shutdown
        $timeout = 10
        $process.WaitForExit($timeout * 1000) | Out-Null
        
        # If still running, force kill
        if (-not $process.HasExited) {
            Write-Host "Process did not exit gracefully, forcing termination..." -ForegroundColor Yellow
            Stop-Process -Id $ProcessId -Force -ErrorAction Stop
        }
        
        Write-Host ""
        Write-Host "AppHost stopped successfully." -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host ""
        Write-Host "Error stopping AppHost: $_" -ForegroundColor Red
        Write-Host ""
        exit 1
    }
}
elseif ($JobId) {
    # Legacy mode - support stopping by Job ID
    Write-Host "Stopping Aspire AppHost (Job ID: $JobId)..." -ForegroundColor Yellow
    
    # Check if job exists
    $job = Get-Job -Id $JobId -ErrorAction SilentlyContinue
    
    if (-not $job) {
        Write-Host "Error: Job with ID $JobId not found." -ForegroundColor Red
        Write-Host ""
        Write-Host "Available jobs:" -ForegroundColor Gray
        Get-Job | Format-Table Id, State, Command -AutoSize
        exit 1
    }
    
    # Display job info
    Write-Host "Job Details:" -ForegroundColor Gray
    Write-Host "  ID:      $($job.Id)" -ForegroundColor Gray
    Write-Host "  State:   $($job.State)" -ForegroundColor Gray
    Write-Host "  Command: $($job.Command)" -ForegroundColor Gray
    Write-Host ""
    
    # Stop the job
    try {
        if ($job.State -eq 'Running') {
            Write-Host "Stopping job..." -ForegroundColor Yellow
            Stop-Job -Id $JobId -ErrorAction Stop
            
            # Wait a moment for graceful shutdown
            Start-Sleep -Seconds 2
        }
        
        # Remove the job
        Write-Host "Removing job from job list..." -ForegroundColor Yellow
        Remove-Job -Id $JobId -Force -ErrorAction Stop
        
        Write-Host ""
        Write-Host "AppHost stopped successfully." -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host ""
        Write-Host "Error stopping AppHost: $_" -ForegroundColor Red
        Write-Host ""
        exit 1
    }
}
else {
    Write-Host "Error: Either -ProcessId or -JobId must be specified." -ForegroundColor Red
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Gray
    Write-Host "  .\stop-apphost.ps1 -ProcessId <pid>" -ForegroundColor Gray
    Write-Host "  .\stop-apphost.ps1 -JobId <jobid>   (legacy)" -ForegroundColor Gray
    Write-Host ""
    exit 1
}