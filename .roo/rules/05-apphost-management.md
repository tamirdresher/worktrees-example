# AppHost Management Rules

## Critical Guidelines for Running Aspire AppHost

### ⚠️ MANDATORY: Always Use Scripts for AppHost Management

**NEVER run the AppHost directly using `dotnet run`**. This project uses port isolation to enable multiple instances running from different directories without conflicts.

## Required Approach

### ✅ DO: Use the Management Scripts

**⚠️ IMPORTANT: Choose the Right Script for Your Shell**

#### Git Bash (Windows)
If you're using Git Bash on Windows, you have two options:

**Option 1: Run PowerShell scripts directly (Recommended)**
```bash
# Start AppHost with automatic port allocation
powershell.exe -File ./scripts/start-apphost.ps1

# Stop specific instance gracefully (use Process ID from start output)
powershell.exe -File ./scripts/stop-apphost.ps1 -ProcessId <pid>

# Kill specific AppHost instance by PID
powershell.exe -File ./scripts/kill-apphost.ps1 -ProcessId <pid>

# Kill specific AppHost instance and all child processes (force)
powershell.exe -File ./scripts/kill-apphost.ps1 -ProcessId <pid> -Force

# Kill ALL AppHost instances from this repository
powershell.exe -File ./scripts/kill-apphost.ps1 -F

# List running instances
powershell.exe -File ./scripts/list-apphosts.ps1
```

**Option 2: Use bash scripts with correct path syntax**
```bash
# Start AppHost with automatic port allocation
./scripts/start-apphost.sh

# Stop specific instance gracefully (use Process ID from start output)
./scripts/stop-apphost.sh <pid>

# Kill specific AppHost instance by PID
./scripts/kill-apphost.sh <pid>

# Kill specific AppHost instance and all child processes (force)
./scripts/kill-apphost.sh <pid> -f

# Kill ALL AppHost instances from this repository
./scripts/kill-apphost.sh -F

# List running instances
./scripts/list-apphosts.sh
```

#### PowerShell (Windows)
```powershell
# Start AppHost with automatic port allocation
.\scripts\start-apphost.ps1

# Stop specific instance gracefully (use Process ID from start output)
.\scripts\stop-apphost.ps1 -ProcessId <pid>

# Kill specific AppHost instance by PID
.\scripts\kill-apphost.ps1 -ProcessId <pid>

# Kill specific AppHost instance and all child processes (force)
.\scripts\kill-apphost.ps1 -ProcessId <pid> -Force

# Kill ALL AppHost instances from this repository
.\scripts\kill-apphost.ps1 -F

# List running instances
.\scripts\list-apphosts.ps1
```

#### Linux/macOS (Bash)
```bash
# Start AppHost with automatic port allocation
./scripts/start-apphost.sh

# Stop specific instance gracefully (use Process ID from start output)
./scripts/stop-apphost.sh <pid>

# Kill specific AppHost instance by PID
./scripts/kill-apphost.sh <pid>

# Kill specific AppHost instance and all child processes (force)
./scripts/kill-apphost.sh <pid> -f

# Kill ALL AppHost instances from this repository
./scripts/kill-apphost.sh -F

# List running instances
./scripts/list-apphosts.sh
```

### ❌ DON'T: Run AppHost Directly

**NEVER do this:**
```powershell
# ❌ This causes port conflicts!
cd src/NoteTaker.AppHost
dotnet run
```

## Why This Matters

### Port Isolation Architecture
The AppHost requires multiple ports:
1. **Dashboard Port** - Aspire dashboard UI
2. **OTLP Endpoint Port** - OpenTelemetry data collection
3. **Resource Service Port** - Internal service communication
4. **MCP Endpoint Port** - Model Context Protocol server for AI assistant integration

### Without Scripts (Problems)
- ❌ Uses hardcoded default ports
- ❌ Port conflicts when multiple instances run
- ❌ Manual port management required
- ❌ Environment variables not set correctly
- ❌ Processes left running in background

### With Scripts (Solution)
- ✅ Automatic port allocation (finds free ports)
- ✅ No conflicts between instances
- ✅ Environment variables configured properly
- ✅ Process management (start/stop/kill)
- ✅ Dashboard URL automatically detected
- ✅ Clean shutdown support

## Common Workflows

### Development Workflow
```powershell
# 1. Start AppHost
.\scripts\start-apphost.ps1
# Output: Dashboard URL: https://localhost:54772/login?t=...
#         Process ID: 12345

# 2. Develop and test
# ... work on your code ...

# 3. When done, kill the specific instance (use PID from start output)
.\scripts\kill-apphost.ps1 -PID 12345 -Force
```

### Multi-Instance Testing
```powershell
# Terminal 1 - Clone A
cd C:\repos\clone-a
.\scripts\start-apphost.ps1
# Gets ports: 54772-54774

# Terminal 2 - Clone B
cd C:\repos\clone-b
.\scripts\start-apphost.ps1
# Gets ports: 61447-61449

# Both run simultaneously without conflicts! ✅
```

### Cleanup After Debugging

**Option 1: Kill all AppHost instances from this repo (recommended)**
```powershell
# Kill all AppHost instances running from this repository
.\scripts\kill-apphost.ps1 -F
```

**Option 2: Kill specific instances by PID**
```powershell
# List all running instances
.\scripts\list-apphosts.ps1

# Kill each instance by PID (use PIDs from list output)
.\scripts\kill-apphost.ps1 -ProcessId 12345 -Force
.\scripts\kill-apphost.ps1 -ProcessId 67890 -Force
```

## Script Reference

| Script | Purpose | When to Use |
|--------|---------|-------------|
| [`start-apphost.ps1`](../../scripts/start-apphost.ps1) / [`.sh`](../../scripts/start-apphost.sh) | Start AppHost with auto port allocation | Every time you need to run AppHost |
| [`stop-apphost.ps1`](../../scripts/stop-apphost.ps1) / [`.sh`](../../scripts/stop-apphost.sh) | Gracefully stop specific instance | When shutting down cleanly |
| [`kill-apphost.ps1`](../../scripts/kill-apphost.ps1) / [`.sh`](../../scripts/kill-apphost.sh) | Kill AppHost process(es):<br/>• `-ProcessId <pid>` - Kill specific process<br/>• `-ProcessId <pid> -Tree` - Kill process tree<br/>• `-All` - Kill ALL from this repo | Cleanup after debugging/testing |
| [`list-apphosts.ps1`](../../scripts/list-apphosts.ps1) / [`.sh`](../../scripts/list-apphosts.sh) | List all running instances | Check what's running |

## Environment Variables

The scripts automatically configure these required environment variables:

```powershell
$env:ASPIRE_DASHBOARD_PORT = "54772"                    # Dynamic
$env:ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL = "http://localhost:54773"  # Dynamic
$env:ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL = "http://localhost:54774"     # Dynamic
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"          # Required for HTTP in dev
```

**DO NOT set these manually** - the scripts handle this automatically.

## Troubleshooting

### Resources Failed to Start or Unhealthy
**⚠️ IMPORTANT: Always try restarting before troubleshooting**

If resources show as "Failed", "Unhealthy", or stuck in "Waiting" state in the Aspire dashboard:

```powershell
# 1. Kill the AppHost (use PID from your start output or list-apphosts)
.\scripts\kill-apphost.ps1 -PID <pid> -Force

# 2. Restart it
.\scripts\start-apphost.ps1
```

**Why this works:**
- Docker containers may fail to initialize properly on first run
- Cosmos DB emulator health checks can timeout initially
- Background services may have transient startup issues
- Port conflicts from previous runs can cause failures

**Only investigate deeper if:**
- Resources still fail after 2-3 restarts
- Same resource consistently fails across restarts
- Error messages indicate configuration issues

### "Port already in use" Error
```powershell
# List running instances to find PIDs
.\scripts\list-apphosts.ps1

# Kill each instance
.\scripts\kill-apphost.ps1 -PID <pid> -Force

# Try again
.\scripts\start-apphost.ps1
```

### Instance Not Stopping
```powershell
# Use force kill with PID
.\scripts\kill-apphost.ps1 -PID <pid> -Force
```

### Dashboard URL Not Working
- The dashboard URL shown is for monitoring output only
- The AppHost port isolation works correctly to prevent conflicts
- Focus on ensuring no port conflicts occur

### Forgot to Clean Up

**Quick cleanup (recommended):**
```powershell
# Kill all AppHost instances from this repository
.\scripts\kill-apphost.ps1 -F
```

**Or manual cleanup by PID:**
```powershell
# Check what's running
.\scripts\list-apphosts.ps1

# Kill each instance by PID
.\scripts\kill-apphost.ps1 -ProcessId <pid1> -Force
.\scripts\kill-apphost.ps1 -ProcessId <pid2> -Force
```

## Integration with Development Tools

### Visual Studio Code
When debugging or running in VS Code, ensure you're using the scripts:
1. Open integrated terminal (Ctrl+`)
2. Run: `.\scripts\start-apphost.ps1` (note the Process ID from output)
3. Use VS Code to debug other projects
4. Cleanup: `.\scripts\kill-apphost.ps1 -PID <pid> -Force`

### Visual Studio
If running from Visual Studio:
1. Close any VS-launched AppHost instances
2. Use scripts from PowerShell/Terminal instead
3. This ensures proper port isolation

### Aspire MCP Server Integration (Roo AI Assistant)

The project includes an MCP (Model Context Protocol) server that allows AI assistants like Roo to interact with the Aspire Dashboard programmatically. This is useful for automated diagnostics, troubleshooting, and monitoring.

#### When to Use Aspire MCP Tools

**✅ Use Aspire MCP tools when:**
- Investigating resource health issues (Failed, Unhealthy states)
- Analyzing distributed traces across services
- Reviewing console logs for errors or warnings
- Checking structured logs with specific filters
- Monitoring resource states during development
- Diagnosing startup or runtime failures
- Comparing resource behavior across worktrees

**ℹ️ How it works:**
1. You start AppHost using [`start-apphost.ps1`](../../scripts/start-apphost.ps1)
2. Roo connects to the Aspire Dashboard via the MCP server (configured in [`.roo/mcp.json`](../../.roo/mcp.json))
3. You can ask Roo to use MCP tools to analyze your running application
4. The MCP server queries the Aspire Dashboard and returns results

#### Available MCP Tools

The Aspire MCP server provides 6 diagnostic tools:

**1. [`list_resources`](../../docs/features/aspire-mcp-dynamic-port-proxy.md#1-list_resources)**
- Lists all application resources (projects, containers, executables)
- Shows running state, endpoints, health status, and relationships
- **When to use**: Get an overview of all resources and their current state

**2. [`execute_resource_command`](../../docs/features/aspire-mcp-dynamic-port-proxy.md#2-execute_resource_command)**
- Executes commands on resources (start, stop, restart)
- **When to use**: Restart a failed resource or stop a misbehaving service

**3. [`list_console_logs`](../../docs/features/aspire-mcp-dynamic-port-proxy.md#3-list_console_logs)**
- Retrieves console output (stdout/stderr) from resources
- Includes output from resource commands (start, stop, restart)
- **When to use**: Check why a resource isn't starting or investigate runtime errors

**4. [`list_structured_logs`](../../docs/features/aspire-mcp-dynamic-port-proxy.md#4-list_structured_logs)**
- Gets structured logs with filtering by resource, level, and search text
- **When to use**: Find specific log messages or errors across all resources

**5. [`list_traces`](../../docs/features/aspire-mcp-dynamic-port-proxy.md#5-list_traces)**
- Lists distributed traces with IDs, resources, duration, and error status
- **When to use**: Track operations across multiple services or find slow requests

**6. [`list_trace_structured_logs`](../../docs/features/aspire-mcp-dynamic-port-proxy.md#6-list_trace_structured_logs)**
- Gets logs for a specific trace ID (belongs to spans)
- **When to use**: Deep-dive into a specific operation's execution path

#### Common Diagnostic Workflows

**Scenario 1: Resources Failed to Start**
```
You: "List the Aspire resources and their health status"
Roo: Uses list_resources tool → Shows resource with "Failed" state

You: "Get the console logs for the failed resource"
Roo: Uses list_console_logs → Shows startup errors

You: "Restart the failed resource"
Roo: Uses execute_resource_command with "restart"
```

**Scenario 2: Investigating Slow Requests**
```
You: "Show recent traces that took longer than 5 seconds"
Roo: Uses list_traces → Identifies slow trace IDs

You: "Get the logs for trace ID abc-123"
Roo: Uses list_trace_structured_logs → Shows span details and timings
```

**Scenario 3: Finding Specific Errors**
```
You: "Search structured logs for 'NullReferenceException'"
Roo: Uses list_structured_logs with search parameter → Shows matching log entries
```

#### Configuration

The MCP server is pre-configured in [`.roo/mcp.json`](../../.roo/mcp.json):

```json
{
  "mcpServers": {
    "aspire-mcp": {
      "command": "dotnet",
      "args": ["scripts/aspire-mcp-proxy.cs", "--no-build"],
      "env": {},
      "description": "Aspire Dashboard MCP stdio proxy using official ModelContextProtocol SDK. Forwards MCP requests from stdio to Aspire's HTTP MCP endpoint. Update ASPIRE_MCP_PORT and ASPIRE_MCP_API_KEY from the Aspire Dashboard MCP dialog after starting AppHost with ./scripts/start-apphost.ps1",
      "alwaysAllow": [
        "list_resources",
        "execute_resource_command",
        "list_traces",
        "list_trace_structured_logs",
        "list_console_logs",
        "list_structured_logs"
      ],
      "timeout": 300,
      "disabled": false
    }
  }
}
```

**Settings Persistence**: The MCP server stores connection details in `scripts/settings.json` and updates automatically when AppHost restarts with new ports.

#### Troubleshooting MCP Connection

**Issue: Roo can't connect to Aspire tools**

**Solution 1: Reload VS Code Window (Recommended)**
- Press `Ctrl+Shift+P` → "Developer: Reload Window"
- This refreshes the MCP connection

**Solution 2: Verify AppHost is Running**
```powershell
# Check if AppHost is running
.\scripts\list-apphosts.ps1

# If not running, start it
.\scripts\start-apphost.ps1
```

```bash
# Check if AppHost is running
./scripts/list-apphosts.sh

# If not running, start it
./scripts/start-apphost.sh
```

#### Best Practices for Using MCP Tools

**✅ DO:**
- Use MCP tools for quick diagnostics during development
- Ask Roo to analyze resource health before deep-diving manually
- Leverage structured log filtering to find specific issues quickly
- Use traces to understand cross-service operation flows
- Check console logs when resources fail to start

**❌ DON'T:**
- Rely solely on MCP tools for production debugging (use full dashboard)
- Execute resource commands without understanding their impact
- Forget to reload VS Code after restarting AppHost
- Use MCP tools when AppHost isn't running (they'll return cached/stale data)

#### Documentation

For complete details on the Aspire MCP server:
- **Feature Documentation**: [`docs/features/aspire-mcp-dynamic-port-proxy.md`](../../docs/features/aspire-mcp-dynamic-port-proxy.md)
- **Tool Reference**: See "Available Tools" section in the feature documentation
- **MCP Configuration**: [`.roo/mcp.json`](../../.roo/mcp.json)

## Best Practices

### ✅ Always Do
- Use scripts for ALL AppHost operations
- Note the Process ID when starting AppHost
- Clean up when done:
  - Quick: `kill-apphost.ps1 -All` (kills all from this repo) OR `kill-apphost.sh --all`
  - Specific: `kill-apphost.ps1 -ProcessId <pid> -Tree` OR `kill-apphost.psh --pid <pid> --tree`
- Check running instances with `list-apphosts.ps1` OR `list-apphosts.sh` before starting new ones
- Read the dashboard URL from script output

### ❌ Never Do
- Run `dotnet run` directly in AppHost directory
- Manually set environment variables
- Leave instances running overnight
- Run AppHost from Visual Studio's "Run" button without scripts

## Documentation References

- **Quick Reference**: [`APPHOST-USAGE.md`](../../APPHOST-USAGE.md) - User guide
- **Script Details**: [`docs/README-APPHOST-SCRIPTS.md`](../../docs/README-APPHOST-SCRIPTS.md) - Comprehensive documentation
- **Technical Plan**: [`docs/APPHOST-PORT-ISOLATION-PLAN.md`](../../docs/APPHOST-PORT-ISOLATION-PLAN.md) - Implementation details
- **Main README**: [`README.md`](../../README.md) - Updated with warnings

## Summary

**The Golden Rule:** Always use [`start-apphost.ps1`](../../scripts/start-apphost.ps1) or [`start-apphost.sh`](../../scripts/start-apphost.sh) to run the AppHost. Clean up when done using:
- **Quick cleanup**: [`kill-apphost.ps1 -All`](../../scripts/kill-apphost.ps1) or [`kill-apphost.sh --all`](../../scripts/kill-apphost.sh) (kills all from repo)
- **Specific cleanup**: [`kill-apphost.ps1 -ProcessId <pid> -Tree`](../../scripts/kill-apphost.ps1) or [`kill-apphost.sh --pid <pid> --tree`](../../scripts/kill-apphost.sh)

This ensures:
- No port conflicts
- Proper environment configuration
- Clean process management
- Multi-instance support