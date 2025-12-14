# AppHost Management Scripts Structure

Here is the ASCII diagram of the repository structure focusing on the AppHost management scripts.

```text
.
├── scripts/
│   ├── start-apphost.ps1       # 🚀 Starts AppHost with auto-port allocation
│   ├── start-apphost.sh        #    (Bash version for Linux/Mac/Git Bash)
│   │
│   ├── list-apphosts.ps1       # 📋 Lists all running AppHost instances & PIDs
│   ├── list-apphosts.sh        #    (Bash version)
│   │
│   ├── stop-apphost.ps1        # 🛑 Gracefully stops a specific instance
│   ├── stop-apphost.sh         #    (Bash version)
│   │
│   ├── kill-apphost.ps1        # 💀 Force kills instances (specific PID or --All)
│   ├── kill-apphost.sh         #    (Bash version)
│   │
│   └── aspire-mcp-proxy.cs     # 🤖 MCP Server Proxy for AI Assistant integration
│
└── src/
    └── NoteTaker.AppHost/      # 🧠 The Aspire Orchestrator project
```

## Usage Summary

| Action | PowerShell | Bash |
|--------|------------|------|
| **Start** | `.\scripts\start-apphost.ps1` | `./scripts/start-apphost.sh` |
| **List** | `.\scripts\list-apphosts.ps1` | `./scripts/list-apphosts.sh` |
| **Stop** | `.\scripts\stop-apphost.ps1 -ProcessId <PID>` | `./scripts/stop-apphost.sh <PID>` |
| **Kill All** | `.\scripts\kill-apphost.ps1 -All` | `./scripts/kill-apphost.sh --all` |