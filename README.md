# .NET Aspire with Git Worktrees & AI Agents

## Project Overview

This project is a polyglot cloud-native application (C#, Node.js, Python) designed to demonstrate a modern development workflow combining **.NET Aspire**, **Git Worktrees**, and **AI Agents**.

The core goal is to solve the "port conflict" problem when running multiple instances of a distributed application simultaneously. This allows you to have an AI agent (like Roo) working on a feature branch in a separate worktree while you continue debugging or developing on the `main` branch in another worktree, without them stepping on each other's toes.

## The "Worktree" Workflow

### The Problem
In a typical .NET Aspire project, ports for the Dashboard, OTLP endpoint, and resources are often static or default to specific values. If you try to run the application from two different directories (e.g., `main` and `feature-x`), they will crash because they compete for the same ports.

### The Solution
This project uses a set of **management scripts** that wrap the AppHost startup. These scripts:
1.  **Dynamically allocate free ports** for the Aspire Dashboard, OTLP, and internal service communication.
2.  **Configure environment variables** automatically so the application knows which ports to use.
3.  **Enable Port Isolation**, allowing multiple instances of the full application stack to run side-by-side on the same machine.

## AI Agent Integration (MCP)

This project includes a **Model Context Protocol (MCP)** server that acts as a bridge between your AI Agent and the running Aspire application.

When you start the application using the scripts, an MCP server is spun up that allows agents like Roo to:
*   **List Resources:** See what services are running and their health status.
*   **Read Logs:** Access console and structured logs to diagnose issues.
*   **Trace Requests:** Follow distributed traces across the .NET, Node.js, and Python services.
*   **Execute Commands:** Restart services or trigger specific actions directly from the chat interface.

This enables a workflow where the AI agent can autonomously build, run, and debug the application within its own isolated worktree.

## 🚀 How to Run

**⚠️ IMPORTANT:** Do NOT use `dotnet run` directly in the AppHost directory. You must use the provided scripts to ensure port isolation works.

### 1. Start the Application

**Windows (PowerShell):**
```powershell
.\scripts\start-apphost.ps1
```

**Linux/macOS (Bash):**
```bash
./scripts\start-apphost.sh
```

The script will output the **Dashboard URL** and the **Process ID**.

### 2. Stop the Application

To clean up resources properly (including child processes), use the kill script.

**Windows (PowerShell):**
```powershell
# Kill all instances from this repo
.\scripts\kill-apphost.ps1 -All
```

**Linux/macOS (Bash):**
```bash
# Kill all instances from this repo
./scripts/kill-apphost.sh --all
```

## Architecture Stack

*   **Orchestrator:** .NET Aspire
*   **Backend:** .NET 10 Minimal API
*   **Frontend:** Node.js (Express)
*   **AI Service:** Python (FastAPI + TextBlob)
*   **Infrastructure:**
    *   PostgreSQL (Data)
    *   Redis (Caching)
    *   RabbitMQ (Messaging)
*   **Observability:** OpenTelemetry (configured across all services)

## Documentation

For detailed instructions on the management scripts and worktree workflow, please see:
*   [AppHost Management Rules](.roo/rules/05-apphost-management.md)
*   [Script Documentation](docs/README-APPHOST-SCRIPTS.md)