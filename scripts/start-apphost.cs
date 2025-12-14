// app.cs
// Usage:
//   dotnet run app.cs
//   dotnet run app.cs -- <AppHostProjectPath>
//
// Example:
//   dotnet run app.cs -- ../MyCustomAppHost
 
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

// when run from the scripts directory)
string defaultAppHostPath = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "../src/NoteTaker.AppHost")
);
 
// Resolve project path: optional first argument
string projectPath = args.Length > 0 ? args[0] : defaultAppHostPath;
 
// Normalize to full path
projectPath = Path.GetFullPath(projectPath);
 
if (!Directory.Exists(projectPath))
{
    WriteColoredLine($"Error: Project path not found: {projectPath}", ConsoleColor.Red);
    return;
}
 
WriteColoredLine("========================================", ConsoleColor.Cyan);
WriteColoredLine("Aspire AppHost Starter (C# wrapper)", ConsoleColor.Cyan);
WriteColoredLine("========================================", ConsoleColor.Cyan);
Console.WriteLine();
 
WriteColoredLine($"Project: {projectPath}", ConsoleColor.Gray);
Console.WriteLine();
 
// Allocate ports like the original PowerShell script
WriteColoredLine("Finding available port...", ConsoleColor.Yellow);
int dashboardPort = GetFreePort();
int otlpPort = dashboardPort + 1;
int resourcePort = dashboardPort + 2;
int mcpPort = dashboardPort + 3;
 
WriteColoredLine("Allocated ports:", ConsoleColor.Green);
WriteColoredLine($"  Dashboard:         {dashboardPort}", ConsoleColor.Green);
WriteColoredLine($"  MCP Endpoint:      {mcpPort}", ConsoleColor.Green);

// Update MCP Proxy settings
try
{
    // Assuming we are in the scripts directory
    string settingsDir = Directory.GetCurrentDirectory();
    Directory.CreateDirectory(settingsDir);
    string settingsPath = Path.Combine(settingsDir, "settings.json");
    
    string jsonContent = $@"{{
  ""port"": ""{mcpPort}"",
  ""apiKey"": ""McpKey"",
  ""lastUpdated"": ""{DateTime.UtcNow:o}""
}}";
    File.WriteAllText(settingsPath, jsonContent);
    WriteColoredLine($"Updated MCP settings at {settingsPath}", ConsoleColor.Gray);
}
catch (Exception ex)
{
    WriteColoredLine($"Warning: Failed to update MCP settings: {ex.Message}", ConsoleColor.Yellow);
}

// You can uncomment these if you want to print them:
// WriteColoredLine($"  OTLP Endpoint:     {otlpPort}", ConsoleColor.Green);
// WriteColoredLine($"  Resource Service:  {resourcePort}", ConsoleColor.Green);
Console.WriteLine();
 
// Prepare environment variables
string dashboardBaseUrl = $"https://localhost:{dashboardPort}";
string otlpUrl = $"https://localhost:{otlpPort}";
string resourceUrl = $"https://localhost:{resourcePort}";
string mcpUrl = $"http://localhost:{mcpPort}";

var psi = new ProcessStartInfo
{
    FileName = "dotnet",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = false,
    WorkingDirectory = projectPath
};
 
// Always: dotnet run --project <path> --no-launch-profile
psi.ArgumentList.Add("run");
psi.ArgumentList.Add("--project");
psi.ArgumentList.Add(projectPath);
psi.ArgumentList.Add("--no-launch-profile");  // Ignore launchSettings.json, use env vars
 
// Env vars like the PowerShell script
psi.Environment["ASPNETCORE_URLS"] = dashboardBaseUrl;
psi.Environment["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"] = otlpUrl;
psi.Environment["ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL"] = resourceUrl;
psi.Environment["ASPIRE_DASHBOARD_MCP_ENDPOINT_URL"] = mcpUrl;
psi.Environment["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true";
psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
psi.Environment["DOTNET_ENVIRONMENT"] = "Development";
psi.Environment["ASPIRE_MCP_PORT"] = mcpPort.ToString();
psi.Environment["AppHost__McpApiKey"]="McpKey";
 
WriteColoredLine("Starting AppHost in background...", ConsoleColor.Yellow);
 
var process = new Process
{
    StartInfo = psi,
    EnableRaisingEvents = true
};
 
try
{
    if (!process.Start())
    {
        WriteColoredLine("Error: Failed to start process.", ConsoleColor.Red);
        return;
    }
}
catch (Exception ex)
{
    WriteColoredLine($"Error starting process: {ex.Message}", ConsoleColor.Red);
    return;
}
 
int pid = process.Id;
 
Console.WriteLine();
WriteColoredLine($"AppHost process started with PID: {pid}", ConsoleColor.Green);
Console.WriteLine();
WriteColoredLine("Waiting for AppHost to initialize...", ConsoleColor.Yellow);
WriteColoredLine("This wrapper will exit when the dashboard is ready, or if the process ends.", ConsoleColor.Gray);
Console.WriteLine();
 
// TCS completes when:
// - dashboard is detected (true), OR
// - process exits before that (false)
var dashboardReadyTcs = new TaskCompletionSource<bool>(
    TaskCreationOptions.RunContinuationsAsynchronously);
 
process.Exited += (_, _) =>
{
    dashboardReadyTcs.TrySetResult(false);
};
 
void HandleLine(string? line)
{
    if (line == null) return;
 
    // Mirror output from AppHost (no color; preserve original formatting)
    Console.WriteLine(line);
 
    if (IsDashboardReadyLine(line, dashboardPort))
        dashboardReadyTcs.TrySetResult(true);
}
 
process.OutputDataReceived += (_, e) => HandleLine(e.Data);
process.ErrorDataReceived  += (_, e) => HandleLine(e.Data);
 
process.BeginOutputReadLine();
process.BeginErrorReadLine();
 
bool dashboardReady = await dashboardReadyTcs.Task;
 
if (dashboardReady)
{
    Console.WriteLine();
    WriteColoredLine("========================================", ConsoleColor.Green);
    WriteColoredLine("AppHost Started Successfully!", ConsoleColor.Green);
    WriteColoredLine("========================================", ConsoleColor.Green);
    Console.WriteLine();
 
    WriteColoredLine($"Dashboard URL: {dashboardBaseUrl}", ConsoleColor.Cyan);
    WriteColoredLine($"Process ID:    {pid}", ConsoleColor.Cyan);
    Console.WriteLine();
 
    WriteColoredLine("Note: The dashboard may take a few seconds to become available.", ConsoleColor.Yellow);
    WriteColoredLine("      If you see a login page, the token is in the AppHost console output.", ConsoleColor.Yellow);
    Console.WriteLine();
 
    WriteColoredLine("The AppHost is running as an independent background process.", ConsoleColor.Cyan);
    WriteColoredLine("This wrapper will now exit.", ConsoleColor.Cyan);
    Console.WriteLine();
 
    process.Dispose();
    return;
}
 
// Process exited before the dashboard was detected
Console.WriteLine();
WriteColoredLine("========================================", ConsoleColor.Red);
WriteColoredLine("Failed to start AppHost", ConsoleColor.Red);
WriteColoredLine("========================================", ConsoleColor.Red);
Console.WriteLine();
 
WriteColoredLine("Process exited before the Aspire dashboard was ready.", ConsoleColor.Yellow);
WriteColoredLine($"PID was: {pid}", ConsoleColor.Yellow);
Console.WriteLine();
 
WriteColoredLine("Common causes:", ConsoleColor.Yellow);
WriteColoredLine("  - Missing dependencies (try 'dotnet restore')", ConsoleColor.Gray);
WriteColoredLine("  - Port already in use", ConsoleColor.Gray);
WriteColoredLine("  - Invalid project configuration", ConsoleColor.Gray);
Console.WriteLine();
 
process.Dispose();
 
 
// -------------------------------
// Helper Methods
// -------------------------------
 
static int GetFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    try
    {
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
    finally
    {
        listener.Stop();
    }
}
 
// Heuristic detection of the Aspire dashboard "is up" line
static bool IsDashboardReadyLine(string line, int port)
{
    bool mentionsPort = line.Contains($"localhost:{port}", StringComparison.OrdinalIgnoreCase);
    bool hasHttp = line.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("https://", StringComparison.OrdinalIgnoreCase);
    bool mentionsAspire = line.Contains("aspire", StringComparison.OrdinalIgnoreCase) ||
                          line.Contains("dashboard", StringComparison.OrdinalIgnoreCase);
    bool hasToken = line.Contains("/login?t=", StringComparison.OrdinalIgnoreCase);
 
    if (hasHttp && mentionsPort && (mentionsAspire || hasToken))
        return true;
 
    if (hasHttp && mentionsAspire && hasToken)
        return true;
 
    return false;
}
 
static void WriteColoredLine(string text, ConsoleColor color)
{
    var old = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = old;
}
 